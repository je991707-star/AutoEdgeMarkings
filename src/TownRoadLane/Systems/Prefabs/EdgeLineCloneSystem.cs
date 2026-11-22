using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Unity.Entities;
using TownRoadLane.Components;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Clones vanilla marking prefabs for two purposes.
    ///
    /// Auto edge line: ordinary city roads (3 m car lanes) get the curb-side edge line that
    /// highways already have. A clone of 'EU/NA Highway Edge Line' lists the city lanes in
    /// m_LeftLanes, and NetInitializeSystem indexes those entries onto the lane prefabs'
    /// SecondaryNetLane buffers, so every road using these lanes (Road Builder roads included)
    /// gets the line. Cloning instead of editing the vanilla prefab lets the mesh be swapped while
    /// the vanilla highway line stays untouched.
    ///
    /// Tool styles: one EU and one NA clone per <see cref="MarkingStyle"/>, used as the prefab for
    /// the sublanes that the marking tool spawns; see <see cref="GetCloneEntity(MarkingStyle, bool)"/>.
    /// Each source must be a vanilla NetLaneGeometryPrefab with SecondaryLane.
    /// </summary>
    public partial class EdgeLineCloneSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        // City lanes that get the edge line. 'Car Drive Lane 3' uses the same 'Car Lane 3 Mesh' as
        // 'Highway Drive Lane 3', so the edge line fits without offsets; the tram and public transport
        // variants have the same 3 m width.
        private static readonly string[] kCityLaneNames =
        {
            "Car Drive Lane 3",
            "Car Drive Lane 3 - Tram",
            "Public Transport Lane 3",
            "Public Transport Lane 3 - Tram",
        };

        // To add a style: append it to MarkingStyle and add an EU and an NA row to kStyleRecipes.
        private struct StyleRecipe
        {
            public MarkingStyle style;
            public bool isNA;
            public string sourcePrefabName;
            public string cloneName;
            public string fallbackMesh;
            // True for the auto edge line: hosted on the city lanes, mesh from the "Edge line style"
            // setting, not registered as a tool style. False for tool styles: the clone is only a
            // prefab for spawned sublanes and must host nothing, otherwise the vanilla secondary
            // lane pass draws it on every city road (the dashed styles are cloned from the vanilla
            // lane divider 'Car Lane Line').
            public bool hostOnCityLanes;
            // True for the US-style yellow left edge line: hosted on the city lanes in m_RightLanes
            // only (the lane's left edge, see the side note in ApplyOrUpdate) with
            // canFlipSides=false, while both EdgeLineEnabled and YellowLeftLineEnabled are on.
            // Mutually exclusive with hostOnCityLanes.
            public bool hostYellowLeft;
        }

        // Built-in procedural line/curb RenderPrefab names (created by CustomLineMeshFactory with
        // zero external EAI/G87 dependency).
        private const string kCrispSolidMesh = CustomLineMeshFactory.kCrispWhiteSolid;
        private const string kCrispDashedMesh = CustomLineMeshFactory.kCrispWhiteDashed;
        private const string kCrispYellowMesh = CustomLineMeshFactory.kCrispYellowSolid;
        private const string kCrispYellowDashedMesh = CustomLineMeshFactory.kCrispYellowDashed;
        private const string kCurbMesh = CustomLineMeshFactory.kCurb;

        private static readonly StyleRecipe[] kStyleRecipes =
        {
            // Auto edge line, the only clones hosted on city lanes. Their mesh follows the "Edge
            // line style" setting, so they are kept apart from the tool's Solid clones: a shared
            // clone would turn the tool's Solid lines yellow along with a yellow edge style.
            new() { style = MarkingStyle.Solid,     isNA = false, sourcePrefabName = "EU Highway Edge Line", cloneName = "TownRoadLane EU Auto Edge Line",       fallbackMesh = "White Solid Line Mesh",  hostOnCityLanes = true  },
            new() { style = MarkingStyle.Solid,     isNA = true,  sourcePrefabName = "NA Highway Edge Line", cloneName = "TownRoadLane NA Auto Edge Line",       fallbackMesh = "White Solid Line Mesh",  hostOnCityLanes = true  },
            // US-style yellow left edge line. NA source only: the clone inherits the NA
            // ThemeObject, so theme requirements keep it out of EU cities. Renders on the lane's
            // left (median) edge; the white NA clone above stops mirroring while
            // YellowLeftLineEnabled is on.
            new() { style = MarkingStyle.YellowSolid, isNA = true, sourcePrefabName = "NA Highway Edge Line", cloneName = "TownRoadLane NA Auto Yellow Left Line", fallbackMesh = "Yellow Solid Line Mesh", hostYellowLeft = true },
            // Tool styles. Clone names are saved with the spawned sublanes, so they must never
            // change. Solid is always white, whatever the edge line settings.
            new() { style = MarkingStyle.Solid,     isNA = false, sourcePrefabName = "EU Highway Edge Line", cloneName = "TownRoadLane EU City Edge Line",       fallbackMesh = "White Solid Line Mesh",  hostOnCityLanes = false },
            new() { style = MarkingStyle.Solid,     isNA = true,  sourcePrefabName = "NA Highway Edge Line", cloneName = "TownRoadLane NA City Edge Line",       fallbackMesh = "White Solid Line Mesh",  hostOnCityLanes = false },
            new() { style = MarkingStyle.Dashed,    isNA = false, sourcePrefabName = "EU Car Lane Line",     cloneName = "TownRoadLane EU City Dashed Line",     fallbackMesh = "White Dashed Line Mesh", hostOnCityLanes = false },
            new() { style = MarkingStyle.Dashed,    isNA = true,  sourcePrefabName = "NA Car Lane Line",     cloneName = "TownRoadLane NA City Dashed Line",     fallbackMesh = "White Dashed Line Mesh", hostOnCityLanes = false },
            // Built-in crisp high-contrast styles (replaces external G87 decals; clone names stay
            // unchanged for save-game compatibility).
            new() { style = MarkingStyle.G87Solid,  isNA = false, sourcePrefabName = "EU Car Bay Line", cloneName = "TownRoadLane EU City G87 Solid Line",  fallbackMesh = kCrispSolidMesh,  hostOnCityLanes = false },
            new() { style = MarkingStyle.G87Solid,  isNA = true,  sourcePrefabName = "NA Car Bay Line", cloneName = "TownRoadLane NA City G87 Solid Line",  fallbackMesh = kCrispSolidMesh,  hostOnCityLanes = false },
            new() { style = MarkingStyle.G87Dashed, isNA = false, sourcePrefabName = "EU Car Bay Line", cloneName = "TownRoadLane EU City G87 Dashed Line", fallbackMesh = kCrispDashedMesh, hostOnCityLanes = false },
            new() { style = MarkingStyle.G87Dashed, isNA = true,  sourcePrefabName = "NA Car Bay Line", cloneName = "TownRoadLane NA City G87 Dashed Line", fallbackMesh = kCrispDashedMesh, hostOnCityLanes = false },
            // Double lines come from a single vanilla mesh, not from two sublanes.
            new() { style = MarkingStyle.DoubleSolid, isNA = false, sourcePrefabName = "EU Car Bay Line", cloneName = "TownRoadLane EU City Double Solid Line", fallbackMesh = "White Double Solid Line Mesh", hostOnCityLanes = false },
            new() { style = MarkingStyle.DoubleSolid, isNA = true,  sourcePrefabName = "NA Car Bay Line", cloneName = "TownRoadLane NA City Double Solid Line", fallbackMesh = "White Double Solid Line Mesh", hostOnCityLanes = false },
            // '- Dense' and '- Long' are vanilla dashed mesh variants. If a game patch renames one,
            // the clone keeps the source's regular dashed mesh.
            new() { style = MarkingStyle.DashedDense,     isNA = false, sourcePrefabName = "EU Car Lane Line", cloneName = "TownRoadLane EU City Dashed Dense Line",     fallbackMesh = "White Dashed Line Mesh - Dense", hostOnCityLanes = false },
            new() { style = MarkingStyle.DashedDense,     isNA = true,  sourcePrefabName = "NA Car Lane Line", cloneName = "TownRoadLane NA City Dashed Dense Line",     fallbackMesh = "White Dashed Line Mesh - Dense", hostOnCityLanes = false },
            new() { style = MarkingStyle.G87Yellow,       isNA = false, sourcePrefabName = "EU Car Bay Line",  cloneName = "TownRoadLane EU City G87 Yellow Line",        fallbackMesh = kCrispYellowMesh,       hostOnCityLanes = false },
            new() { style = MarkingStyle.G87Yellow,       isNA = true,  sourcePrefabName = "NA Car Bay Line",  cloneName = "TownRoadLane NA City G87 Yellow Line",        fallbackMesh = kCrispYellowMesh,       hostOnCityLanes = false },
            new() { style = MarkingStyle.G87YellowDashed, isNA = false, sourcePrefabName = "EU Car Bay Line",  cloneName = "TownRoadLane EU City G87 Yellow Dashed Line", fallbackMesh = kCrispYellowDashedMesh, hostOnCityLanes = false },
            new() { style = MarkingStyle.G87YellowDashed, isNA = true,  sourcePrefabName = "NA Car Bay Line",  cloneName = "TownRoadLane NA City G87 Yellow Dashed Line", fallbackMesh = kCrispYellowDashedMesh, hostOnCityLanes = false },
            new() { style = MarkingStyle.DashedLong,      isNA = false, sourcePrefabName = "EU Car Lane Line", cloneName = "TownRoadLane EU City Dashed Long Line",       fallbackMesh = "White Dashed Line Mesh - Long", hostOnCityLanes = false },
            new() { style = MarkingStyle.DashedLong,      isNA = true,  sourcePrefabName = "NA Car Lane Line", cloneName = "TownRoadLane NA City Dashed Long Line",       fallbackMesh = "White Dashed Line Mesh - Long", hostOnCityLanes = false },
            // Built-in flat curb texture for island and median edges (zero external pack needed).
            new() { style = MarkingStyle.Curb,            isNA = false, sourcePrefabName = "EU Car Bay Line",  cloneName = "TownRoadLane EU City Curb Line",              fallbackMesh = kCurbMesh, hostOnCityLanes = false },
            new() { style = MarkingStyle.Curb,            isNA = true,  sourcePrefabName = "NA Car Bay Line",  cloneName = "TownRoadLane NA City Curb Line",              fallbackMesh = kCurbMesh, hostOnCityLanes = false },
            // Vanilla yellow styles: same source prefabs as their white counterparts.
            new() { style = MarkingStyle.YellowSolid,       isNA = false, sourcePrefabName = "EU Highway Edge Line", cloneName = "TownRoadLane EU City Yellow Solid Line",        fallbackMesh = "Yellow Solid Line Mesh",               hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowSolid,       isNA = true,  sourcePrefabName = "NA Highway Edge Line", cloneName = "TownRoadLane NA City Yellow Solid Line",        fallbackMesh = "Yellow Solid Line Mesh",               hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowDashed,      isNA = false, sourcePrefabName = "EU Car Lane Line",     cloneName = "TownRoadLane EU City Yellow Dashed Line",       fallbackMesh = "Yellow Dashed Line Mesh - Long",       hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowDashed,      isNA = true,  sourcePrefabName = "NA Car Lane Line",     cloneName = "TownRoadLane NA City Yellow Dashed Line",       fallbackMesh = "Yellow Dashed Line Mesh - Long",       hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowDoubleSolid, isNA = false, sourcePrefabName = "EU Car Bay Line",      cloneName = "TownRoadLane EU City Yellow Double Solid Line", fallbackMesh = "Yellow Double Solid Line Mesh",        hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowDoubleSolid, isNA = true,  sourcePrefabName = "NA Car Bay Line",      cloneName = "TownRoadLane NA City Yellow Double Solid Line", fallbackMesh = "Yellow Double Solid Line Mesh",        hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowSolidDashed, isNA = false, sourcePrefabName = "EU Car Bay Line",      cloneName = "TownRoadLane EU City Yellow Solid Dashed Line", fallbackMesh = "Yellow Solid Dashed Line Mesh - Long", hostOnCityLanes = false },
            new() { style = MarkingStyle.YellowSolidDashed, isNA = true,  sourcePrefabName = "NA Car Bay Line",      cloneName = "TownRoadLane NA City Yellow Solid Dashed Line", fallbackMesh = "Yellow Solid Dashed Line Mesh - Long", hostOnCityLanes = false },
        };

        private PrefabSystem _prefabSystem;
        private EntityQuery _lanePrefabQuery;
        private EntityQuery _meshPrefabQuery;
        private bool _done;
        private int _waitTicks;
        private bool _waitWarned;
        private const int kWaitWarnTicks = 300;

        // Tool-style clones per (style, isNA). Registered once via AddPrefab; entities are
        // resolved through PrefabSystem so lookups stay valid across world reloads.
        private readonly Dictionary<(MarkingStyle, bool), NetLanePrefab> _clonesByStyle = new();

        /// <summary>Current entity of the clone for this style and theme, or Entity.Null while it
        /// is not loaded (callers fall back to Solid).</summary>
        public Entity GetCloneEntity(MarkingStyle style, bool isNA)
        {
            if (_prefabSystem == null) return Entity.Null;
            return _clonesByStyle.TryGetValue((style, isNA), out var pb) && pb != null
                ? _prefabSystem.GetEntity(pb)
                : Entity.Null;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _lanePrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<NetLaneData>());
            _meshPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<MeshData>());
            RequireForUpdate(_lanePrefabQuery);
        }

        // Clones are configured in memory and registered once via PrefabSystem.AddPrefab (never
        // UpdatePrefab on a live world). Runs until all required vanilla sources are present, then
        // disables itself for the rest of the process.
        protected override void OnUpdate()
        {
            if (_done) return;
            try
            {
                if (ApplyOrUpdate())
                {
                    _done = true;
                    Enabled = false;
                }
                else if (++_waitTicks >= kWaitWarnTicks && !_waitWarned)
                {
                    _waitWarned = true;
                    log.Warn($"EdgeLineCloneSystem: still waiting for vanilla host lanes/sources after {kWaitWarnTicks} PrefabUpdate ticks");
                }
            }
            catch (Exception e)
            {
                _done = true;
                Enabled = false;
                log.Error(e, "EdgeLineCloneSystem failed");
            }
        }

        /// <summary>
        /// Creates (or caches already-registered) clones in <see cref="kStyleRecipes"/>.
        /// Returns false if required vanilla sources have not finished initializing yet.
        /// </summary>
        public bool ApplyOrUpdate()
        {
            // The mesh setting applies only to the hosted auto edge clones; tool styles always use
            // their recipe mesh.
            string edgeMeshName = Mod.Settings?.EdgeLineMeshName() ?? "White Solid Line Mesh";
            bool autoEdgeOn = Mod.Settings == null || Mod.Settings.EdgeLineEnabled;
            bool yellowLeftOn = autoEdgeOn && (Mod.Settings == null || Mod.Settings.YellowLeftLineEnabled);

            // Resolve every prefab we need by name in one pass over NetLanePrefab entities.
            var wantedLanes = new HashSet<string>(kCityLaneNames);
            foreach (var r in kStyleRecipes) { wantedLanes.Add(r.sourcePrefabName); wantedLanes.Add(r.cloneName); }
            var laneByName = LaneCloning.FindLanes(_prefabSystem, _lanePrefabQuery, wantedLanes);

            var cityLanes = LaneCloning.ResolveLanes(laneByName, kCityLaneNames, "city host lane");
            if (cityLanes.Count == 0) return false;

            // Wait until core vanilla marking sources are loaded so we never lock in a partial set.
            foreach (var r in kStyleRecipes)
            {
                if (!laneByName.ContainsKey(r.sourcePrefabName) && !laneByName.ContainsKey(r.cloneName))
                    return false;
            }

            // Resolve every mesh that might be needed in one query pass, then ensure our built-in
            // procedural line/curb RenderPrefabs are registered before cloning.
            var meshNames = new HashSet<string>(CustomLineMeshFactory.RequiredVanillaSources) { edgeMeshName };
            foreach (var r in kStyleRecipes) meshNames.Add(r.fallbackMesh);
            var meshByName = LaneCloning.ResolveMeshes(_prefabSystem, _meshPrefabQuery, meshNames);
            foreach (var req in CustomLineMeshFactory.RequiredVanillaSources)
                if (!meshByName.ContainsKey(req)) return false;

            CustomLineMeshFactory.EnsureCustomLineMeshes(_prefabSystem, meshByName);

            // Yellow left line on highways. Vanilla 'NA Highway Edge Line' hosts the highway lanes
            // in m_LeftLanes with canFlipSides=true, so it draws white on both edges. While the
            // option is on, its mirroring is switched off (white stays on the curb side) and its
            // host entries are copied (deduplicated) to the yellow clone's m_RightLanes, so
            // highways get the yellow median edge with the lanes and flags vanilla uses. The EU
            // prefab is untouched.
            //
            // The vanilla prefab never goes through UpdatePrefab: only the CanFlipSides bit is
            // cleared in the already-built host buffer entries, preventing duplicate SecondaryNetLane
            // entries that would crash in Modification4B barrier playback.
            SecondaryLaneInfo[] highwayYellowInfos = Array.Empty<SecondaryLaneInfo>();
            if (yellowLeftOn
                && laneByName.TryGetValue("NA Highway Edge Line", out var naVanillaEdge) && naVanillaEdge != null
                && naVanillaEdge.TryGet<SecondaryLane>(out var naVanillaSec) && naVanillaSec.m_LeftLanes != null)
            {
                highwayYellowInfos = LaneCloning.DeduplicateLaneInfos(naVanillaSec.m_LeftLanes);
                naVanillaSec.m_CanFlipSides = false;
                Entity naEdgeEnt = _prefabSystem.GetEntity(naVanillaEdge);
                int stripped = 0;
                if (naEdgeEnt != Entity.Null)
                {
                    foreach (var info in highwayYellowInfos)
                    {
                        if (info.m_Lane == null) continue;
                        Entity hostEnt = _prefabSystem.GetEntity(info.m_Lane);
                        if (hostEnt == Entity.Null || !EntityManager.Exists(hostEnt) || !EntityManager.HasBuffer<SecondaryNetLane>(hostEnt)) continue;
                        var hostBuf = EntityManager.GetBuffer<SecondaryNetLane>(hostEnt);
                        for (int i = 0; i < hostBuf.Length; i++)
                        {
                            var entry = hostBuf[i];
                            if (entry.m_Lane != naEdgeEnt) continue;
                            if ((entry.m_Flags & SecondaryNetLaneFlags.CanFlipSides) == 0) continue;
                            entry.m_Flags &= ~SecondaryNetLaneFlags.CanFlipSides;
                            hostBuf[i] = entry;
                            stripped++;
                        }
                    }
                }
                log.Info($"yellow-left: unmirrored vanilla 'NA Highway Edge Line' — CanFlipSides stripped from {stripped} host entries, {highwayYellowInfos.Length} entries mirrored to the yellow clone");
            }

            int created = 0;
            int reused = 0;
            foreach (var recipe in kStyleRecipes)
            {
                if (!LaneCloning.TryPrepareClone(_prefabSystem, laneByName, recipe.sourcePrefabName, recipe.cloneName, out var cloneBase, out var sec, out bool isNew))
                    continue;

                if (isNew)
                {
                    string wantedMesh = recipe.hostOnCityLanes ? edgeMeshName : recipe.fallbackMesh;
                    RenderPrefab mesh = LaneCloning.PickMesh(meshByName, wantedMesh, recipe.fallbackMesh, recipe.cloneName);

                    // Clear all hosting BEFORE AddPrefab: Clone() copies the source's hosting, so
                    // clearing it prior to registration ensures NetInitializeSystem never indexes
                    // unwanted vanilla hostings onto the clone.
                    sec.m_LeftLanes = Array.Empty<SecondaryLaneInfo>();
                    sec.m_RightLanes = Array.Empty<SecondaryLaneInfo>();
                    sec.m_CrossingLanes = Array.Empty<SecondaryLaneInfo2>();
                    sec.m_CanFlipSides = false;

                    int hostCount = 0;
                    if (recipe.hostOnCityLanes && autoEdgeOn)
                    {
                        // Sides: m_LeftLanes lists the host lanes lying to the left of the line, so
                        // the line renders on the lane's right edge, and vice versa.
                        sec.m_LeftLanes = MakeCityLaneInfos(cityLanes);
                        sec.m_CanFlipSides = !(yellowLeftOn && recipe.isNA);
                        hostCount = sec.m_LeftLanes.Length;
                    }
                    else if (recipe.hostYellowLeft && yellowLeftOn)
                    {
                        // Hosted in m_RightLanes, so the line renders on the lane's left (median)
                        // edge. City lanes plus the deduplicated highway entries copied from the
                        // vanilla NA edge line.
                        var combined = new List<SecondaryLaneInfo>(cityLanes.Count * 2 + highwayYellowInfos.Length);
                        combined.AddRange(MakeCityLaneInfos(cityLanes));
                        combined.AddRange(highwayYellowInfos);
                        sec.m_RightLanes = LaneCloning.DeduplicateLaneInfos(combined);
                        sec.m_CanFlipSides = false;
                        hostCount = sec.m_RightLanes.Length;
                    }
                    // Tool-style clones host nothing, so vanilla SecondaryLaneSystem never draws them.

                    int swapped = LaneCloning.SwapMesh(cloneBase, mesh);
                    _prefabSystem.AddPrefab(cloneBase);
                    created++;
                    log.Debug($"registered '{recipe.cloneName}' [{recipe.style}/{(recipe.isNA ? "NA" : "EU")}]: hostedEntries={hostCount} mesh='{(mesh != null ? mesh.name : "<source>")}' swapped={swapped}");
                }
                else
                {
                    reused++;
                }

                // Hosted clones are not tool styles and would collide with the tool's entry under
                // the same (style, isNA) key.
                if (!recipe.hostOnCityLanes && !recipe.hostYellowLeft) _clonesByStyle[(recipe.style, recipe.isNA)] = cloneBase;
            }

            log.Info($"EdgeLineCloneSystem: registered {created} new prefab(s), reused {reused}");
            return true;
        }

        /// <summary>
        /// Two SecondaryLaneInfo entries per city lane, matching what vanilla uses for 'Highway Drive Lane 3':
        ///   - { RequireSafe } draws the edge line on straight segments.
        ///   - { RequireMerge, RequireSafeMaster } continues the line through merges (onramps, width
        ///     transitions) on the master side of the merge.
        /// Without the second entry the line would stop at every merge point.
        /// </summary>
        private static SecondaryLaneInfo[] MakeCityLaneInfos(IReadOnlyList<NetLanePrefab> lanes)
        {
            var arr = new SecondaryLaneInfo[lanes.Count * 2];
            for (int i = 0; i < lanes.Count; i++)
            {
                arr[i * 2] = new SecondaryLaneInfo { m_Lane = lanes[i], m_RequireSafe = true };
                arr[i * 2 + 1] = new SecondaryLaneInfo { m_Lane = lanes[i], m_RequireMerge = true, m_RequireSafeMaster = true };
            }
            return arr;
        }
    }
}
