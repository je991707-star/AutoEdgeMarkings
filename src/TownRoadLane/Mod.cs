using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Net;
using Game.SceneFlow;
using TownRoadLane.Localization;
using TownRoadLane.Systems.Emission;
using TownRoadLane.Systems.Prefabs;
using TownRoadLane.Systems.Tool;
using TownRoadLane.Systems.Topology;
using TownRoadLane.Systems.UI;
#if DEBUG
using TownRoadLane.Diagnostics;
#endif

namespace TownRoadLane
{
    /// <summary>
    /// Entry point. Keeps the vanilla <see cref="SecondaryLaneSystem"/> enabled and registers the
    /// prefab clone systems, <see cref="MarkingSuppressionSystem"/> (to hide vanilla markings on
    /// overridden or user-marked nodes), the node marking tool and the systems that turn
    /// user-drawn lines and fills into game entities.
    /// </summary>
    public class Mod : IMod
    {
        public static ILog log = CreateLog();

        private static ILog CreateLog()
        {
            var logger = LogManager.GetLogger($"{nameof(TownRoadLane)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
            // By default UnityLogger reopens and closes the file on every write. When an open
            // fails (antivirus or cloud sync holding the file), Open() swallows the error, the
            // writer stays null and the write throws a NullReferenceException outside its
            // IOException catch, so it escapes into whichever system was logging. Keeping the
            // stream open means one open per session.
            logger.keepStreamOpen = true;
            return logger;
        }

        public static TownRoadLaneSetting Settings { get; private set; }
        // TownRoadLaneUISystem needs the live instance to resolve the mod's ExecutableAsset path:
        // ModManager indexes assets by mod instance, so a new Mod() would not be found.
        public static Mod Instance { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            // The defaults instance must be created before the live one: every ModSetting ctor
            // registers itself in the static ModSetting.instances[id] map, and the game resolves
            // the id to whichever instance was constructed last.
            var settingDefaults = new TownRoadLaneSetting(this);
            Settings = new TownRoadLaneSetting(this);
            // Must run before GetAction() resolves anything, otherwise the ToggleMarkingTool
            // ProxyAction silently never fires. Traffic registers key bindings in the same order.
            Settings.RegisterKeyBindings();
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            GameManager.instance.localizationManager.AddSource("ru-RU", new LocaleRU(Settings));
            AssetDatabase.global.LoadSettings(nameof(TownRoadLane), Settings, settingDefaults);
            // A decode failure silently falls back to SetDefaults(), so log what the load actually
            // produced; user reports then show the real state.
            log.Info($"settings loaded: edge={Settings.EdgeLineEnabled}/{Settings.EdgeLineStyle}, parking={Settings.ParkingMarkingsEnabled}/{Settings.ParkingLineStyle}/{Settings.ParkingEndStyle}, pins='{Settings.PinnedLineStylesCsv}'/'{Settings.PinnedAreaStylesCsv}'");
            if (Settings.VerboseLogging)
            {
                log.effectivenessLevel = Level.Debug;
                log.Info("verbose logging on");
            }

            // Vanilla-surface fill styles; see VanillaSurfaceLateClone for why they are registered
            // after loading.
            VanillaSurfaceLateClone.Register(updateSystem.World);

#if DEBUG
            // Developer prefab surveys. Debug builds only, and even there off unless the hidden
            // DiagnosticDumps setting is on: they write tens of thousands of lines per boot.
            if (Settings.DiagnosticDumps)
            {
                // Read-only structural dump, useful when something changes between game patches.
                updateSystem.UpdateAt<RoadPrefabDumpSystem>(SystemUpdatePhase.PrefabUpdate);
                // One-shot probes for Shader.Find and the vanilla SurfacePrefab inventory.
                // Disables itself after its second pass.
                updateSystem.UpdateAt<AreasPrototypeSystem>(SystemUpdatePhase.PrefabUpdate);
            }
            log.Info($"diagnostic dumps: {(Settings.DiagnosticDumps ? "ON" : "off")}");
            // ParkingPairDumpSystem (parking endpoint debugging) is not registered by default;
            // add it here in GameSimulation when needed.
#endif

            // Marking prefab clones. Must run BEFORE PrefabInitializeSystem: a prefab added after it
            // in the same PrefabUpdate tick skips initialization (invalid lane archetype) but still
            // gets indexed into host SecondaryNetLane buffers, and the first road built then crashes
            // natively in ModificationBarrier4B playback.
            updateSystem.UpdateBefore<EdgeLineCloneSystem, Game.Prefabs.PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateBefore<ParkingLineCloneSystem, Game.Prefabs.PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);

            // Keep vanilla SecondaryLaneSystem enabled for full compatibility with game patches
            // and mods such as Road Builder. Instead of replacing it, MarkingSuppressionSystem
            // runs in Modification5 right before SecondaryLaneReferencesSystem to remove vanilla
            // markings on suppressed edges/nodes before they enter SubLane buffers or render batches.
            updateSystem.UpdateBefore<MarkingSuppressionSystem, SecondaryLaneReferencesSystem>(SystemUpdatePhase.Modification5);
            log.Info("MarkingSuppressionSystem registered before SecondaryLaneReferencesSystem at Modification5 (vanilla SecondaryLaneSystem kept enabled)");

            // There is deliberately no system that reapplies settings at runtime: refreshing the
            // clone prefabs (UpdatePrefab) in a live world leaves existing sublanes with stale
            // PrefabRefs, and the next secondary lane rebuild crashes natively inside a Burst job.
            // Settings apply on the next save load: markings are saved with the map, so
            // VanillaLaneRefreshSystem marks all roads Updated once after loading and vanilla
            // SecondaryLaneSystem (Modification4B) rebuilds them with the current clone prefabs.
            updateSystem.UpdateAt<VanillaLaneRefreshSystem>(SystemUpdatePhase.Modification1);

            // ToolBaseSystem adds itself to ToolSystem.tools in OnCreate; registering is enough.
            // Game.UpdateSystem does not read Unity's [UpdateBefore]/[UpdateAfter] attributes, so
            // ordering is declared explicitly here.
            updateSystem.UpdateAt<MarkingNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateBefore<MarkingToolHotkeySystem, MarkingNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            // Idle unless the marking tool is active; writes to OverlayRenderSystem.Buffer.
            updateSystem.UpdateAt<MarkingOverlaySystem>(SystemUpdatePhase.Rendering);

            // User lines and fills are emitted as regular vanilla entities (SecondaryLane sublanes
            // and Game.Areas.Area) so the game's own renderer draws them. A custom mesh path
            // cannot work: the vanilla decal shaders need DOTS instance properties
            // (colossal_CurveMatrix) that only the BatchRendererGroup pipeline supplies.
            //
            // Modification1 runs before LaneSystem (4), SecondaryLaneSystem (4B),
            // MarkingSuppressionSystem (5) and SecondaryLaneReferencesSystem (5).
            updateSystem.UpdateAt<MarkingPairMigrationSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<MarkingTopologySystem, MarkingPairMigrationSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<MarkingSegmentEmissionSystem, MarkingTopologySystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<MarkingAreaTopologySystem, MarkingTopologySystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAfter<MarkingAreaEmissionSystem, MarkingAreaTopologySystem>(SystemUpdatePhase.Modification1);
            // Runs after vanilla Game.Areas.GeometrySystem (Modification2B) and replaces its
            // shrink-and-budget ear clipping, which can leave fills invisible, with a full
            // triangulation of the real outline.
            updateSystem.UpdateAfter<MarkingAreaTriangulationSystem, Game.Areas.GeometrySystem>(SystemUpdatePhase.Modification2B);

            updateSystem.UpdateAt<TownRoadLaneUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
