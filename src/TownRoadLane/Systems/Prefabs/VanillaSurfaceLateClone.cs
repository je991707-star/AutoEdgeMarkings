using System;
using System.Reflection;
using Colossal.Core;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Game.Rendering;
using Game.SceneFlow;
using Unity.Entities;
using UnityEngine;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Registers all runtime <see cref="SurfacePrefab"/> fill styles used by
    /// <see cref="Emission.MarkingAreaEmissionSystem"/> without any external EAI or G87 asset
    /// dependency:
    ///   - Decorative vanilla surfaces (grass, sand, pavement, tiles; slots 15, 17-22) cloned from
    ///     vanilla prefabs with <see cref="DecalLayers.Roads"/> added.
    ///   - Built-in road-marking surfaces (junction box, white/yellow stripes, green bike lane,
    ///     red bus lane, asphalt patch; slots 1-6 and 14) cloned from vanilla concrete/asphalt
    ///     surfaces with self-contained procedural textures from <see cref="ProceduralTextureFactory"/>.
    ///
    /// Registration runs on a regular frame after loading: clones created while a save is loading
    /// render as the grey 'Missing Area' prefab because AreaBatchSystem only picks up prefabs in
    /// their Created frame.
    /// </summary>
    public static class VanillaSurfaceLateClone
    {
        private static readonly ILog log = Mod.log;

        // Prefab names are saved with the fill areas that use them, so they must never change.
        // Built-in road-marking surfaces (slots 1-6, 14):
        public const string kCloneJunctionBox = "TRL Junction Box Surface";
        public const string kCloneWhiteStripesDense = "TRL White Stripes Dense Surface";
        public const string kCloneWhiteStripesSparse = "TRL White Stripes Sparse Surface";
        public const string kCloneYellowStripesDense = "TRL Yellow Stripes Dense Surface";
        public const string kCloneGreenBike = "TRL Green Bike Lane Surface";
        public const string kCloneRedBus = "TRL Red Bus Lane Surface";
        public const string kCloneAsphaltPatch = "TRL Asphalt Patch Surface";

        // Vanilla decorative surface clones (slots 15, 17-22):
        public const string kCloneGrass = "TRL Grass Surface";
        public const string kCloneGrassDark = "TRL Grass Dark Surface";
        public const string kCloneSand = "TRL Sand Surface";
        public const string kClonePavement = "TRL Pavement Surface";
        public const string kCloneTiles1 = "TRL Tiles 1 Surface";
        public const string kCloneTiles2 = "TRL Tiles 2 Surface";
        public const string kCloneTiles3 = "TRL Tiles 3 Surface";

        private const string kConcreteSource = "Concrete Surface 01";
        private const string kVanillaAsphaltSource = "Asphalt Surface 01";

        private static readonly (string source, string clone)[] kVanillaSurfaceClones =
        {
            ("Grass Surface 01",    kCloneGrass),
            ("Grass Surface 02",    kCloneGrassDark),
            ("Sand Surface 01",     kCloneSand),
            ("Pavement Surface 01", kClonePavement),
            ("Tiles Surface 01",    kCloneTiles1),
            ("Tiles Surface 02",    kCloneTiles2),
            ("Tiles Surface 03",    kCloneTiles3),
        };

        private struct CustomSurfaceSpec
        {
            public string cloneName;
            public int priority;
            public DecalLayers layerMask;
            public float uvScale;
            public bool transparentPattern;
            public Func<Texture2D> buildTexture;
        }

        private static readonly CustomSurfaceSpec[] kCustomSurfaceSpecs =
        {
            new()
            {
                cloneName = kCloneJunctionBox,
                priority = 4, // draws over road markings
                layerMask = DecalLayers.Roads,
                uvScale = 0.50f,
                transparentPattern = true,
                buildTexture = () => ProceduralTextureFactory.CreateJunctionBoxTexture(),
            },
            new()
            {
                cloneName = kCloneWhiteStripesDense,
                priority = -10, // draws on roads under markings
                layerMask = DecalLayers.Roads,
                uvScale = 0.42f,
                transparentPattern = true,
                buildTexture = () => ProceduralTextureFactory.CreateDiagonalStripesTexture(
                    "TRL White Stripes Dense Texture", ProceduralTextureFactory.kPaintWhite, paintDuty: 0.50f, cycles: 4),
            },
            new()
            {
                cloneName = kCloneWhiteStripesSparse,
                priority = -10,
                layerMask = DecalLayers.Roads,
                uvScale = 0.42f,
                transparentPattern = true,
                buildTexture = () => ProceduralTextureFactory.CreateDiagonalStripesTexture(
                    "TRL White Stripes Sparse Texture", ProceduralTextureFactory.kPaintWhite, paintDuty: 0.34f, cycles: 2),
            },
            new()
            {
                cloneName = kCloneYellowStripesDense,
                priority = -10,
                layerMask = DecalLayers.Roads,
                uvScale = 0.42f,
                transparentPattern = true,
                buildTexture = () => ProceduralTextureFactory.CreateDiagonalStripesTexture(
                    "TRL Yellow Stripes Dense Texture", ProceduralTextureFactory.kPaintYellow, paintDuty: 0.50f, cycles: 4),
            },
            new()
            {
                cloneName = kCloneGreenBike,
                priority = -10,
                layerMask = DecalLayers.Roads,
                uvScale = 0.25f,
                transparentPattern = false,
                buildTexture = () => ProceduralTextureFactory.CreateTintedPavementTexture(
                    "TRL Green Bike Lane Texture", ProceduralTextureFactory.kBikeGreen, opacity: 0.88f, grainAmplitude: 0.16f),
            },
            new()
            {
                cloneName = kCloneRedBus,
                priority = -10,
                layerMask = DecalLayers.Roads,
                uvScale = 0.25f,
                transparentPattern = false,
                buildTexture = () => ProceduralTextureFactory.CreateTintedPavementTexture(
                    "TRL Red Bus Lane Texture", ProceduralTextureFactory.kBusRed, opacity: 0.88f, grainAmplitude: 0.16f),
            },
        };

        // Process-wide on purpose, never reset: Register runs once from Mod.OnLoad and the clones
        // stay in PrefabSystem for the whole process, so later save loads must not add them again.
        private static World _world;
        private static bool _done;
        private static int _gateFrames;
        private static bool _gateWarned;
        private const int kGateWarnFrames = 3600; // ≈ 1 min at 60 fps

        public static void Register(World world)
        {
            _world = world;
            MainThreadDispatcher.RegisterUpdater(TryInitialize);
        }

        // Runs every frame on the main thread until it returns true. Includes GameMode.Game so
        // the launcher's "Continue" button (which skips the main menu) still registers them.
        private static bool TryInitialize()
        {
            if (_done) return true;
            var gm = GameManager.instance;
            bool gateOpen = gm != null && gm.modManager.isInitialized
                && (gm.gameMode == GameMode.MainMenu || gm.gameMode == GameMode.Game)
                && gm.state != GameManager.State.Booting && gm.state != GameManager.State.Loading
                && _world != null && _world.IsCreated;
            if (!gateOpen)
            {
                if (++_gateFrames >= kGateWarnFrames && !_gateWarned)
                {
                    _gateWarned = true;
                    log.Warn($"[surface-registry] gate not passed after {kGateWarnFrames} frames (gameMode={gm?.gameMode.ToString() ?? "<no GameManager>"}, state={gm?.state.ToString() ?? "-"}) — built-in fill surfaces will fall back to concrete");
                }
                return false;
            }

            _done = true;
            try
            {
                CreateAllSurfaces();
            }
            catch (Exception e)
            {
                log.Error($"[surface-registry] creating built-in surfaces failed: {e}");
            }
            return true;
        }

        private static void CreateAllSurfaces()
        {
            var prefabSystem = _world.GetOrCreateSystemManaged<PrefabSystem>();
            log.Info($"[surface-registry] registering built-in fill surfaces (gameMode={GameManager.instance.gameMode})");

            // 1. Vanilla decorative surface clones (grass, sand, pavement, tiles).
            foreach (var (sourceName, cloneName) in kVanillaSurfaceClones)
            {
                if (AlreadyRegistered(prefabSystem, cloneName)) continue;
                if (!TryGetSource(prefabSystem, sourceName, out var src, out var srcRa)) continue;

                var clone = MakeClone(src, srcRa, cloneName);
                clone.TryGet<RenderedArea>(out var ra);
                if (srcRa.m_Material != null)
                    ra.m_Material = ProceduralTextureFactory.Pin(new Material(srcRa.m_Material) { name = cloneName + " Material" });
                ra.m_DecalLayerMask = srcRa.m_DecalLayerMask | DecalLayers.Roads;
                SyncMaterialProperties(ra);
                prefabSystem.AddPrefab(clone);
                log.Debug($"[surface-registry] registered '{cloneName}' ← '{sourceName}' (prio={ra.m_RendererPriority}, layer={ra.m_DecalLayerMask})");
            }

            // 2. Built-in road-marking surfaces (junction box, stripes, bike/bus lanes).
            if (TryGetSource(prefabSystem, kConcreteSource, out var concreteSrc, out var concreteRa))
            {
                foreach (var spec in kCustomSurfaceSpecs)
                {
                    if (AlreadyRegistered(prefabSystem, spec.cloneName)) continue;
                    RegisterCustomSurface(prefabSystem, concreteSrc, concreteRa, spec);
                }
            }
            else
            {
                log.Warn($"[surface-registry] vanilla '{kConcreteSource}' not found — custom road-marking surfaces skipped");
            }

            // 3. Asphalt patch: prefer cloning vanilla Asphalt Surface 01 if present; otherwise
            // build a textured dark-asphalt surface from Concrete Surface 01.
            if (!AlreadyRegistered(prefabSystem, kCloneAsphaltPatch))
            {
                if (TryGetSource(prefabSystem, kVanillaAsphaltSource, out var asphaltSrc, out var asphaltRa))
                {
                    var clone = MakeClone(asphaltSrc, asphaltRa, kCloneAsphaltPatch);
                    clone.TryGet<RenderedArea>(out var ra);
                    if (asphaltRa.m_Material != null)
                        ra.m_Material = ProceduralTextureFactory.Pin(new Material(asphaltRa.m_Material) { name = kCloneAsphaltPatch + " Material" });
                    ra.m_RendererPriority = -10;
                    ra.m_DecalLayerMask = DecalLayers.Terrain | DecalLayers.Roads;
                    SyncMaterialProperties(ra);
                    prefabSystem.AddPrefab(clone);
                    log.Debug($"[surface-registry] registered '{kCloneAsphaltPatch}' ← vanilla '{kVanillaAsphaltSource}'");
                }
                else if (concreteSrc != null && concreteRa != null)
                {
                    RegisterCustomSurface(prefabSystem, concreteSrc, concreteRa, new CustomSurfaceSpec
                    {
                        cloneName = kCloneAsphaltPatch,
                        priority = -10,
                        layerMask = DecalLayers.Terrain | DecalLayers.Roads,
                        uvScale = 0.25f,
                        transparentPattern = false,
                        buildTexture = () => ProceduralTextureFactory.CreateTintedPavementTexture(
                            "TRL Asphalt Patch Texture", ProceduralTextureFactory.kAsphaltDark, opacity: 0.96f, grainAmplitude: 0.22f),
                    });
                }
            }

            log.Info("[surface-registry] built-in fill surfaces ready");
        }

        private static void RegisterCustomSurface(PrefabSystem prefabSystem, SurfacePrefab baseSrc, RenderedArea baseRa, CustomSurfaceSpec spec)
        {
            var clone = MakeClone(baseSrc, baseRa, spec.cloneName);
            clone.TryGet<RenderedArea>(out var ra);

            Texture2D albedo = spec.buildTexture();
            Texture2D flatNormal = ProceduralTextureFactory.GetFlatNormalTexture();
            Texture2D paintMask = ProceduralTextureFactory.GetPaintMaskTexture();

            Material mat = baseRa.m_Material != null
                ? new Material(baseRa.m_Material) { name = spec.cloneName + " Material" }
                : new Material(Shader.Find("Shader Graphs/AreaDecalShader")) { name = spec.cloneName + " Material" };
            mat = ProceduralTextureFactory.Pin(mat);

            if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", albedo);
            mat.mainTexture = albedo;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", flatNormal);
            if (mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", paintMask);

            ra.m_Material = mat;
            // Clear AssetReferences copied from Concrete Surface 01 so they never override our custom material maps.
            ra.m_BaseColorMap = null;
            ra.m_NormalMap = null;
            ra.m_MaskMap = null;

            ra.m_BaseColor = Color.white;
            ra.m_RendererPriority = spec.priority;
            ra.m_DecalLayerMask = spec.layerMask;
            ra.m_UVScale = spec.uvScale;
            ra.m_NormalOpacity = spec.transparentPattern ? 0f : 0.25f;
            ra.m_MetallicOpacity = spec.transparentPattern ? 0.25f : 0.45f;
            ra.m_Smoothness = 0.20f;
            ra.m_Metallic = 0f;

            SyncMaterialProperties(ra);
            prefabSystem.AddPrefab(clone);
            log.Debug($"[surface-registry] registered custom '{spec.cloneName}' (prio={ra.m_RendererPriority}, layer={ra.m_DecalLayerMask}, uvScale={ra.m_UVScale})");
        }

        private static bool AlreadyRegistered(PrefabSystem prefabSystem, string cloneName)
            => prefabSystem.TryGetPrefab(new PrefabID(nameof(SurfacePrefab), cloneName), out _);

        private static bool TryGetSource(PrefabSystem prefabSystem, string sourceName, out SurfacePrefab src, out RenderedArea srcRa)
        {
            src = null;
            srcRa = null;
            if (!prefabSystem.TryGetPrefab(new PrefabID(nameof(SurfacePrefab), sourceName), out var srcBase)
                || srcBase is not SurfacePrefab found)
            {
                return false;
            }
            if (!found.TryGet<RenderedArea>(out var ra) || ra == null)
            {
                return false;
            }
            src = found;
            srcRa = ra;
            return true;
        }

        /// <summary>New SurfacePrefab with the source's serialized fields and its own RenderedArea.
        /// Only fields declared on these types are copied, so the PrefabBase/ComponentBase
        /// internals (component list, prefab back-references) are never shared with the
        /// vanilla original.</summary>
        private static SurfacePrefab MakeClone(SurfacePrefab src, RenderedArea srcRa, string name)
        {
            var clone = ScriptableObject.CreateInstance<SurfacePrefab>();
            clone.name = name;
            CopyDeclaredFields(src, clone, typeof(SurfacePrefab));
            CopyDeclaredFields(src, clone, typeof(AreaPrefab));

            var ra = clone.AddComponent<RenderedArea>();
            CopyDeclaredFields(srcRa, ra, typeof(RenderedArea));
            return clone;
        }

        private static void CopyDeclaredFields(object src, object dst, Type type)
        {
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                f.SetValue(dst, f.GetValue(src));
        }

        /// <summary>Keeps shader properties on <see cref="RenderedArea.m_Material"/> synchronized
        /// with the component fields.</summary>
        private static void SyncMaterialProperties(RenderedArea ra)
        {
            var mat = ra.m_Material;
            if (mat == null) return;
            if (mat.HasProperty("colossal_DecalLayerMask"))
                mat.SetFloat("colossal_DecalLayerMask", (float)(uint)ra.m_DecalLayerMask);
            if (mat.HasProperty("colossal_UVScale"))
                mat.SetFloat("colossal_UVScale", ra.m_UVScale);
            if (mat.HasProperty("_DrawOrder"))
                mat.SetFloat("_DrawOrder", ra.m_RendererPriority);
            if (mat.HasProperty("_NormalOpacity"))
                mat.SetFloat("_NormalOpacity", ra.m_NormalOpacity);
            if (mat.HasProperty("_MetallicOpacity"))
                mat.SetFloat("_MetallicOpacity", ra.m_MetallicOpacity);
        }
    }
}
