using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Mathematics;

namespace AutoEdgeMarkings.Systems.Prefabs
{
    /// <summary>
    /// Marks parallel street-parking zones, which vanilla leaves as bare asphalt.
    ///
    /// Vanilla only marks perpendicular and angled bays ('Parking Cross Line', 'Car Bay Line').
    /// Parallel parking uses the 'Parking Lane 2' lane prefab, which no vanilla marking references.
    /// Two prefabs per region theme are cloned from the closest vanilla marking, so material, LODs,
    /// submeshes and archetype come along, and then get the mesh chosen in the settings (vanilla or
    /// built-in procedural decal from CustomLineMeshFactory):
    ///
    ///  * "Parking Line", cloned from 'Car Bay Line' and hosted on 'Parking Lane 2' on the
    ///    parking side: a line along the whole zone.
    ///  * "Parking End", cloned from 'Parking Cross Line', with 'Parking Lane 2' in
    ///    m_CrossingLanes. That lane has SlotInterval 0 (a single slot), so the crossing path draws
    ///    exactly one tick at the block start and one at the block end; RequireContinue=false keeps
    ///    both ends.
    ///
    /// Vanilla prefabs are never edited. Vanilla SecondaryLaneSystem places and cuts the lines.
    /// </summary>
    public partial class ParkingLineCloneSystem : LaneCloneSystemBase
    {
        // 'Parking Lane 2' is present in a road section exactly when it has a parallel parking zone.
        // 'Boarding Lane 0' is always present, even with a wide sidewalk and no parking, so hosting on
        // it would draw the line everywhere and cover the curb edge line.
        private const string kParkingLaneName = "Parking Lane 2";

        // Used when the chosen built-in mesh could not be built.
        private const string kFallbackLineMesh = "White Dashed Line Mesh - Dense";
        private const string kFallbackEndMesh = "White Solid Line Mesh";

        private enum Role { Longitudinal, End }
        private static readonly (string src, string clone, Role role)[] kRecipes =
        {
            ("EU Car Bay Line",       "AEM EU Parking Line", Role.Longitudinal),
            ("NA Car Bay Line",       "AEM NA Parking Line", Role.Longitudinal),
            ("EU Parking Cross Line", "AEM EU Parking End",  Role.End),
            ("NA Parking Cross Line", "AEM NA Parking End",  Role.End),
        };

        protected override IEnumerable<string> RequiredLanes
        {
            get
            {
                var names = new List<string>(kCityLaneNames) { kParkingLaneName };
                foreach (var r in kRecipes) names.Add(r.src);
                return names;
            }
        }
        protected override IEnumerable<string> RequiredMeshes => new[] { kFallbackLineMesh, kFallbackEndMesh };
        // A null end mesh ("None") resolves to the fallback; the end clones then host nothing.
        protected override IEnumerable<string> StyleMeshes =>
            new[] { Mod.Settings.ParkingLineMeshName(), Mod.Settings.ParkingEndMeshName() ?? kFallbackEndMesh };

        protected override void Apply(Dictionary<string, NetLanePrefab> lanes, Dictionary<string, RenderPrefab> meshes)
        {
            var settings = Mod.Settings;
            bool parkingOn = settings.ParkingMarkingsEnabled;
            string endMeshName = settings.ParkingEndMeshName();
            bool wantEnds = parkingOn && endMeshName != null;
            var parkingLane = lanes[kParkingLaneName];

            RenderPrefab lineMesh = PickMesh(meshes, settings.ParkingLineMeshName(), kFallbackLineMesh);
            RenderPrefab endMesh = PickMesh(meshes, endMeshName ?? kFallbackEndMesh, kFallbackEndMesh);

            // Created unconditionally; the settings only decide whether hosting is attached.
            foreach (var (srcName, cloneName, role) in kRecipes)
            {
                var clone = CreateClone(lanes, srcName, cloneName, out var sec);
                sec.m_LeftLanes = Array.Empty<SecondaryLaneInfo>();
                sec.m_RightLanes = Array.Empty<SecondaryLaneInfo>();
                sec.m_CrossingLanes = Array.Empty<SecondaryLaneInfo2>();
                sec.m_CanFlipSides = true;

                if (role == Role.Longitudinal)
                {
                    if (parkingOn)
                    {
                        var carriageway = new SecondaryLaneInfo[kCityLaneNames.Length];
                        for (int i = 0; i < carriageway.Length; i++)
                            carriageway[i] = new SecondaryLaneInfo { m_Lane = lanes[kCityLaneNames[i]], m_RequireSafe = true };
                        sec.m_LeftLanes = carriageway;
                        sec.m_RightLanes = new[] { new SecondaryLaneInfo { m_Lane = parkingLane, m_RequireSafe = true } };
                    }
                    sec.m_FitToParkingSpaces = false;
                    Register(clone, lineMesh);
                }
                else
                {
                    if (wantEnds)
                        sec.m_CrossingLanes = new[] { new SecondaryLaneInfo2 { m_Lane = parkingLane, m_RequireContinue = false } };
                    sec.m_FitToParkingSpaces = true;
                    sec.m_LengthOffset = new float2(-0.1f, 0f);
                    sec.m_PositionOffset = new float3(0.1f, 0f, 0f);
                    Register(clone, endMesh);
                }
            }

            log.Info($"ParkingLineCloneSystem: registered {kRecipes.Length} prefab(s) (enabled={parkingOn}, line='{lineMesh.name}', end='{(wantEnds ? endMesh.name : "(none)")}')");
        }
    }
}
