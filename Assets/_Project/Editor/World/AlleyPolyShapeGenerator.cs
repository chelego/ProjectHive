using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace ProjectHive.EditorTools.World
{
    /// <summary>
    /// Builds editable ProBuilder alley surfaces and raised city blocks from the existing
    /// Map01_Blockout road/building proxies. Everything generated stays under one preview root.
    /// </summary>
    public static class AlleyPolyShapeGenerator
    {
        private const string RoadRootPath = "Map01_Blockout/Roads";
        private const string BuildingRootPath = "Map01_Blockout/BuildingLots";
        private const string OutputRootName = "Generated_AlleyPolys_PREVIEW";

        private const float MaximumAlleyWidth = 8.5f;
        private const float RoadBaseY = 0f;
        private const float RoadThickness = 0.03f;
        private const float RoadUnionCellSize = 1.5f;
        private const float RoadUnionPadding = 0.35f;
        private const float RoadSimplifyTolerance = 1.15f;
        private const float BlockBaseY = -0.55f;
        private const float BlockThickness = 1f;
        private const float CellSize = 2f;
        private const float GridPadding = 12f;
        private const float RoadPadding = 0.75f;
        private const float MinimumBlockArea = 80f;
        private const float MaximumBlockArea = 50000f;
        private const float SimplifyTolerance = 1.45f;

        private sealed class RoadBox
        {
            public Transform Transform;
            public BoxCollider Collider;
            public Bounds WorldBounds;
            public float Width;

            public bool Contains(Vector2 point, float padding)
            {
                var local = Transform.InverseTransformPoint(new Vector3(point.x, Transform.position.y, point.y));
                var scale = Transform.lossyScale;
                var padX = padding / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
                var padZ = padding / Mathf.Max(0.0001f, Mathf.Abs(scale.z));
                var center = Collider.center;
                var half = Collider.size * 0.5f;
                return local.x >= center.x - half.x - padX && local.x <= center.x + half.x + padX
                    && local.z >= center.z - half.z - padZ && local.z <= center.z + half.z + padZ;
            }
        }

        private sealed class PolygonRegion
        {
            public List<Vector2> Points;
            public Bounds Bounds;
        }

        private sealed class GridComponent
        {
            public readonly List<int> Cells = new List<int>();
            public bool TouchesBoundary;
            public int BuildingCount;
            public bool ContainsManualFloorBuilding;
        }

        [MenuItem("Tools/Project Hive/Map/Generate Alley PolyShapes")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[AlleyPolyShapeGenerator] Exit Play Mode first.");
                return;
            }

            var roadRoot = GameObject.Find(RoadRootPath);
            var buildingRoot = GameObject.Find(BuildingRootPath);
            if (roadRoot == null || buildingRoot == null)
            {
                Debug.LogError("[AlleyPolyShapeGenerator] Map01_Blockout/Roads or BuildingLots was not found.");
                return;
            }

            ClearInternal(false);
            SetAllSourceAlleysEnabled(roadRoot, true);

            var sourceRoads = CollectRoadBoxes(roadRoot);
            var alleys = sourceRoads.Where(road =>
                    road.Transform.name.StartsWith("trail", StringComparison.OrdinalIgnoreCase)
                    && road.Width <= MaximumAlleyWidth)
                .ToList();
            var existingRoads = CollectExistingPolygons(false);
            var manualFloors = CollectExistingPolygons(true);

            var outputRoot = new GameObject(OutputRootName);
            Undo.RegisterCreatedObjectUndo(outputRoot, "Generate alley PolyShapes");
            var alleyRoot = CreateChild(outputRoot.transform, "AlleyRoads");
            var blockRoot = CreateChild(outputRoot.transform, "RaisedBlocks");
            var roadMaterial = FindMaterial("M_Asphalt_No_Lines_Advanced") ?? FindMaterial("Blockout_Road_Black");
            var blockMaterial = FindBlockMaterial();

            var generatedAlleys = new List<RoadBox>();
            var skippedManual = 0;
            foreach (var alley in alleys)
            {
                var center = alley.Transform.TransformPoint(alley.Collider.center);
                if (IsInsideHandAuthoredSmallRoad(center, existingRoads))
                {
                    skippedManual++;
                    continue;
                }

                generatedAlleys.Add(alley);
                SetRoadProxyEnabled(alley, false);
            }

            var generatedRoadMeshes = CreateCombinedRoadMeshes(
                alleyRoot.transform,
                generatedAlleys,
                roadMaterial);

            var generatedBlocks = GenerateBlocks(
                blockRoot.transform,
                sourceRoads,
                existingRoads,
                manualFloors,
                buildingRoot,
                blockMaterial);

            Selection.activeGameObject = outputRoot;
            EditorSceneManager.MarkSceneDirty(outputRoot.scene);
            EditorSceneManager.SaveScene(outputRoot.scene);
            Debug.Log("[AlleyPolyShapeGenerator] Combined " + generatedAlleys.Count
                + " alley proxies into " + generatedRoadMeshes + " ProBuilder road mesh(es) and generated "
                + generatedBlocks + " raised blocks. Skipped " + skippedManual
                + " alley proxies inside the hand-authored small_road example. RoadPoly and Lake_bridge were not modified.");
        }

        [MenuItem("Tools/Project Hive/Map/Clear Generated Alley PolyShapes")]
        public static void ClearGenerated()
        {
            ClearInternal(true);
        }

        private static void ClearInternal(bool save)
        {
            var roadRoot = GameObject.Find(RoadRootPath);
            if (roadRoot != null)
                SetAllSourceAlleysEnabled(roadRoot, true);

            var generated = GameObject.Find(OutputRootName);
            if (generated != null)
                Undo.DestroyObjectImmediate(generated);

            if (!save)
                return;
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AlleyPolyShapeGenerator] Cleared generated geometry and restored source alley proxies.");
        }

        private static List<RoadBox> CollectRoadBoxes(GameObject roadRoot)
        {
            var roads = new List<RoadBox>();
            foreach (var collider in roadRoot.GetComponentsInChildren<BoxCollider>(true))
            {
                var transform = collider.transform;
                var scale = transform.lossyScale;
                var sizeX = collider.size.x * Mathf.Abs(scale.x);
                var sizeZ = collider.size.z * Mathf.Abs(scale.z);
                var corners = GetWorldFootprint(transform, collider);
                var min = corners[0];
                var max = corners[0];
                for (var i = 1; i < corners.Length; i++)
                {
                    min = Vector3.Min(min, corners[i]);
                    max = Vector3.Max(max, corners[i]);
                }

                roads.Add(new RoadBox
                {
                    Transform = transform,
                    Collider = collider,
                    Width = Mathf.Min(sizeX, sizeZ),
                    WorldBounds = new Bounds((min + max) * 0.5f, max - min)
                });
            }
            return roads;
        }

        private static Vector3[] GetWorldFootprint(Transform transform, BoxCollider collider)
        {
            var c = collider.center;
            var h = collider.size * 0.5f;
            return new[]
            {
                transform.TransformPoint(new Vector3(c.x - h.x, c.y, c.z - h.z)),
                transform.TransformPoint(new Vector3(c.x + h.x, c.y, c.z - h.z)),
                transform.TransformPoint(new Vector3(c.x + h.x, c.y, c.z + h.z)),
                transform.TransformPoint(new Vector3(c.x - h.x, c.y, c.z + h.z))
            };
        }

        private static int CreateCombinedRoadMeshes(
            Transform parent,
            IList<RoadBox> roads,
            Material material)
        {
            if (roads.Count == 0)
                return 0;

            var minX = roads.Min(road => road.WorldBounds.min.x) - RoadUnionCellSize * 2f;
            var maxX = roads.Max(road => road.WorldBounds.max.x) + RoadUnionCellSize * 2f;
            var minZ = roads.Min(road => road.WorldBounds.min.z) - RoadUnionCellSize * 2f;
            var maxZ = roads.Max(road => road.WorldBounds.max.z) + RoadUnionCellSize * 2f;
            var width = Mathf.CeilToInt((maxX - minX) / RoadUnionCellSize);
            var height = Mathf.CeilToInt((maxZ - minZ) / RoadUnionCellSize);
            if (width <= 0 || height <= 0 || (long)width * height > 2000000L)
            {
                Debug.LogError("[AlleyPolyShapeGenerator] Unsupported road union grid: " + width + " x " + height);
                return 0;
            }

            var roadMask = new bool[width * height];
            RasterizeRoadBoxes(
                roads,
                roadMask,
                width,
                height,
                minX,
                minZ,
                RoadUnionCellSize,
                RoadUnionPadding);

            var ids = new int[roadMask.Length];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = roadMask[i] ? -2 : -1;
            var components = FloodFill(ids, width, height);
            var componentMeshes = new List<ProBuilderMesh>();

            for (var componentId = 0; componentId < components.Count; componentId++)
            {
                var contours = ExtractContours(
                    componentId,
                    components[componentId],
                    ids,
                    width,
                    height,
                    minX,
                    minZ,
                    RoadUnionCellSize);
                if (contours.Count == 0)
                    continue;

                var rawOuter = contours.OrderByDescending(loop => Mathf.Abs(SignedArea(loop))).First();
                var outer = SimplifyClosedContour(rawOuter, RoadSimplifyTolerance);
                if (outer.Count < 3)
                    continue;
                if (SignedArea(outer) < 0f)
                    outer.Reverse();

                var holes = contours
                    .Where(loop => !ReferenceEquals(loop, rawOuter))
                    .Where(loop => Mathf.Abs(SignedArea(loop)) >= RoadUnionCellSize * RoadUnionCellSize * 4f)
                    .Select(loop => SimplifyClosedContour(loop, RoadSimplifyTolerance))
                    .Where(loop => loop.Count >= 3 && PointInPolygon(loop[0], outer))
                    .ToList();
                foreach (var hole in holes)
                    if (SignedArea(hole) > 0f)
                        hole.Reverse();

                ProBuilderMesh componentMesh;
                if (TryCreateRoadComponent(
                    parent,
                    outer,
                    holes,
                    material,
                    roads[0],
                    componentMeshes.Count + 1,
                    out componentMesh))
                    componentMeshes.Add(componentMesh);
            }

            if (componentMeshes.Count == 0)
                return 0;

            List<ProBuilderMesh> combinedMeshes;
            if (componentMeshes.Count == 1)
            {
                combinedMeshes = new List<ProBuilderMesh> { componentMeshes[0] };
            }
            else
            {
                combinedMeshes = CombineMeshes.Combine(componentMeshes, componentMeshes[0]);
                if (combinedMeshes == null || combinedMeshes.Count == 0)
                    combinedMeshes = componentMeshes;
            }

            var keptObjects = new HashSet<GameObject>(combinedMeshes.Select(mesh => mesh.gameObject));
            foreach (var componentMesh in componentMeshes)
                if (!keptObjects.Contains(componentMesh.gameObject))
                    Undo.DestroyObjectImmediate(componentMesh.gameObject);

            for (var i = 0; i < combinedMeshes.Count; i++)
            {
                var mesh = combinedMeshes[i];
                var go = mesh.gameObject;
                go.name = combinedMeshes.Count == 1
                    ? "AlleyRoad_Combined"
                    : "AlleyRoad_Combined_" + (i + 1).ToString("000");
                go.transform.SetParent(parent, true);
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null && material != null)
                    renderer.sharedMaterials = new[] { material };
                var collider = go.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = Undo.AddComponent<MeshCollider>(go);
                collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = false;
                EditorUtility.SetDirty(go);
            }

            return combinedMeshes.Count;
        }

        private static bool TryCreateRoadComponent(
            Transform parent,
            IList<Vector2> outer,
            IList<List<Vector2>> holes,
            Material material,
            RoadBox sourceStyle,
            int index,
            out ProBuilderMesh mesh)
        {
            var centroid = Vector2.zero;
            foreach (var point in outer)
                centroid += point;
            centroid /= outer.Count;

            var go = new GameObject("AlleyRoad_Component_" + index.ToString("000"));
            Undo.RegisterCreatedObjectUndo(go, "Create combined alley road component");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centroid.x, RoadBaseY, centroid.y);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = sourceStyle.Transform.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(sourceStyle.Transform.gameObject));

            mesh = go.AddComponent<ProBuilderMesh>();
            var outerPoints = outer.Select(point =>
                new Vector3(point.x - centroid.x, 0f, point.y - centroid.y)).ToList();
            var holePoints = holes.Select(loop => (IList<Vector3>)loop.Select(point =>
                new Vector3(point.x - centroid.x, 0f, point.y - centroid.y)).ToList()).ToList();
            var result = mesh.CreateShapeFromPolygon(outerPoints, RoadThickness, false, holePoints);
            if (result.status == ActionResult.Status.Failure)
            {
                Debug.LogWarning("[AlleyPolyShapeGenerator] Combined road component failed: "
                    + go.name + " / " + result.notification);
                Undo.DestroyObjectImmediate(go);
                mesh = null;
                return false;
            }

            mesh.ToMesh();
            mesh.Refresh();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return true;
        }

        private static int GenerateBlocks(
            Transform parent,
            List<RoadBox> sourceRoads,
            List<PolygonRegion> existingRoads,
            List<PolygonRegion> manualFloors,
            GameObject buildingRoot,
            Material material)
        {
            var buildings = buildingRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (buildings.Length == 0)
                return 0;

            var minX = buildings.Min(r => r.bounds.min.x) - GridPadding;
            var maxX = buildings.Max(r => r.bounds.max.x) + GridPadding;
            var minZ = buildings.Min(r => r.bounds.min.z) - GridPadding;
            var maxZ = buildings.Max(r => r.bounds.max.z) + GridPadding;
            var width = Mathf.CeilToInt((maxX - minX) / CellSize);
            var height = Mathf.CeilToInt((maxZ - minZ) / CellSize);
            if (width <= 0 || height <= 0 || (long)width * height > 2000000L)
            {
                Debug.LogError("[AlleyPolyShapeGenerator] Unsupported block grid: " + width + " x " + height);
                return 0;
            }

            var roadMask = new bool[width * height];
            RasterizeRoadBoxes(sourceRoads, roadMask, width, height, minX, minZ);
            RasterizePolygons(existingRoads, roadMask, width, height, minX, minZ);

            var ids = new int[roadMask.Length];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = roadMask[i] ? -1 : -2;
            var components = FloodFill(ids, width, height);
            AssociateBuildings(buildings, components, ids, width, height, minX, minZ, manualFloors);

            var generated = 0;
            for (var componentId = 0; componentId < components.Count; componentId++)
            {
                var component = components[componentId];
                var area = component.Cells.Count * CellSize * CellSize;
                if (component.TouchesBoundary || component.BuildingCount == 0
                    || component.ContainsManualFloorBuilding || area < MinimumBlockArea || area > MaximumBlockArea)
                    continue;

                float rawContourArea;
                var contour = ExtractLargestContour(
                    componentId, component, ids, width, height, minX, minZ, out rawContourArea);
                // A connected outside region can wrap around one or more road rings. PolyShape does not
                // serialize hole contours, so generating its outer loop would cover roads and inner blocks.
                // Keep only components whose outer-loop area closely matches their actual grid-cell area.
                if (rawContourArea > area * 1.12f)
                    continue;
                contour = SimplifyClosedContour(contour, SimplifyTolerance);
                if (contour.Count < 3)
                    continue;
                if (SignedArea(contour) < 0f)
                    contour.Reverse();
                if (CreateBlockPolyShape(parent, contour, material, generated + 1, component.BuildingCount))
                    generated++;
            }
            return generated;
        }

        private static void RasterizeRoadBoxes(
            IEnumerable<RoadBox> roads,
            bool[] mask,
            int width,
            int height,
            float minX,
            float minZ)
        {
            RasterizeRoadBoxes(roads, mask, width, height, minX, minZ, CellSize, RoadPadding);
        }

        private static void RasterizeRoadBoxes(
            IEnumerable<RoadBox> roads,
            bool[] mask,
            int width,
            int height,
            float minX,
            float minZ,
            float cellSize,
            float padding)
        {
            foreach (var road in roads)
            {
                var b = road.WorldBounds;
                var x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - padding - minX) / cellSize), 0, width - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x + padding - minX) / cellSize), 0, width - 1);
                var z0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - padding - minZ) / cellSize), 0, height - 1);
                var z1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z + padding - minZ) / cellSize), 0, height - 1);
                for (var z = z0; z <= z1; z++)
                for (var x = x0; x <= x1; x++)
                    if (road.Contains(CellCenter(x, z, minX, minZ, cellSize), padding))
                        mask[x + z * width] = true;
            }
        }

        private static void RasterizePolygons(
            IEnumerable<PolygonRegion> polygons,
            bool[] mask,
            int width,
            int height,
            float minX,
            float minZ)
        {
            foreach (var polygon in polygons)
            {
                var x0 = Mathf.Clamp(Mathf.FloorToInt((polygon.Bounds.min.x - minX) / CellSize), 0, width - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((polygon.Bounds.max.x - minX) / CellSize), 0, width - 1);
                var z0 = Mathf.Clamp(Mathf.FloorToInt((polygon.Bounds.min.z - minZ) / CellSize), 0, height - 1);
                var z1 = Mathf.Clamp(Mathf.CeilToInt((polygon.Bounds.max.z - minZ) / CellSize), 0, height - 1);
                for (var z = z0; z <= z1; z++)
                for (var x = x0; x <= x1; x++)
                    if (PointInPolygon(CellCenter(x, z, minX, minZ), polygon.Points))
                        mask[x + z * width] = true;
            }
        }

        private static List<GridComponent> FloodFill(int[] ids, int width, int height)
        {
            var components = new List<GridComponent>();
            var queue = new Queue<int>();
            for (var index = 0; index < ids.Length; index++)
            {
                if (ids[index] != -2)
                    continue;
                var id = components.Count;
                var component = new GridComponent();
                components.Add(component);
                ids[index] = id;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Cells.Add(current);
                    var x = current % width;
                    var z = current / width;
                    if (x == 0 || z == 0 || x == width - 1 || z == height - 1)
                        component.TouchesBoundary = true;
                    TryQueue(current, x - 1, z, width, height, id, ids, queue);
                    TryQueue(current, x + 1, z, width, height, id, ids, queue);
                    TryQueue(current, x, z - 1, width, height, id, ids, queue);
                    TryQueue(current, x, z + 1, width, height, id, ids, queue);
                }
            }
            return components;
        }

        private static void TryQueue(
            int current,
            int x,
            int z,
            int width,
            int height,
            int componentId,
            int[] ids,
            Queue<int> queue)
        {
            if (x < 0 || z < 0 || x >= width || z >= height)
                return;
            var next = x + z * width;
            if (ids[next] != -2)
                return;
            ids[next] = componentId;
            queue.Enqueue(next);
        }

        private static void AssociateBuildings(
            IEnumerable<Renderer> buildings,
            IList<GridComponent> components,
            int[] ids,
            int width,
            int height,
            float minX,
            float minZ,
            IList<PolygonRegion> manualFloors)
        {
            foreach (var building in buildings)
            {
                var point = new Vector2(building.bounds.center.x, building.bounds.center.z);
                var x = Mathf.Clamp(Mathf.FloorToInt((point.x - minX) / CellSize), 0, width - 1);
                var z = Mathf.Clamp(Mathf.FloorToInt((point.y - minZ) / CellSize), 0, height - 1);
                var id = FindNearestComponent(ids, width, height, x, z);
                if (id < 0 || id >= components.Count)
                    continue;
                components[id].BuildingCount++;
                if (manualFloors.Any(floor => PointInPolygon(point, floor.Points)))
                    components[id].ContainsManualFloorBuilding = true;
            }
        }

        private static int FindNearestComponent(int[] ids, int width, int height, int centerX, int centerZ)
        {
            for (var radius = 0; radius <= 3; radius++)
            for (var z = Mathf.Max(0, centerZ - radius); z <= Mathf.Min(height - 1, centerZ + radius); z++)
            for (var x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(width - 1, centerX + radius); x++)
            {
                var id = ids[x + z * width];
                if (id >= 0)
                    return id;
            }
            return -1;
        }

        private static List<Vector2> ExtractLargestContour(
            int componentId,
            GridComponent component,
            int[] ids,
            int width,
            int height,
            float minX,
            float minZ,
            out float bestArea)
        {
            var contours = ExtractContours(
                componentId, component, ids, width, height, minX, minZ, CellSize);
            var best = contours.OrderByDescending(loop => Mathf.Abs(SignedArea(loop))).FirstOrDefault()
                ?? new List<Vector2>();
            bestArea = Mathf.Abs(SignedArea(best));
            return best;
        }

        private static List<List<Vector2>> ExtractContours(
            int componentId,
            GridComponent component,
            int[] ids,
            int width,
            int height,
            float minX,
            float minZ,
            float cellSize)
        {
            var outgoing = new Dictionary<long, Queue<long>>();
            foreach (var index in component.Cells)
            {
                var x = index % width;
                var z = index / width;
                if (z == 0 || ids[index - width] != componentId) AddEdge(outgoing, Pack(x, z), Pack(x + 1, z));
                if (x == width - 1 || ids[index + 1] != componentId) AddEdge(outgoing, Pack(x + 1, z), Pack(x + 1, z + 1));
                if (z == height - 1 || ids[index + width] != componentId) AddEdge(outgoing, Pack(x + 1, z + 1), Pack(x, z + 1));
                if (x == 0 || ids[index - 1] != componentId) AddEdge(outgoing, Pack(x, z + 1), Pack(x, z));
            }

            var contours = new List<List<Vector2>>();
            while (true)
            {
                var pair = outgoing.FirstOrDefault(item => item.Value.Count > 0);
                if (pair.Value == null || pair.Value.Count == 0)
                    break;
                var start = pair.Key;
                var current = start;
                var loop = new List<Vector2>();
                var guard = 0;
                do
                {
                    loop.Add(UnpackWorld(current, minX, minZ, cellSize));
                    Queue<long> next;
                    if (!outgoing.TryGetValue(current, out next) || next.Count == 0)
                        break;
                    current = next.Dequeue();
                    guard++;
                }
                while (current != start && guard < 200000);

                if (current != start || loop.Count < 3)
                    continue;
                contours.Add(loop);
            }
            return contours;
        }

        private static void AddEdge(IDictionary<long, Queue<long>> outgoing, long from, long to)
        {
            Queue<long> queue;
            if (!outgoing.TryGetValue(from, out queue))
            {
                queue = new Queue<long>();
                outgoing.Add(from, queue);
            }
            queue.Enqueue(to);
        }

        private static long Pack(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
        }

        private static Vector2 UnpackWorld(long key, float minX, float minZ, float cellSize)
        {
            var x = (int)(key >> 32);
            var z = (int)(key & 0xffffffffL);
            return new Vector2(minX + x * cellSize, minZ + z * cellSize);
        }

        private static List<Vector2> SimplifyClosedContour(List<Vector2> input, float tolerance)
        {
            var points = new List<Vector2>(input);
            var changed = true;
            var passes = 0;
            while (changed && points.Count > 3 && passes++ < 20)
            {
                changed = false;
                for (var i = points.Count - 1; i >= 0 && points.Count > 3; i--)
                {
                    var previous = points[(i - 1 + points.Count) % points.Count];
                    var current = points[i];
                    var next = points[(i + 1) % points.Count];
                    if (DistanceToSegment(current, previous, next) <= tolerance)
                    {
                        points.RemoveAt(i);
                        changed = true;
                    }
                }
            }
            return points;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 0.0001f)
                return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * t);
        }

        private static bool CreateBlockPolyShape(
            Transform parent,
            List<Vector2> contour,
            Material material,
            int index,
            int buildingCount)
        {
            var centroid = Vector2.zero;
            foreach (var point in contour)
                centroid += point;
            centroid /= contour.Count;

            var go = new GameObject("RaisedBlock_" + index.ToString("000") + "_Buildings" + buildingCount);
            Undo.RegisterCreatedObjectUndo(go, "Create raised block PolyShape");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centroid.x, BlockBaseY, centroid.y);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic
                | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var mesh = go.AddComponent<ProBuilderMesh>();
            var poly = go.AddComponent<PolyShape>();
            poly.SetControlPoints(contour.Select(point =>
                new Vector3(point.x - centroid.x, 0f, point.y - centroid.y)).ToList());
            poly.extrude = BlockThickness;
            poly.flipNormals = false;
            var result = poly.CreateShapeFromPolygon();
            if (result.status == ActionResult.Status.Failure)
            {
                Debug.LogWarning("[AlleyPolyShapeGenerator] Block failed: " + go.name + " / " + result.notification);
                Undo.DestroyObjectImmediate(go);
                return false;
            }
            mesh.ToMesh();
            mesh.Refresh();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = false;
            return true;
        }

        private static List<PolygonRegion> CollectExistingPolygons(bool floors)
        {
            var regions = new List<PolygonRegion>();
            foreach (var poly in UnityEngine.Object.FindObjectsByType<PolyShape>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!poly.gameObject.scene.IsValid() || IsUnderGeneratedRoot(poly.transform))
                    continue;
                var renderer = poly.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                var isFloor = renderer.sharedMaterials.Any(material =>
                    material != null && material.name == "ProBuilderDefault");
                if (isFloor != floors || poly.controlPoints.Count < 3)
                    continue;

                var points = poly.controlPoints.Select(poly.transform.TransformPoint)
                    .Select(point => new Vector2(point.x, point.z)).ToList();
                var minX = points.Min(point => point.x);
                var maxX = points.Max(point => point.x);
                var minZ = points.Min(point => point.y);
                var maxZ = points.Max(point => point.y);
                regions.Add(new PolygonRegion
                {
                    Points = points,
                    Bounds = new Bounds(
                        new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                        new Vector3(maxX - minX, 0f, maxZ - minZ))
                });
            }
            return regions;
        }

        private static bool IsInsideHandAuthoredSmallRoad(Vector3 center, IEnumerable<PolygonRegion> roads)
        {
            var point = new Vector2(center.x, center.z);
            return roads.Any(road => road.Points.Count > 8 && PointInPolygon(point, road.Points));
        }

        private static bool IsUnderGeneratedRoot(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name == OutputRootName)
                    return true;
                transform = transform.parent;
            }
            return false;
        }

        private static Vector2 CellCenter(int x, int z, float minX, float minZ)
        {
            return CellCenter(x, z, minX, minZ, CellSize);
        }

        private static Vector2 CellCenter(int x, int z, float minX, float minZ, float cellSize)
        {
            return new Vector2(minX + (x + 0.5f) * cellSize, minZ + (z + 0.5f) * cellSize);
        }

        private static bool PointInPolygon(Vector2 point, IList<Vector2> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 0.000001f) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float SignedArea(IList<Vector2> points)
        {
            var area = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                area += points[i].x * points[next].y - points[next].x * points[i].y;
            }
            return area * 0.5f;
        }

        private static Material FindBlockMaterial()
        {
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var material = renderer.sharedMaterials.FirstOrDefault(item =>
                    item != null && item.name == "ProBuilderDefault");
                if (material != null)
                    return material;
            }
            return FindMaterial("ProBuilderDefault");
        }

        private static Material FindMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null && material.name == name)
                    return material;
            }
            return null;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create generated map group");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetAllSourceAlleysEnabled(GameObject roadRoot, bool enabled)
        {
            foreach (var road in CollectRoadBoxes(roadRoot))
            {
                if (road.Transform.name.StartsWith("trail", StringComparison.OrdinalIgnoreCase)
                    && road.Width <= MaximumAlleyWidth)
                    SetRoadProxyEnabled(road, enabled);
            }
        }

        private static void SetRoadProxyEnabled(RoadBox road, bool enabled)
        {
            var renderer = road.Transform.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = enabled;
            road.Collider.enabled = enabled;
            EditorUtility.SetDirty(road.Transform.gameObject);
        }
    }
}
