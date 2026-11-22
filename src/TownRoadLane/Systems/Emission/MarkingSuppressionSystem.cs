using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using TownRoadLane.Components;
using SubLane = Game.Net.SubLane;
using SecondaryLane = Game.Net.SecondaryLane;

namespace TownRoadLane.Systems.Emission
{
    /// <summary>
    /// Suppresses vanilla <see cref="SecondaryLane"/> markings on nodes and edges that either have
    /// <see cref="MarkingOverride"/> with <see cref="MarkingOverride.HideAll"/> set or are nodes
    /// with at least one user-drawn <see cref="MarkingLine"/>.
    ///
    /// Runs in <c>Modification5</c> right before <see cref="SecondaryLaneReferencesSystem"/>,
    /// leaving the vanilla <see cref="SecondaryLaneSystem"/> completely enabled and untouched for
    /// full compatibility with game updates and mods such as Road Builder.
    ///
    /// Two complementary fast paths avoid scanning every secondary lane in the city:
    ///   1. Newly created or updated vanilla secondary lanes (<c>Created</c>/<c>Updated</c>,
    ///      excluding <see cref="TRLSegmentLink"/>) whose owner is suppressed are destroyed
    ///      immediately (if <c>Created</c> this tick, before any reference/batch system sees them)
    ///      or marked <c>Deleted</c> (if pre-existing).
    ///   2. Suppressed owners that were updated, whose <see cref="MarkingOverride"/> /
    ///      <see cref="MarkingLine"/> changed, or on the first pass after a save load have their
    ///      existing <see cref="SubLane"/> buffers swept for surviving vanilla secondary lanes.
    /// </summary>
    public partial class MarkingSuppressionSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private EntityQuery _suppressedOwnersAny;
        private EntityQuery _suppressedOwnersWithSubLanes;
        private EntityQuery _updatedSuppressedOwners;
        private EntityQuery _changedOverrides;
        private EntityQuery _changedLines;
        private EntityQuery _changedSecondaryLanes;
        private bool _postLoadSweepPending;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Fast gate: if no entity in the city has MarkingOverride or MarkingLine, skip work.
            _suppressedOwnersAny = GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<MarkingOverride>(),
                    ComponentType.ReadOnly<MarkingLine>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                },
            });

            _suppressedOwnersWithSubLanes = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SubLane>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<MarkingOverride>(),
                    ComponentType.ReadOnly<MarkingLine>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });

            // Distinct component sets from _suppressedOwnersWithSubLanes so Unity's EntityQuery
            // cache never shares a change filter with the unfiltered post-load sweep query.
            _updatedSuppressedOwners = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<SubLane>(),
                    ComponentType.ReadOnly<Updated>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<MarkingOverride>(),
                    ComponentType.ReadOnly<MarkingLine>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            _changedOverrides = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MarkingOverride>(),
                    ComponentType.ReadOnly<SubLane>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            _changedOverrides.SetChangedVersionFilter(ComponentType.ReadOnly<MarkingOverride>());

            _changedLines = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<MarkingLine>(),
                    ComponentType.ReadOnly<Node>(),
                    ComponentType.ReadOnly<SubLane>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            _changedLines.SetChangedVersionFilter(ComponentType.ReadOnly<MarkingLine>());

            // Brand-new or updated vanilla secondary lanes emitted by SecondaryLaneSystem in 4B.
            _changedSecondaryLanes = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<SecondaryLane>(),
                    ComponentType.ReadOnly<Owner>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<Created>(),
                    ComponentType.ReadOnly<Updated>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<TRLSegmentLink>(),
                    ComponentType.ReadOnly<Deleted>(),
                },
            });

            RequireForUpdate(_suppressedOwnersAny);
        }

        protected override void OnGameLoaded(Colossal.Serialization.Entities.Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            _postLoadSweepPending = true;
        }

        protected override void OnUpdate()
        {
            bool runPostLoadSweep = _postLoadSweepPending;
            _postLoadSweepPending = false;

            bool hasChangedLanes = !_changedSecondaryLanes.IsEmptyIgnoreFilter;
            bool hasOwnerChanges = runPostLoadSweep
                || !_changedOverrides.IsEmpty
                || !_changedLines.IsEmpty
                || !_updatedSuppressedOwners.IsEmptyIgnoreFilter;

            if (!hasChangedLanes && !hasOwnerChanges)
                return;

            var toSuppress = new HashSet<Entity>();

            // Path 1: intercept newly created or updated vanilla secondary lanes before
            // SecondaryLaneReferencesSystem registers them onto the owner's SubLane buffer.
            if (hasChangedLanes)
            {
                using var lanes = _changedSecondaryLanes.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < lanes.Length; i++)
                {
                    Entity lane = lanes[i];
                    Entity owner = EntityManager.GetComponentData<Owner>(lane).m_Owner;
                    if (IsOwnerSuppressed(owner))
                        toSuppress.Add(lane);
                }
            }

            // Path 2: sweep existing SubLane buffers on suppressed owners so reused or loaded
            // vanilla secondary lanes are removed too.
            if (hasOwnerChanges)
            {
                if (runPostLoadSweep)
                {
                    CollectFromOwners(_suppressedOwnersWithSubLanes, toSuppress);
                }
                else
                {
                    if (!_changedOverrides.IsEmpty) CollectFromOwners(_changedOverrides, toSuppress);
                    if (!_changedLines.IsEmpty) CollectFromOwners(_changedLines, toSuppress);
                    if (!_updatedSuppressedOwners.IsEmptyIgnoreFilter) CollectFromOwners(_updatedSuppressedOwners, toSuppress);
                }
            }

            if (toSuppress.Count == 0)
                return;

            int destroyed = 0;
            int deleted = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity lane in toSuppress)
            {
                if (!EntityManager.Exists(lane) || EntityManager.HasComponent<Deleted>(lane))
                    continue;

                if (EntityManager.HasComponent<Created>(lane))
                {
                    // Spawned in Modification4B this same tick; not yet referenced in SubLane,
                    // search trees or render batches, so destroying it leaves no trace.
                    ecb.DestroyEntity(lane);
                    destroyed++;
                }
                else
                {
                    // Already live from an earlier frame: mark Deleted (and drop Applied like
                    // vanilla RemoveUnusedOldLanes) so SecondaryLaneReferencesSystem and render
                    // batches clean it up cleanly.
                    if (EntityManager.HasComponent<Applied>(lane))
                        ecb.RemoveComponent<Applied>(lane);
                    ecb.AddComponent<Deleted>(lane);
                    deleted++;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            if (destroyed > 0 || deleted > 0)
                log.Debug($"MarkingSuppressionSystem: suppressed {destroyed} created + {deleted} existing vanilla secondary lane(s)");
        }

        private void CollectFromOwners(EntityQuery ownerQuery, HashSet<Entity> toSuppress)
        {
            using var owners = ownerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < owners.Length; i++)
            {
                Entity owner = owners[i];
                if (!IsOwnerSuppressed(owner)) continue;
                if (!EntityManager.HasBuffer<SubLane>(owner)) continue;

                var subLanes = EntityManager.GetBuffer<SubLane>(owner, isReadOnly: true);
                for (int s = 0; s < subLanes.Length; s++)
                {
                    Entity subLane = subLanes[s].m_SubLane;
                    if (subLane == Entity.Null || !EntityManager.Exists(subLane)) continue;
                    if (!EntityManager.HasComponent<SecondaryLane>(subLane)) continue;
                    if (EntityManager.HasComponent<TRLSegmentLink>(subLane)) continue;
                    if (EntityManager.HasComponent<Deleted>(subLane)) continue;
                    toSuppress.Add(subLane);
                }
            }
        }

        private bool IsOwnerSuppressed(Entity owner)
        {
            if (owner == Entity.Null || !EntityManager.Exists(owner))
                return false;

            if (IsDirectOwnerSuppressed(owner))
                return true;

            // Temp preview nodes/edges created by road tools inherit the original entity's
            // suppression state via Temp.m_Original.
            if (EntityManager.HasComponent<Temp>(owner))
            {
                Entity original = EntityManager.GetComponentData<Temp>(owner).m_Original;
                if (original != Entity.Null && EntityManager.Exists(original) && IsDirectOwnerSuppressed(original))
                    return true;
            }

            return false;
        }

        private bool IsDirectOwnerSuppressed(Entity owner)
        {
            if (EntityManager.HasComponent<MarkingOverride>(owner)
                && EntityManager.GetComponentData<MarkingOverride>(owner).HideAll)
                return true;

            if (EntityManager.HasComponent<Node>(owner)
                && EntityManager.HasBuffer<MarkingLine>(owner)
                && EntityManager.GetBuffer<MarkingLine>(owner, isReadOnly: true).Length > 0)
                return true;

            return false;
        }
    }
}
