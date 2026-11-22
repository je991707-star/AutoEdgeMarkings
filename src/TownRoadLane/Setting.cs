using System;
using System.Threading.Tasks;
using Colossal.Core;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using TownRoadLane.Systems.Prefabs;

namespace TownRoadLane
{
    /// <summary>
    /// Mod settings: an on/off switch and a mesh style (vanilla or built-in procedural) per
    /// automatic feature. Each clone system reads its style when it builds prefabs. Style
    /// changes need a game restart (see the note below the parking group).
    /// </summary>
    [FileLocation(nameof(TownRoadLane))]
    [SettingsUIGroupOrder(kEdgeGroup, kParkingGroup)]
    [SettingsUIShowGroupName(kEdgeGroup, kParkingGroup)]
    // The class name must be unique among all installed mods: ApplyAndSave() calls
    // AssetDatabase.SaveSpecificSetting(GetType().Name), which matches settings by bare type name
    // across every mod and takes the first hit. With the template name "Setting" and another
    // template-based mod installed, changes are written to the other mod's file and ours never
    // persist.
    public class TownRoadLaneSetting : ModSetting
    {
        public const string kSection = "Main";
        public const string kEdgeGroup = "EdgeLine";
        public const string kParkingGroup = "ParkingMarkings";

        public TownRoadLaneSetting(IMod mod) : base(mod) { }

        // Vanilla ApplyAndSave() is async void: every checkbox click starts its own
        // read-modify-write of the settings file, and two quick changes race, so the slower task
        // can overwrite the faster one's write. ApplyAndSave isn't virtual, so instead Apply()
        // marks the state dirty and one extra save writes the final in-memory state once the UI
        // has been quiet for a couple of seconds.
        private const int kQuietFramesBeforeSave = 120; // ~2 s at 60 fps

        private bool _saveDirty;
        private int _quietFrames;
        private bool _saverRegistered;
        private volatile bool _saveInFlight;

        public override void Apply()
        {
            base.Apply();
            _saveDirty = true;
            _quietFrames = 0;
            if (_saverRegistered) return;
            _saverRegistered = true;
            MainThreadDispatcher.RegisterUpdater(SaveWhenQuiet);
        }

        // Never unregisters (returns false), so later Apply() calls reuse it. Idle cost is one
        // bool check per frame. Only one SaveSpecificSetting task is allowed in flight at a time;
        // any edits arriving while a save is running re-arm _saveDirty and flush once it finishes.
        private bool SaveWhenQuiet()
        {
            if (!_saveDirty || _saveInFlight) return false;
            if (++_quietFrames < kQuietFramesBeforeSave) return false;
            _saveDirty = false;
            _saveInFlight = true;
            _ = SaveNowAsync();
            return false;
        }

        private async Task SaveNowAsync()
        {
            try
            {
                await AssetDatabase.global.SaveSpecificSetting(GetType().Name);
                Mod.log.Debug($"settings: coalesced save landed (edge={EdgeLineEnabled}/{EdgeLineStyle}, parking={ParkingMarkingsEnabled}/{ParkingLineStyle}/{ParkingEndStyle})");
            }
            catch (Exception e)
            {
                Mod.log.Warn($"settings: coalesced save failed: {e.Message}");
            }
            finally
            {
                _saveInFlight = false;
            }
        }

        // Edge line: curb-side line on city roads with 3 m lanes.

        [SettingsUISection(kSection, kEdgeGroup)]
        public bool EdgeLineEnabled { get; set; } = true;

        [SettingsUISection(kSection, kEdgeGroup)]
        [SettingsUIDisableByCondition(typeof(TownRoadLaneSetting), nameof(IsEdgeDisabled))]
        public EdgeLineStyleEnum EdgeLineStyle { get; set; } = EdgeLineStyleEnum.WhiteSolid;

        // US convention: NA-theme cities get a yellow line on the left (median) edge of one-way
        // and divided carriageways, while the white edge line stays curb-side. No explicit theme
        // check is needed: the yellow clone is based on the NA prefab and inherits its
        // ThemeObject, so EU-theme cities never spawn it.
        [SettingsUISection(kSection, kEdgeGroup)]
        [SettingsUIDisableByCondition(typeof(TownRoadLaneSetting), nameof(IsEdgeDisabled))]
        public bool YellowLeftLineEnabled { get; set; } = true;

        // Parallel street-parking markings.

        [SettingsUISection(kSection, kParkingGroup)]
        public bool ParkingMarkingsEnabled { get; set; } = true;

        [SettingsUISection(kSection, kParkingGroup)]
        [SettingsUIDisableByCondition(typeof(TownRoadLaneSetting), nameof(IsParkingDisabled))]
        // Default uses the built-in crisp white dashed style (enum member name kept for .coc
        // compatibility with older TownRoadLane settings files).
        public ParkingLineStyleEnum ParkingLineStyle { get; set; } = ParkingLineStyleEnum.WhiteDashed_G87;

        [SettingsUISection(kSection, kParkingGroup)]
        [SettingsUIDisableByCondition(typeof(TownRoadLaneSetting), nameof(IsParkingDisabled))]
        public ParkingEndStyleEnum ParkingEndStyle { get; set; } = ParkingEndStyleEnum.WhiteSolid;

        // There is deliberately no runtime "reapply" button. Refreshing the clone prefabs
        // (UpdatePrefab) while a world is live leaves existing sublanes with stale PrefabRefs, and
        // the next SecondaryLane rebuild (any road edit, even a bulldozer hover creating Temp
        // roads) crashes natively in a Burst job. Style changes therefore apply after a game
        // restart, when ApplyOrUpdate runs before anything references the clones. Reloading a
        // save is not enough: the clone prefabs live for the whole game process.

