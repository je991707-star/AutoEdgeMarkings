using System;
using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace AutoEdgeMarkings.Systems.Prefabs
{
    /// <summary>
    /// Shared loop of <see cref="EdgeLineCloneSystem"/> and <see cref="ParkingLineCloneSystem"/>:
    /// each PrefabUpdate tick it looks up the required vanilla lane and mesh prefabs by name; once
    /// all are loaded it builds the built-in meshes, lets the subclass clone and configure its
    /// markings, registers them once via <see cref="PrefabSystem.AddPrefab"/> and disables itself
    /// for the rest of the process (the clones are never refreshed with UpdatePrefab).
    /// </summary>
    public abstract partial class LaneCloneSystemBase : GameSystemBase
    {
        protected static readonly ILog log = Mod.log;

        // City lanes that host the edge and parking lines. 'Car Drive Lane 3' uses the same
        // 'Car Lane 3 Mesh' as 'Highway Drive Lane 3', so the edge line fits without offsets; the
        // tram and public transport variants have the same 3 m width.
        protected static readonly string[] kCityLaneNames =
        {
            "Car Drive Lane 3",
            "Car Drive Lane 3 - Tram",
            "Public Transport Lane 3",
            "Public Transport Lane 3 - Tram",
        };

        private const int kWaitWarnTicks = 300;

        protected PrefabSystem _prefabSystem;
        private EntityQuery _lanePrefabQuery;
        private EntityQuery _meshPrefabQuery;
        private int _waitTicks;

        /// <summary>Lane prefabs (hosts and clone sources) that must all be loaded.</summary>
        protected abstract IEnumerable<string> RequiredLanes { get; }
        /// <summary>Vanilla meshes that must be loaded (the fallbacks).</summary>
        protected abstract IEnumerable<string> RequiredMeshes { get; }
        /// <summary>Meshes picked in the settings; resolved if present, else PickMesh falls back.</summary>
        protected abstract IEnumerable<string> StyleMeshes { get; }
        /// <summary>Clones, configures and registers the markings.</summary>
        protected abstract void Apply(Dictionary<string, NetLanePrefab> lanes, Dictionary<string, RenderPrefab> meshes);

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _lanePrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<NetLaneData>());
            _meshPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<MeshData>());
            RequireForUpdate(_lanePrefabQuery);
        }

        protected override void OnUpdate()
        {
            try
            {
                var lanes = FindPrefabs<NetLanePrefab>(_lanePrefabQuery, RequiredLanes);
                var requiredMeshes = new List<string>(RequiredMeshes);
                requiredMeshes.AddRange(CustomLineMeshFactory.RequiredVanillaSources);
                var meshNames = new List<string>(requiredMeshes);
                meshNames.AddRange(StyleMeshes);
                var meshes = FindPrefabs<RenderPrefab>(_meshPrefabQuery, meshNames);

                string missing = FirstMissing(lanes, RequiredLanes) ?? FirstMissing(meshes, requiredMeshes);
                if (missing != null)
                {
                    // Vanilla prefabs load over several ticks; only a long wait is worth reporting.
                    if (++_waitTicks == kWaitWarnTicks)
                        log.Warn($"{GetType().Name}: still waiting for vanilla prefab '{missing}' after {kWaitWarnTicks} PrefabUpdate ticks");
                    return;
                }

                CustomLineMeshFactory.EnsureCustomLineMeshes(_prefabSystem, meshes);
                Apply(lanes, meshes);
            }
            catch (Exception e)
            {
                log.Error(e, $"{GetType().Name} failed");
            }
            Enabled = false;
        }

        /// <summary>
        /// An unregistered in-memory clone of <paramref name="sourceName"/>, configured by the caller
        /// BEFORE <see cref="Register"/>. Avoids DuplicatePrefab + UpdatePrefab, which registers the
        /// source's hosting first and then re-inits, producing duplicate SecondaryNetLane entries
        /// and native crashes alongside Road Builder's dynamic prefab updates.
        /// </summary>
        protected static NetLanePrefab CreateClone(Dictionary<string, NetLanePrefab> lanes, string sourceName, string cloneName, out SecondaryLane secondaryLane)
        {
            var clone = (NetLanePrefab)lanes[sourceName].Clone(cloneName);
            if (!clone.TryGet(out secondaryLane))
                throw new InvalidOperationException($"'{sourceName}' has no SecondaryLane");
            // Clone() copies the source's old names; left in, they collide in PrefabSystem's ID map.
            if (clone.Has<ObsoleteIdentifiers>()) clone.Remove<ObsoleteIdentifiers>();
            return clone;
        }

        /// <summary>Puts <paramref name="mesh"/> into every populated mesh slot and registers the clone.
        /// Clone() round-trips through JSON, so the slots are the clone's own objects.</summary>
        protected void Register(NetLanePrefab clone, RenderPrefab mesh)
        {
            var meshes = ((NetLaneGeometryPrefab)clone).m_Meshes;
            for (int i = 0; i < meshes.Length; i++)
                if (meshes[i].m_Mesh != null) meshes[i].m_Mesh = mesh;
            _prefabSystem.AddPrefab(clone);
            log.Debug($"registered '{clone.name}' mesh='{mesh.name}'");
        }

        /// <summary>The wanted mesh, else the (required, so present) fallback.</summary>
        protected static RenderPrefab PickMesh(Dictionary<string, RenderPrefab> meshes, string wanted, string fallback)
        {
            if (meshes.TryGetValue(wanted, out var mesh)) return mesh;
            log.Warn($"mesh '{wanted}' not found — falling back to '{fallback}'");
            return meshes[fallback];
        }

        private Dictionary<string, T> FindPrefabs<T>(EntityQuery query, IEnumerable<string> names) where T : PrefabBase
        {
            var wanted = new HashSet<string>(names);
            var result = new Dictionary<string, T>();
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                if (_prefabSystem.TryGetPrefab<T>(entity, out var prefab) && wanted.Contains(prefab.name) && !result.ContainsKey(prefab.name))
                    result[prefab.name] = prefab;
            return result;
        }

        private static string FirstMissing<T>(Dictionary<string, T> found, IEnumerable<string> names)
        {
            foreach (var n in names)
                if (!found.ContainsKey(n)) return n;
            return null;
        }
    }
}
