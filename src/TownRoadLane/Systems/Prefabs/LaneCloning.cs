using System.Collections.Generic;
using Colossal.Logging;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Prefab lookups and clone steps shared by <see cref="EdgeLineCloneSystem"/> and
    /// <see cref="ParkingLineCloneSystem"/>: find vanilla lane and mesh prefabs by name, clone
    /// a SecondaryLane marking in memory before registering it once via <see cref="PrefabSystem.AddPrefab"/>,
    /// and swap its mesh on an isolated array copy.
    /// </summary>
    internal static class LaneCloning
    {
        private static readonly ILog log = Mod.log;

        /// <summary>Lane prefabs whose names are in <paramref name="names"/>, keyed by name, from
        /// one pass over <paramref name="lanePrefabQuery"/>. The first prefab with a name wins.</summary>
        public static Dictionary<string, NetLanePrefab> FindLanes(PrefabSystem prefabSystem, EntityQuery lanePrefabQuery, HashSet<string> names)
        {
            var result = new Dictionary<string, NetLanePrefab>();
            var ents = lanePrefabQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                if (!prefabSystem.TryGetPrefab<NetLanePrefab>(ents[i], out var lane) || lane == null) continue;
                if (names.Contains(lane.name) && !result.ContainsKey(lane.name)) result[lane.name] = lane;
            }
            ents.Dispose();
            return result;
        }

        /// <summary>Render prefabs whose names are in <paramref name="names"/> (null or empty
        /// names are ignored), keyed by name, from one pass over <paramref name="meshPrefabQuery"/>.
        /// The first prefab with a name wins.</summary>
        public static Dictionary<string, RenderPrefab> ResolveMeshes(PrefabSystem prefabSystem, EntityQuery meshPrefabQuery, IEnumerable<string> names)
        {
            var wanted = new HashSet<string>();
            foreach (var n in names) if (!string.IsNullOrEmpty(n)) wanted.Add(n);
            var result = new Dictionary<string, RenderPrefab>();
            var ents = meshPrefabQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
                if (prefabSystem.TryGetPrefab<RenderPrefab>(ents[i], out var rp) && rp != null && wanted.Contains(rp.name) && !result.ContainsKey(rp.name))
                    result[rp.name] = rp;
            ents.Dispose();
            return result;
        }

        /// <summary>The <paramref name="names"/> found in <paramref name="byName"/>, in order;
        /// missing ones are logged and skipped.</summary>
        public static List<NetLanePrefab> ResolveLanes(Dictionary<string, NetLanePrefab> byName, string[] names, string what)
        {
            var list = new List<NetLanePrefab>();
            foreach (var n in names)
                if (byName.TryGetValue(n, out var p) && p != null) list.Add(p);
                else log.Warn($"{what} '{n}' not found — skipping it");
            return list;
        }

        /// <summary>The wanted mesh, else the fallback, else null (the clone keeps its source
        /// prefab's mesh).</summary>
        public static RenderPrefab PickMesh(Dictionary<string, RenderPrefab> byName, string wanted, string fallback, string what)
        {
            if (!string.IsNullOrEmpty(wanted) && byName.TryGetValue(wanted, out var rp) && rp != null) return rp;
            if (byName.TryGetValue(fallback, out var fb) && fb != null)
            { log.Warn($"{what} mesh '{wanted}' not found — falling back to '{fallback}'"); return fb; }
            log.Warn($"{what} mesh '{wanted}' and fallback '{fallback}' both missing — keeping source mesh");
            return null;
        }

        /// <summary>
        /// Returns an already-registered clone named <paramref name="cloneName"/> (<paramref name="isNew"/>
        /// = false), or creates an unregistered in-memory clone via <see cref="PrefabBase.Clone"/>
        /// (<paramref name="isNew"/> = true) so the caller can configure its <see cref="SecondaryLane"/>
        /// and meshes BEFORE registering it once with <see cref="PrefabSystem.AddPrefab"/>.
        ///
        /// Avoids <see cref="PrefabSystem.DuplicatePrefab"/> + <see cref="PrefabSystem.UpdatePrefab"/>,
        /// which registers the source's original hosting first and then forces a re-init that can
        /// produce duplicate <see cref="SecondaryNetLane"/> entries and native crashes when combined
        /// with Road Builder's dynamic prefab updates.
        /// </summary>
        public static bool TryPrepareClone(
            PrefabSystem prefabSystem, Dictionary<string, NetLanePrefab> laneByName, string sourceName, string cloneName,
            out NetLanePrefab clone, out SecondaryLane secondaryLane, out bool isNew)
        {
            secondaryLane = null;
            isNew = false;

            if (laneByName.TryGetValue(cloneName, out clone) && clone != null)
            {
                if (!clone.TryGet(out secondaryLane)) { log.Warn($"'{cloneName}' has no SecondaryLane — skipping"); return false; }
                return true;
            }

            if (prefabSystem.TryGetPrefab(new PrefabID(nameof(NetLaneGeometryPrefab), cloneName), out var existingBase)
                && existingBase is NetLanePrefab existingLane)
            {
                clone = existingLane;
                laneByName[cloneName] = clone;
                if (!clone.TryGet(out secondaryLane)) { log.Warn($"'{cloneName}' has no SecondaryLane — skipping"); return false; }
                return true;
            }

            if (!laneByName.TryGetValue(sourceName, out var src) || src is not NetLaneGeometryPrefab || !src.TryGet<SecondaryLane>(out _))
            {
                log.Warn($"source '{sourceName}' missing/invalid — can't create '{cloneName}'");
                clone = null;
                return false;
            }

            clone = src.Clone(cloneName) as NetLanePrefab;
            if (clone == null || !clone.TryGet(out secondaryLane))
            {
                log.Warn($"cloned '{cloneName}' has no SecondaryLane — skipping");
                clone = null;
                return false;
            }

            laneByName[cloneName] = clone;
            isNew = true;
            return true;
        }

        /// <summary>Copies the prefab's mesh array (so the source prefab's array is never mutated)
        /// and puts <paramref name="mesh"/> into every populated mesh slot; returns how many slots
        /// changed. A null mesh still isolates the array copy without changing its entries.</summary>
        public static int SwapMesh(NetLanePrefab prefab, RenderPrefab mesh)
        {
            if (prefab is not NetLaneGeometryPrefab g || g.m_Meshes == null) return 0;
            var copy = new NetLaneMeshInfo[g.m_Meshes.Length];
            int n = 0;
            for (int m = 0; m < g.m_Meshes.Length; m++)
            {
                copy[m] = g.m_Meshes[m];
                if (mesh != null && copy[m].m_Mesh != null)
                {
                    copy[m].m_Mesh = mesh;
                    n++;
                }
            }
            g.m_Meshes = copy;
            return n;
        }

        /// <summary>Drops null lane entries and duplicate (lane, flag-mask) entries from a
        /// SecondaryLaneInfo list so NetInitializeSystem never receives duplicate hostings.</summary>
        public static SecondaryLaneInfo[] DeduplicateLaneInfos(IReadOnlyList<SecondaryLaneInfo> infos)
        {
            if (infos == null || infos.Count == 0) return System.Array.Empty<SecondaryLaneInfo>();
            var list = new List<SecondaryLaneInfo>(infos.Count);
            var seen = new HashSet<(NetLanePrefab, int)>();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (info == null || info.m_Lane == null) continue;
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
                if (!seen.Add((info.m_Lane, mask))) continue;
                list.Add(info);
            }
            return list.ToArray();
        }
    }
}
