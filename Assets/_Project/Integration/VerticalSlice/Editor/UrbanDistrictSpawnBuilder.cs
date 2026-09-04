#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectHive.Gameplay.Raid;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectHive.Editor.Integration
{
    public static class UrbanDistrictSpawnBuilder
    {
        private sealed class Footprint
        {
            public Bounds Bounds;
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            public Footprint(MeshFilter filter)
            {
                vertices = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                triangles = filter.sharedMesh.triangles;
                Bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 p in vertices) Bounds.Encapsulate(p);
            }
            public bool Contains(Vector3 p)
            {
                if (p.x < Bounds.min.x || p.x > Bounds.max.x || p.z < Bounds.min.z || p.z > Bounds.max.z) return false;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    float area = Cross(a, b, c);
                    if (Mathf.Abs(area) < 0.01f) continue;
                    float u = Cross(a, b, p), v = Cross(b, c, p), w = Cross(c, a, p);
                    if ((u >= -0.001f && v >= -0.001f && w >= -0.001f) ||
                        (u <= 0.001f && v <= 0.001f && w <= 0.001f)) return true;
                }
                return false;
            }
            private static float Cross(Vector3 a, Vector3 b, Vector3 c) => (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        }

        public static PrototypeSpawnDistrict[] Build(Scene scene, int agentTypeId)
        {
            GameObject source = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "small_road");
            GameObject mainRoads = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "RoadPoly");
            if (source == null || mainRoads == null) throw new InvalidOperationException("small_road / RoadPoly source is missing.");
            MeshFilter[] meshes = source.transform.Cast<Transform>()
                .Select(t => t.GetComponent<MeshFilter>()).Where(m => m != null && m.sharedMesh != null).ToArray();
            if (meshes.Length != 9) throw new InvalidOperationException($"District source changed: expected 9 small_road meshes, found {meshes.Length}. Review district boundaries.");
            var footprints = meshes.Select(m => new Footprint(m)).ToArray();
            Footprint[] roads = mainRoads.transform.Cast<Transform>()
                .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^road\d+$"))
                .Select(t => t.GetComponent<MeshFilter>()).Where(m => m != null && m.sharedMesh != null)
                .Select(m => new Footprint(m)).ToArray();
            // The existing broad alley-floor meshes define districts. Two pieces in the centre/east belong to one district.
            int[][] members = { new[] { 7 }, new[] { 4, 5 }, new[] { 3, 6 }, new[] { 1 }, new[] { 2 }, new[] { 8 }, new[] { 0 } };
            string[] labels = { "NorthWest", "Central", "NorthEast", "West", "East", "SouthWest", "School" };
            var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeId);
            float radius = Mathf.Max(0.35f, settings.agentRadius);
            float height = Mathf.Max(radius * 2f, settings.agentHeight);
            var allCandidates = new List<Vector3>();
            var districtCandidates = new List<Vector3[]>();
            Physics.SyncTransforms();
            for (int district = 0; district < members.Length; district++)
            {
                Footprint[] allowed = members[district].Select(i => footprints[i]).ToArray();
                Bounds bounds = allowed[0].Bounds;
                foreach (Footprint footprint in allowed) bounds.Encapsulate(footprint.Bounds);
                var candidates = new List<Vector3>();
                for (float z = bounds.min.z + 3f; z <= bounds.max.z; z += 6f)
                for (float x = bounds.min.x + 3f; x <= bounds.max.x; x += 6f)
                {
                    Vector3 p = new Vector3(x, bounds.max.y + 0.3f, z);
                    if (!allowed.Any(f => f.Contains(p)) || roads.Any(f => f.Contains(p))) continue;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, filter)) continue;
                    if (Mathf.Abs(hit.position.y - p.y) > 2f || !allowed.Any(f => f.Contains(hit.position)) || roads.Any(f => f.Contains(hit.position))) continue;
                    Vector3 bottom = hit.position + Vector3.up * (radius + 0.15f);
                    Vector3 top = hit.position + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
                    if (Physics.CheckCapsule(bottom, top, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (candidates.Any(q => (q - hit.position).sqrMagnitude < 25f) ||
                        allCandidates.Any(q => (q - hit.position).sqrMagnitude < 25f)) continue;
                    candidates.Add(hit.position);
                }
                if (candidates.Count < 20) throw new InvalidOperationException($"{labels[district]}: only {candidates.Count} safe spawn candidates. At least 20 required for 10 spawns and player exclusion.");
                // Evenly reduce the authoring grid to keep serialized scene data compact.
                int take = Mathf.Min(160, candidates.Count);
                Vector3[] selected = Enumerable.Range(0, take).Select(i => candidates[i * candidates.Count / take]).ToArray();
                districtCandidates.Add(selected);
                allCandidates.AddRange(selected);
            }
            var root = new GameObject("_BreckenSpawnDistricts");
            SceneManager.MoveGameObjectToScene(root, scene);
            var result = new List<PrototypeSpawnDistrict>();
            for (int i = 0; i < labels.Length; i++)
            {
                Vector3[] candidates = districtCandidates[i];
                var go = new GameObject($"District_{i + 1:00}_{labels[i]}");
                go.transform.SetParent(root.transform);
                go.transform.position = candidates.Aggregate(Vector3.zero, (sum, p) => sum + p) / candidates.Length;
                var district = go.AddComponent<PrototypeSpawnDistrict>();
                district.Configure(labels[i], 10, candidates);
                result.Add(district);
                Debug.Log($"[DistrictSpawn] {go.name}: population=10, safeCandidates={candidates.Length}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return result.ToArray();
        }
    }
}
#endif