        public bool IsEdgeDisabled() => !EdgeLineEnabled;
        public bool IsParkingDisabled() => !ParkingMarkingsEnabled;

        // Per-prefab registration messages at Debug level, for bug reports. Hidden from
        // the options screen; to enable, set "VerboseLogging": true in TownRoadLane.coc and
        // restart the game.
        [SettingsUIHidden]
        public bool VerboseLogging { get; set; } = false;

#if DEBUG
        // Developer prefab dump at boot (RoadPrefabDumpSystem): tens of
        // thousands of log lines per start, enough for Skyve to flag the mod for extreme logging.
        // Debug builds only and hidden from the options screen; to enable, set
        // "DiagnosticDumps": true in TownRoadLane.coc and restart the game.
        [SettingsUIHidden]
        public bool DiagnosticDumps { get; set; } = false;
#endif

        public override void SetDefaults()
        {
            EdgeLineEnabled = true;
            EdgeLineStyle = EdgeLineStyleEnum.WhiteSolid;
            YellowLeftLineEnabled = true;
            ParkingMarkingsEnabled = true;
            ParkingLineStyle = ParkingLineStyleEnum.WhiteDashed_G87;
            ParkingEndStyle = ParkingEndStyleEnum.WhiteSolid;
            VerboseLogging = false;
#if DEBUG
            DiagnosticDumps = false;
#endif
        }

        /// <summary>Resolves the chosen edge-line style to a vanilla or built-in render-prefab name.</summary>
        public string EdgeLineMeshName() => EdgeLineStyle switch
        {
            EdgeLineStyleEnum.WhiteSolid => "White Solid Line Mesh",
            EdgeLineStyleEnum.WhiteSolidThick => "White Solid Line Mesh - Thick",
            EdgeLineStyleEnum.WhiteDashed => "White Dashed Line Mesh",
            EdgeLineStyleEnum.YellowSolid => "Yellow Solid Line Mesh",
            EdgeLineStyleEnum.WhiteSolid_G87 => CustomLineMeshFactory.kCrispWhiteSolid,
            EdgeLineStyleEnum.WhiteDashed_G87 => CustomLineMeshFactory.kCrispWhiteDashed,
            EdgeLineStyleEnum.YellowSolid_G87 => CustomLineMeshFactory.kCrispYellowSolid,
            _ => "White Solid Line Mesh",
        };

        /// <summary>Resolves the chosen longitudinal parking-line style to a vanilla or built-in render-prefab name.</summary>
        public string ParkingLineMeshName() => ParkingLineStyle switch
        {
            ParkingLineStyleEnum.WhiteDashedDense => "White Dashed Line Mesh - Dense",
            ParkingLineStyleEnum.WhiteDashed => "White Dashed Line Mesh",
            ParkingLineStyleEnum.WhiteSolid => "White Solid Line Mesh",
            ParkingLineStyleEnum.YellowDashed => "Yellow Dashed Line Mesh - Long",
            ParkingLineStyleEnum.YellowSolid => "Yellow Solid Line Mesh",
            ParkingLineStyleEnum.WhiteSolid_G87 => CustomLineMeshFactory.kCrispWhiteSolid,
            ParkingLineStyleEnum.WhiteDashed_G87 => CustomLineMeshFactory.kCrispWhiteParkingDashed,
            ParkingLineStyleEnum.YellowSolid_G87 => CustomLineMeshFactory.kCrispYellowSolid,
            ParkingLineStyleEnum.YellowDashed_G87 => CustomLineMeshFactory.kCrispYellowParkingDashed,
            ParkingLineStyleEnum.BlueSolid_G87 => CustomLineMeshFactory.kBlueSolid,
            ParkingLineStyleEnum.BlueDashed_G87 => CustomLineMeshFactory.kBlueParkingDashed,
            _ => "White Dashed Line Mesh - Dense",
        };

        /// <summary>Resolves the chosen end-tick style to a vanilla or built-in render-prefab name; null means no end ticks.</summary>
        public string ParkingEndMeshName() => ParkingEndStyle switch
        {
            ParkingEndStyleEnum.None => null,
            ParkingEndStyleEnum.WhiteSolid => "White Solid Line Mesh",
            ParkingEndStyleEnum.WhiteSolidThick => "White Solid Line Mesh - Thick",
            ParkingEndStyleEnum.WhiteTerminal_G87 => CustomLineMeshFactory.kWhiteTerminal,
            ParkingEndStyleEnum.YellowTerminal_G87 => CustomLineMeshFactory.kYellowTerminal,
            ParkingEndStyleEnum.BlueSolid_G87 => CustomLineMeshFactory.kBlueSolid,
            _ => "White Solid Line Mesh",
        };

        public enum EdgeLineStyleEnum
        {
            WhiteSolid,
            WhiteSolidThick,
            WhiteDashed,
            YellowSolid,
            WhiteSolid_G87,
            WhiteDashed_G87,
            YellowSolid_G87,
        }

        public enum ParkingLineStyleEnum
        {
            WhiteDashedDense,
            WhiteDashed,
            WhiteSolid,
            YellowDashed,
            YellowSolid,
            WhiteSolid_G87,
            WhiteDashed_G87,
            YellowSolid_G87,
            YellowDashed_G87,
            BlueSolid_G87,
            BlueDashed_G87,
        }

        public enum ParkingEndStyleEnum
        {
            None,
            WhiteSolid,
            WhiteSolidThick,
            WhiteTerminal_G87,
            YellowTerminal_G87,
            BlueSolid_G87,
        }
    }
}
