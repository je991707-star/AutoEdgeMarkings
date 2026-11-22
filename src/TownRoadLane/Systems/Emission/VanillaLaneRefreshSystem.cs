using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Entities;

namespace TownRoadLane.Systems.Emission
{
    /// <summary>
    /// Marks every road edge and node Updated once when a new game starts from a map, so vanilla
    /// <see cref="SecondaryLaneSystem"/> rebuilds the map's built-in roads with the clone prefabs.
    ///
    /// Markings are saved with the map. SecondaryLaneSystem only processes Updated or Deleted
    /// owners, so without this the map's roads keep their vanilla-only markings (e.g. no auto edge
    /// line) until something reconnects them. Saved games already contain the rebuilt markings,
    /// so loading a save does not refresh.
    /// </summary>
    public partial class VanillaLaneRefreshSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private EntityQuery _ownerQuery;
        private bool _pending;

        protected override void OnCreate()
        {
            base.OnCreate();
            // Same owners vanilla SecondaryLaneSystem handles, limited to roads.
            _ownerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SubLane>() },
                Any = new[] { ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Node>() },
                None = new[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Updated>(),
                    ComponentType.ReadOnly<OutsideConnection>(),
                },
            });
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            _pending = purpose == Purpose.NewGame;
        }

        protected override void OnUpdate()
        {
            if (!_pending) return;
            _pending = false;
            int count = _ownerQuery.CalculateEntityCount();
            if (count == 0) return;
            EntityManager.AddComponent<Updated>(_ownerQuery);
            log.Info($"lane-refresh: new game — marked {count} map road edge(s)/node(s) Updated so vanilla markings rebuild with the clone prefabs");
        }
    }
}
