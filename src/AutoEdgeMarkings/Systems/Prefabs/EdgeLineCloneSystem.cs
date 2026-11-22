using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;

namespace AutoEdgeMarkings.Systems.Prefabs
{
    /// <summary>
    /// Auto edge line: ordinary city roads (3 m car lanes) get the curb-side edge line that
    /// highways already have. A clone of 'EU/NA Highway Edge Line' lists the city lanes in
    /// m_LeftLanes, and NetInitializeSystem indexes those entries onto the lane prefabs'
    /// SecondaryNetLane buffers, so every road using these lanes (Road Builder roads included)
    /// gets the line. Cloning instead of editing the vanilla prefab lets the mesh be swapped while
    /// the vanilla highway line stays untouched. Also adds the US-style yellow left edge line.
    /// </summary>
    public partial class EdgeLineCloneSystem : LaneCloneSystemBase
    {
        private const string kNAHighwayEdgeLine = "NA Highway Edge Line";
        private const string kWhiteFallback = "White Solid Line Mesh";
        private const string kYellowMesh = "Yellow Solid Line Mesh";

        private struct Recipe
        {
            public bool isNA;
            public string sourcePrefabName;
            public string cloneName;
            // False: the curb-side edge line, hosted on the city lanes in m_LeftLanes with its
            // mesh from the "Edge line style" setting. True: the US-style yellow left edge line,
            // hosted in m_RightLanes only (the lane's left edge) with canFlipSides=false, while
            // both EdgeLineEnabled and YellowLeftLineEnabled are on.
            public bool yellowLeft;
        }

        private static readonly Recipe[] kRecipes =
        {
            new() { isNA = false, sourcePrefabName = "EU Highway Edge Line", cloneName = "AEM EU Edge Line" },
            new() { isNA = true,  sourcePrefabName = kNAHighwayEdgeLine, cloneName = "AEM NA Edge Line" },
            // NA source only: the clone inherits the NA ThemeObject, so theme requirements keep it
            // out of EU cities. The white NA clone above stops mirroring while
            // YellowLeftLineEnabled is on.
            new() { isNA = true,  sourcePrefabName = kNAHighwayEdgeLine, cloneName = "AEM NA Yellow Left Line", yellowLeft = true },
        };

        protected override IEnumerable<string> RequiredLanes =>
            new List<string>(kCityLaneNames) { "EU Highway Edge Line", kNAHighwayEdgeLine };
        protected override IEnumerable<string> RequiredMeshes => new[] { kWhiteFallback, kYellowMesh };
        protected override IEnumerable<string> StyleMeshes => new[] { Mod.Settings.EdgeLineMeshName() };

