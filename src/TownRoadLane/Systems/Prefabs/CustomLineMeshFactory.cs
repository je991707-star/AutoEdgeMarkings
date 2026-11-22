using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game.Prefabs;
using UnityEngine;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Builds self-contained <see cref="RenderPrefab"/> line decals for the edge-line and parking
    /// style options by cloning vanilla line meshes (reusing their proven CurvedDecal geometry and
    /// bounds) and attaching in-memory <see cref="SurfaceAsset"/>s backed by
    /// <see cref="ProceduralTextureFactory"/> textures. No external asset dependency and no disk
    /// cache writes.
    /// </summary>
    internal static class CustomLineMeshFactory
    {
        private static readonly ILog log = Mod.log;

        public const string kCrispWhiteSolid = "TRL Crisp White Solid Mesh";
        public const string kCrispWhiteDashed = "TRL Crisp White Dashed Mesh";
        public const string kCrispYellowSolid = "TRL Crisp Yellow Solid Mesh";
        public const string kBlueSolid = "TRL Blue Solid Mesh";
        public const string kWhiteTerminal = "TRL White Terminal Mesh";
        public const string kYellowTerminal = "TRL Yellow Terminal Mesh";
        // Parking-line variants on the short, dense dash geometry; the lane-divider dash above
        // makes a parking edge read as an extra driving lane.
        public const string kCrispWhiteParkingDashed = "TRL Crisp White Parking Dashed Mesh";
        public const string kCrispYellowParkingDashed = "TRL Crisp Yellow Parking Dashed Mesh";
        public const string kBlueParkingDashed = "TRL Blue Parking Dashed Mesh";

        private const string kVanillaSolidSource = "White Solid Line Mesh";
        private const string kVanillaDashedSource = "White Dashed Line Mesh";
        private const string kVanillaDenseDashedSource = "White Dashed Line Mesh - Dense";

        // Retain asset wrappers for the process lifetime so neither GC nor weak caches drop them.
        private static readonly List<SurfaceAsset> s_PinnedSurfaces = new List<SurfaceAsset>();
        private static readonly List<TextureAsset> s_PinnedTextures = new List<TextureAsset>();
        private static readonly HashSet<string> s_Failed = new HashSet<string>();

        private const string kAssetSubPath = "TownRoadLane";
        private static ILocalAssetDatabase s_Database;

        // Transient in-memory database (only an empty temp folder is created; nothing is saved),
        // registered with AssetDatabase.global so AssetReference lookups by id find our assets.
        private static ILocalAssetDatabase Database
        {
            get
            {
                if (s_Database == null)
                {
                    s_Database = AssetDatabase.GetTransient(0L);
                    AssetDatabase.global.RegisterDatabase(s_Database).Wait();
                }
                return s_Database;
            }
        }

        private struct MeshSpec
        {
            public string name;
            public string sourceVanillaMesh;
            // Null keeps the vanilla albedo, which is where the dash pattern lives.
            public Func<Texture2D> buildTexture;
            // _BaseColor multiplier; null = white.
            public Color? tint;
        }

        private static readonly MeshSpec[] kSpecs =
        {
            new() { name = kCrispWhiteSolid,  sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kCrispWhiteSolid, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f) },
            new() { name = kCrispWhiteDashed, sourceVanillaMesh = kVanillaDashedSource, buildTexture = () => ProceduralTextureFactory.CreateDashedLineTexture(kCrispWhiteDashed, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f, dashFraction: 0.54f) },
            new() { name = kCrispYellowSolid, sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kCrispYellowSolid, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.78f) },
            new() { name = kBlueSolid,        sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kBlueSolid, ProceduralTextureFactory.kPaintBlue, widthFraction: 0.72f) },
            new() { name = kWhiteTerminal,    sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kWhiteTerminal, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.88f) },
            new() { name = kYellowTerminal,   sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kYellowTerminal, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.88f) },
            // Parking dashes keep the vanilla dense albedo: its short dash pattern is in the texture,
            // so a procedural one-dash-per-tile texture turns it back into lane-divider dashes.
            new() { name = kCrispWhiteParkingDashed,  sourceVanillaMesh = kVanillaDenseDashedSource },
            new() { name = kCrispYellowParkingDashed, sourceVanillaMesh = kVanillaDenseDashedSource, tint = (Color)ProceduralTextureFactory.kPaintYellow },
            new() { name = kBlueParkingDashed,        sourceVanillaMesh = kVanillaDenseDashedSource, tint = (Color)ProceduralTextureFactory.kPaintBlue },
        };

        /// <summary>All vanilla source mesh names required to build the custom line meshes.</summary>
        public static readonly string[] RequiredVanillaSources =
        {
            kVanillaSolidSource,
            kVanillaDashedSource,
            kVanillaDenseDashedSource,
        };

        /// <summary>
        /// Ensures every built-in custom <see cref="RenderPrefab"/> is registered with
        /// <paramref name="prefabSystem"/> and inserted into <paramref name="meshByName"/>.
        /// Idempotent and safe to call from both <see cref="EdgeLineCloneSystem"/> and
        /// <see cref="ParkingLineCloneSystem"/>.
        /// </summary>
        public static void EnsureCustomLineMeshes(PrefabSystem prefabSystem, Dictionary<string, RenderPrefab> meshByName)
        {
            int created = 0;
            for (int i = 0; i < kSpecs.Length; i++)
            {
                var spec = kSpecs[i];
                if (meshByName.ContainsKey(spec.name)) continue;
                // Both clone systems call this; a spec that already failed must not retry and re-log.
                if (s_Failed.Contains(spec.name)) continue;

                if (prefabSystem.TryGetPrefab(new PrefabID(nameof(RenderPrefab), spec.name), out var existingBase)
                    && existingBase is RenderPrefab existingMesh)
                {
                    meshByName[spec.name] = existingMesh;
                    continue;
                }

                if (!meshByName.TryGetValue(spec.sourceVanillaMesh, out var srcMesh) || srcMesh == null)
                {
                    log.Warn($"CustomLineMeshFactory: vanilla source '{spec.sourceVanillaMesh}' missing — skipping '{spec.name}'");
                    continue;
                }

                try
                {
                    var customMesh = CreateCustomMesh(srcMesh, spec);
                    if (customMesh != null)
                    {
                        prefabSystem.AddPrefab(customMesh);
                        meshByName[spec.name] = customMesh;
                        created++;
                    }
                }
                catch (Exception e)
                {
                    s_Failed.Add(spec.name);
                    log.Warn($"CustomLineMeshFactory: failed to build '{spec.name}' ({e.GetType().Name}: {e.Message}) — callers will fall back to vanilla mesh");
                }
            }

            if (created > 0)
                log.Info($"CustomLineMeshFactory: registered {created} built-in line/curb RenderPrefab(s)");
        }

        private static RenderPrefab CreateCustomMesh(RenderPrefab srcMesh, MeshSpec spec)
        {
            SurfaceAsset srcSurface = FirstSurfaceAsset(srcMesh);
            if (srcSurface == null)
            {
                log.Warn($"CustomLineMeshFactory: '{srcMesh.name}' has no SurfaceAsset — skipping '{spec.name}'");
                return null;
            }

            Material srcMat = srcSurface.Load(0, loadTextures: true, TextureAsset.KeepOnCPU.Dont, useVT: false);
            if (srcMat == null)
            {
                log.Warn($"CustomLineMeshFactory: failed to load material from '{srcMesh.name}' — skipping '{spec.name}'");
                return null;
            }

            // RenderPrefab stores surfaces as AssetReference ids that ManagedBatchSystem resolves
            // through AssetDatabase.global, so both assets must live in a registered database;
            // a bare `new SurfaceAsset()` resolves to null and CreateBatch throws.
            TextureAsset texAsset = null;
            if (spec.buildTexture != null)
            {
                texAsset = Database.AddAsset<TextureAsset>(
                    AssetDataPath.Create(kAssetSubPath, spec.name + "_BaseColorMap"),
                    Colossal.Hash128.CreateGuid(spec.name + "_BaseColorMap"));
                texAsset.SetData(spec.buildTexture());
                texAsset.Load(0, TextureAsset.KeepOnCPU.Object);
                s_PinnedTextures.Add(texAsset);
            }

            // CopyFrom carries the vanilla material template; SetData(Material) on a fresh
            // SurfaceAsset has none and throws "Template material was not found".
            var surfaceAsset = Database.AddAsset<SurfaceAsset>(
                AssetDataPath.Create(kAssetSubPath, spec.name + "_SurfaceAsset"),
                Colossal.Hash128.CreateGuid(spec.name + "_SurfaceAsset"));
            surfaceAsset.CopyFrom(srcSurface);
            if (texAsset != null) surfaceAsset.UpdateTexture("_BaseColorMap", texAsset);
            if (surfaceAsset.HasProperty("_BaseColor"))
                surfaceAsset.UpdateColors(new[] { new KeyValuePair<string, Color>("_BaseColor", spec.tint ?? Color.white) });
            // Pin refcount >= 1 so BatchMeshSystem never unloads this in-memory surface.
            surfaceAsset.Load(0, loadTextures: true, TextureAsset.KeepOnCPU.Object, useVT: false);
            s_PinnedSurfaces.Add(surfaceAsset);

            var clone = srcMesh.Clone(spec.name) as RenderPrefab;
            if (clone == null) return null;

            clone.surfaceAssets = new[] { surfaceAsset };
            clone.manualVTRequired = false;
            return clone;
        }

        private static SurfaceAsset FirstSurfaceAsset(RenderPrefab mesh)
        {
            if (mesh.surfaceAssets == null) return null;
            foreach (var s in mesh.surfaceAssets)
                if (s != null) return s;
            return null;
        }
    }
}
