using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Net;
using Game.SceneFlow;
using AutoEdgeMarkings.Localization;
using AutoEdgeMarkings.Systems.Emission;
using AutoEdgeMarkings.Systems.Prefabs;
#if DEBUG
using AutoEdgeMarkings.Diagnostics;
#endif

namespace AutoEdgeMarkings
{
    /// <summary>
    /// Entry point. Registers the prefab clone systems that add automatic markings (curb-side
    /// edge line, North American yellow left line, parallel parking lines) to vanilla lanes; the
    /// vanilla <see cref="SecondaryLaneSystem"/> stays enabled and places them.
    /// </summary>
    public class Mod : IMod
    {
        public static ILog log = CreateLog();

        private static ILog CreateLog()
        {
            var logger = LogManager.GetLogger($"{nameof(AutoEdgeMarkings)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
            // By default UnityLogger reopens and closes the file on every write. When an open
            // fails (antivirus or cloud sync holding the file), Open() swallows the error, the
            // writer stays null and the write throws a NullReferenceException outside its
            // IOException catch, so it escapes into whichever system was logging. Keeping the
            // stream open means one open per session.
            logger.keepStreamOpen = true;
            return logger;
        }

        public static AutoEdgeMarkingsSetting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            // The defaults instance must be created before the live one: every ModSetting ctor
            // registers itself in the static ModSetting.instances[id] map, and the game resolves
            // the id to whichever instance was constructed last.
            var settingDefaults = new AutoEdgeMarkingsSetting(this);
            Settings = new AutoEdgeMarkingsSetting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            GameManager.instance.localizationManager.AddSource("ru-RU", new LocaleRU(Settings));
            AssetDatabase.global.LoadSettings(nameof(AutoEdgeMarkings), Settings, settingDefaults);
            // A decode failure silently falls back to SetDefaults(), so log what the load actually
            // produced; user reports then show the real state.
            log.Info($"settings loaded: edge={Settings.EdgeLineEnabled}/{Settings.EdgeLineStyle}, yellowLeft={Settings.YellowLeftLineEnabled}, parking={Settings.ParkingMarkingsEnabled}/{Settings.ParkingLineStyle}/{Settings.ParkingEndStyle}");
            if (Settings.VerboseLogging)
            {
                log.effectivenessLevel = Level.Debug;
                log.Info("verbose logging on");
            }

#if DEBUG
            // Developer prefab survey. Debug builds only, and even there off unless the hidden
            // DiagnosticDumps setting is on: it writes tens of thousands of lines per boot.
            if (Settings.DiagnosticDumps)
                updateSystem.UpdateAt<RoadPrefabDumpSystem>(SystemUpdatePhase.PrefabUpdate);
            log.Info($"diagnostic dumps: {(Settings.DiagnosticDumps ? "ON" : "off")}");
#endif

            // Marking prefab clones. Must run BEFORE PrefabInitializeSystem: a prefab added after it
            // in the same PrefabUpdate tick skips initialization (invalid lane archetype) but still
            // gets indexed into host SecondaryNetLane buffers, and the first road built then crashes
            // natively in ModificationBarrier4B playback.
            updateSystem.UpdateBefore<EdgeLineCloneSystem, Game.Prefabs.PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateBefore<ParkingLineCloneSystem, Game.Prefabs.PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);

            // There is deliberately no system that reapplies settings at runtime: refreshing the
            // clone prefabs (UpdatePrefab) in a live world leaves existing sublanes with stale
            // PrefabRefs, and the next secondary lane rebuild crashes natively inside a Burst job.
            // The clone systems register once per process, so setting changes need a game restart
            // and only affect roads built or rebuilt afterwards. Markings are saved with the map,
            // so on a new game VanillaLaneRefreshSystem marks the map's roads Updated once and
            // vanilla SecondaryLaneSystem (Modification4B) rebuilds them with the clone prefabs.
            updateSystem.UpdateAt<VanillaLaneRefreshSystem>(SystemUpdatePhase.Modification1);
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
