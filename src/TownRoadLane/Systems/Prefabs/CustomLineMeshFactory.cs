using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game.Prefabs;
using UnityEngine;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Builds self-contained <see cref="RenderPrefab"/> line and curb decals by cloning vanilla
    /// line meshes (reusing their proven CurvedDecal geometry and bounds) and attaching in-memory
    /// <see cref="SurfaceAsset"/>s backed by <see cref="ProceduralTextureFactory"/> textures.
    ///
    /// Replaces all external G87 NetLaneDecal and Vanilla Curb RenderPrefabs with zero EAI
    /// dependency and zero disk cache writes.
    /// </summary>
    internal static class CustomLineMeshFactory
    {
        private static readonly ILog log = Mod.log;

        public const string kCrispWhiteSolid = "TRL Crisp White Solid Mesh";
        public const string kCrispWhiteDashed = "TRL Crisp White Dashed Mesh";
        public const string kCrispYellowSolid = "TRL Crisp Yellow Solid Mesh";
        public const string kCrispYellowDashed = "TRL Crisp Yellow Dashed Mesh";
        public const string kCurb = "TRL Curb Mesh";
        public const string kBlueSolid = "TRL Blue Solid Mesh";
        public const string kBlueDashed = "TRL Blue Dashed Mesh";
        public const string kWhiteTerminal = "TRL White Terminal Mesh";
        public const string kYellowTerminal = "TRL Yellow Terminal Mesh";

        private const string kVanillaSolidSource = "White Solid Line Mesh";
        private const string kVanillaDashedSource = "White Dashed Line Mesh";

        // Retain asset wrappers for the process lifetime so neither GC nor weak caches drop them.
        private static readonly List<SurfaceAsset> s_PinnedSurfaces = new List<SurfaceAsset>();
        private static readonly List<TextureAsset> s_PinnedTextures = new List<TextureAsset>();

        private struct MeshSpec
        {
            public string name;
            public string sourceVanillaMesh;
            public Func<Texture2D> buildTexture;
            public int drawOrderOffset;
        }

        private static readonly MeshSpec[] kSpecs =
        {
            new() { name = kCrispWhiteSolid,  sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kCrispWhiteSolid, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f) },
            new() { name = kCrispWhiteDashed, sourceVanillaMesh = kVanillaDashedSource, buildTexture = () => ProceduralTextureFactory.CreateDashedLineTexture(kCrispWhiteDashed, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.78f, dashFraction: 0.54f) },
            new() { name = kCrispYellowSolid, sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kCrispYellowSolid, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.78f) },
            new() { name = kCrispYellowDashed,sourceVanillaMesh = kVanillaDashedSource, buildTexture = () => ProceduralTextureFactory.CreateDashedLineTexture(kCrispYellowDashed, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.78f, dashFraction: 0.54f) },
            new() { name = kCurb,             sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateCurbTexture(), drawOrderOffset = 1 },
            new() { name = kBlueSolid,        sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kBlueSolid, ProceduralTextureFactory.kPaintBlue, widthFraction: 0.72f) },
            new() { name = kBlueDashed,       sourceVanillaMesh = kVanillaDashedSource, buildTexture = () => ProceduralTextureFactory.CreateDashedLineTexture(kBlueDashed, ProceduralTextureFactory.kPaintBlue, widthFraction: 0.72f, dashFraction: 0.52f) },
            new() { name = kWhiteTerminal,    sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kWhiteTerminal, ProceduralTextureFactory.kPaintWhite, widthFraction: 0.88f) },
            new() { name = kYellowTerminal,   sourceVanillaMesh = kVanillaSolidSource,  buildTexture = () => ProceduralTextureFactory.CreateSolidLineTexture(kYellowTerminal, ProceduralTextureFactory.kPaintYellow, widthFraction: 0.88f) },
        };

        /// <summary>All vanilla source mesh names required to build the custom line meshes.</summary>
        public static readonly string[] RequiredVanillaSources =
        {
            kVanillaSolidSource,
            kVanillaDashedSource,
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

            Texture2D tex = spec.buildTexture();
            var mat = ProceduralTextureFactory.Pin(new Material(srcMat) { name = spec.name + " Material" });
            if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", tex);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (spec.drawOrderOffset != 0 && mat.HasProperty("_DrawOrder"))
                mat.SetFloat("_DrawOrder", mat.GetFloat("_DrawOrder") + spec.drawOrderOffset);

            var texAsset = new TextureAsset
            {
                id = new Identifier(Colossal.Hash128.CreateGuid(spec.name + "_BaseColorMap")),
            };
            texAsset.SetData(tex);
            texAsset.Load(0, TextureAsset.KeepOnCPU.Object);
            s_PinnedTextures.Add(texAsset);

            var surfaceAsset = new SurfaceAsset
            {
                id = new Identifier(Colossal.Hash128.CreateGuid(spec.name + "_SurfaceAsset")),
            };
            surfaceAsset.SetData(mat);
            surfaceAsset.UpdateTexture("_BaseColorMap", texAsset);
            // Pin refcount >= 1 so BatchMeshSystem never unloads this in-memory surface.
            surfaceAsset.Load(0, loadTextures: true, TextureAsset.KeepOnCPU.Object, useVT: false);
            s_PinnedSurfaces.Add(surfaceAsset);

            var clone = srcMesh.Clone(spec.name) as RenderPrefab;
            if (clone == null) return null;

            clone.surfaceAssets = new[] { surfaceAsset };
            clone.manualVTRequired = false;

            if (spec.drawOrderOffset != 0 && clone.TryGet<DecalProperties>(out var decalProps) && decalProps != null)
                decalProps.m_RendererPriority += spec.drawOrderOffset;

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
