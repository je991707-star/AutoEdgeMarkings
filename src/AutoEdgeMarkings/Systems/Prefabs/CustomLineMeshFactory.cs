using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game.Prefabs;
using UnityEngine;

namespace AutoEdgeMarkings.Systems.Prefabs
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

        public const string kCrispWhiteSolid = "AEM Crisp White Solid Mesh";
        public const string kCrispWhiteDashed = "AEM Crisp White Dashed Mesh";
        public const string kCrispYellowSolid = "AEM Crisp Yellow Solid Mesh";
        public const string kBlueSolid = "AEM Blue Solid Mesh";
        public const string kWhiteTerminal = "AEM White Terminal Mesh";
        public const string kYellowTerminal = "AEM Yellow Terminal Mesh";
        // Parking-line variants on the short, dense dash geometry; the lane-divider dash above
        // makes a parking edge read as an extra driving lane.
        public const string kCrispWhiteParkingDashed = "AEM Crisp White Parking Dashed Mesh";
        public const string kCrispYellowParkingDashed = "AEM Crisp Yellow Parking Dashed Mesh";
        public const string kBlueParkingDashed = "AEM Blue Parking Dashed Mesh";

        private const string kVanillaSolidSource = "White Solid Line Mesh";
        private const string kVanillaDashedSource = "White Dashed Line Mesh";
        private const string kVanillaDenseDashedSource = "White Dashed Line Mesh - Dense";

        // Retain asset wrappers for the process lifetime so neither GC nor weak caches drop them.
        private static readonly List<SurfaceAsset> s_PinnedSurfaces = new List<SurfaceAsset>();
        private static readonly List<TextureAsset> s_PinnedTextures = new List<TextureAsset>();
        // Null until the first EnsureCustomLineMeshes call; then the meshes that built successfully.
        private static Dictionary<string, RenderPrefab> s_Built;

        private const string kAssetSubPath = "AutoEdgeMarkings";
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
            new() { name = kCrispWhiteSolid,  sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kCrispWhiteSolid, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f) },
            new() { name = kCrispWhiteDashed, sourceVanillaMesh = kVanillaDashedSource, buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kCrispWhiteDashed, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f, dashFraction: 0.54f) },
            new() { name = kCrispYellowSolid, sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kCrispYellowSolid, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.78f) },
            new() { name = kBlueSolid,        sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kBlueSolid, ProceduralTextureFactory.kPaintBlue, widthFraction: 0.72f) },
            new() { name = kWhiteTerminal,    sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kWhiteTerminal, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.88f) },
            new() { name = kYellowTerminal,   sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateLineTexture(kYellowTerminal, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.88f) },
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
        /// Builds and registers every built-in <see cref="RenderPrefab"/> on the first call (from
        /// whichever clone system runs first) and adds them to <paramref name="meshByName"/>, which
        /// must already hold <see cref="RequiredVanillaSources"/>. A mesh that fails to build is
        /// left out, so callers fall back to their vanilla mesh.
        /// </summary>
        public static void EnsureCustomLineMeshes(PrefabSystem prefabSystem, Dictionary<string, RenderPrefab> meshByName)
        {
            if (s_Built == null)
            {
                s_Built = new Dictionary<string, RenderPrefab>();
                foreach (var spec in kSpecs)
                {
                    try
                    {
                        var customMesh = CreateCustomMesh(meshByName[spec.sourceVanillaMesh], spec);
                        prefabSystem.AddPrefab(customMesh);
                        s_Built[spec.name] = customMesh;
                    }
                    catch (Exception e)
                    {
                        log.Warn($"CustomLineMeshFactory: failed to build '{spec.name}' ({e.GetType().Name}: {e.Message}) — callers will fall back to vanilla mesh");
                    }
                }
                log.Info($"CustomLineMeshFactory: registered {s_Built.Count}/{kSpecs.Length} built-in line RenderPrefab(s)");
            }

            foreach (var kv in s_Built)
                meshByName[kv.Key] = kv.Value;
        }

        private static RenderPrefab CreateCustomMesh(RenderPrefab srcMesh, MeshSpec spec)
        {
            SurfaceAsset srcSurface = srcMesh.surfaceAssets?.FirstOrDefault(s => s != null)
                ?? throw new InvalidOperationException($"'{srcMesh.name}' has no SurfaceAsset");

            // Loads the vanilla surface's data so CopyFrom below has something to copy.
            if (srcSurface.Load(0, loadTextures: true, TextureAsset.KeepOnCPU.Dont, useVT: false) == null)
                throw new InvalidOperationException($"failed to load material from '{srcMesh.name}'");

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

            var clone = (RenderPrefab)srcMesh.Clone(spec.name);
            clone.surfaceAssets = new[] { surfaceAsset };
            clone.manualVTRequired = false;
            return clone;
        }
    }
}