        protected override void Apply(Dictionary<string, NetLanePrefab> lanes, Dictionary<string, RenderPrefab> meshes)
        {
            var settings = Mod.Settings;
            bool autoEdgeOn = settings.EdgeLineEnabled;
            bool yellowLeftOn = autoEdgeOn && settings.YellowLeftLineEnabled;
            RenderPrefab edgeMesh = PickMesh(meshes, settings.EdgeLineMeshName(), kWhiteFallback);

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
            var naVanillaEdge = lanes[kNAHighwayEdgeLine];
            if (yellowLeftOn && naVanillaEdge.TryGet<SecondaryLane>(out var naVanillaSec) && naVanillaSec.m_LeftLanes != null)
            {
                highwayYellowInfos = DeduplicateLaneInfos(naVanillaSec.m_LeftLanes);
                // Before vanilla init (same tick) this in-memory flag is enough and stripped stays 0;
                // the buffer strip below covers host buffers that were already built.
                naVanillaSec.m_CanFlipSides = false;
                Entity naEdgeEnt = _prefabSystem.GetEntity(naVanillaEdge);
                int stripped = 0;
                foreach (var info in highwayYellowInfos)
                {
                    Entity hostEnt = _prefabSystem.GetEntity(info.m_Lane);
                    if (!EntityManager.HasBuffer<SecondaryNetLane>(hostEnt)) continue;
                    var hostBuf = EntityManager.GetBuffer<SecondaryNetLane>(hostEnt);
                    for (int i = 0; i < hostBuf.Length; i++)
                    {
                        var entry = hostBuf[i];
                        if (entry.m_Lane != naEdgeEnt || (entry.m_Flags & SecondaryNetLaneFlags.CanFlipSides) == 0) continue;
                        entry.m_Flags &= ~SecondaryNetLaneFlags.CanFlipSides;
                        hostBuf[i] = entry;
                        stripped++;
                    }
                }
                log.Info($"yellow-left: unmirrored vanilla '{kNAHighwayEdgeLine}' — CanFlipSides stripped from {stripped} host entries, {highwayYellowInfos.Length} entries mirrored to the yellow clone");
            }

            foreach (var recipe in kRecipes)
            {
                var clone = CreateClone(lanes, recipe.sourcePrefabName, recipe.cloneName, out var sec);

                // Clear all hosting BEFORE AddPrefab: Clone() copies the source's hosting, so
                // NetInitializeSystem would otherwise index the vanilla hostings onto the clone.
                // With its option off, a clone hosts nothing, so vanilla never draws it.
                sec.m_LeftLanes = Array.Empty<SecondaryLaneInfo>();
                sec.m_RightLanes = Array.Empty<SecondaryLaneInfo>();
                sec.m_CrossingLanes = Array.Empty<SecondaryLaneInfo2>();
                sec.m_CanFlipSides = false;

                if (!recipe.yellowLeft && autoEdgeOn)
                {
                    // Sides: m_LeftLanes lists the host lanes lying to the left of the line, so
                    // the line renders on the lane's right edge, and vice versa.
                    sec.m_LeftLanes = MakeCityLaneInfos(lanes);
                    sec.m_CanFlipSides = !(yellowLeftOn && recipe.isNA);
                }
                else if (recipe.yellowLeft && yellowLeftOn)
                {
                    // m_RightLanes: the line renders on the lane's left (median) edge. City lanes
                    // plus the highway entries copied from the vanilla NA edge line.
                    var combined = new List<SecondaryLaneInfo>(MakeCityLaneInfos(lanes));
                    combined.AddRange(highwayYellowInfos);
                    sec.m_RightLanes = DeduplicateLaneInfos(combined);
                }

                Register(clone, recipe.yellowLeft ? meshes[kYellowMesh] : edgeMesh);
            }

            log.Info($"EdgeLineCloneSystem: registered {kRecipes.Length} prefab(s) (enabled={autoEdgeOn}, yellowLeft={yellowLeftOn}, mesh='{edgeMesh.name}')");
        }

        /// <summary>
        /// Two SecondaryLaneInfo entries per city lane, matching what vanilla uses for 'Highway Drive Lane 3':
        ///   - { RequireSafe } draws the edge line on straight segments.
        ///   - { RequireMerge, RequireSafeMaster } continues the line through merges (onramps, width
        ///     transitions) on the master side of the merge.
        /// Without the second entry the line would stop at every merge point.
        /// </summary>
        private static SecondaryLaneInfo[] MakeCityLaneInfos(Dictionary<string, NetLanePrefab> lanes)
        {
            var arr = new SecondaryLaneInfo[kCityLaneNames.Length * 2];
            for (int i = 0; i < kCityLaneNames.Length; i++)
            {
                var lane = lanes[kCityLaneNames[i]];
                arr[i * 2] = new SecondaryLaneInfo { m_Lane = lane, m_RequireSafe = true };
                arr[i * 2 + 1] = new SecondaryLaneInfo { m_Lane = lane, m_RequireMerge = true, m_RequireSafeMaster = true };
            }
            return arr;
        }

        /// <summary>Drops null-lane and duplicate (lane, flag-mask) entries so NetInitializeSystem
        /// never receives duplicate hostings.</summary>
        private static SecondaryLaneInfo[] DeduplicateLaneInfos(IReadOnlyList<SecondaryLaneInfo> infos)
        {
            var list = new List<SecondaryLaneInfo>(infos.Count);
            var seen = new HashSet<(NetLanePrefab, int)>();
            foreach (var info in infos)
            {
                if (info?.m_Lane == null) continue;
                int mask = (info.m_RequireSafe ? 1 << 0 : 0)
                         | (info.m_RequireUnsafe ? 1 << 1 : 0)
                         | (info.m_RequireSingle ? 1 << 2 : 0)
                         | (info.m_RequireMultiple ? 1 << 3 : 0)
                         | (info.m_RequireAllowPassing ? 1 << 4 : 0)
                         | (info.m_RequireForbidPassing ? 1 << 5 : 0)
                         | (info.m_RequireMerge ? 1 << 6 : 0)
                         | (info.m_RequireContinue ? 1 << 7 : 0)
                         | (info.m_RequireSafeMaster ? 1 << 8 : 0)
                         | (info.m_RequireRoundabout ? 1 << 9 : 0)
                         | (info.m_RequireNotRoundabout ? 1 << 10 : 0);
                if (seen.Add((info.m_Lane, mask))) list.Add(info);
            }
            return list.ToArray();
        }
    }
}
