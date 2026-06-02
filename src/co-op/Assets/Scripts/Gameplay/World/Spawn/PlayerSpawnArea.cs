using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.World.Spawn
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnArea : MonoBehaviour
    {
        public enum ShapePreset { Circle, Square, Rectangle }

        [Serializable]
        public sealed class FixedSpawnPoint
        {
            public Vector2 WorldXZ;
            public Vector3 EulerDeg;
        }

        [SerializeField] private List<Vector2> _vertices = new();

        public IReadOnlyList<Vector2> Vertices => _vertices;
        public int VertexCount => _vertices?.Count ?? 0;
        public Vector2 GetVertex(int i) => _vertices[i];
        public void SetVertex(int i, Vector2 v) => _vertices[i] = v;
        public void InsertVertex(int i, Vector2 v) => _vertices.Insert(i, v);
        public void RemoveVertex(int i) => _vertices.RemoveAt(i);

        [Header("Spawn rules")]
        [Tooltip("Minimum distance between two spawning players (the random sampler avoids existing player positions).")]
        [Min(0f)] public float MinSpacing = 1.5f;

        [Tooltip("Clearance kept from any collider (walls etc.). 0 = right against the wall.")]
        [Min(0f)] public float ObstaclePadding = 0.5f;

        [Tooltip("How far below the spawner's Y the ground may sit (downward raycast distance).")]
        [Min(0.1f)] public float MaxBelowDepth = 10f;

        [Header("Random-fallback rotation (use 'Apply player rotation' to bake)")]
        [Tooltip("Rotation applied when a player falls through to random sampling. Set via the inspector button.")]
        [SerializeField] private Vector3 _spawnEulerDeg;

        public Quaternion SpawnRotation => Quaternion.Euler(_spawnEulerDeg);
        public Vector3 SpawnEulerDeg => _spawnEulerDeg;

        [Header("Fixed spawn points (claim-pool; runtime picks random un-claimed)")]
        [SerializeField] private List<FixedSpawnPoint> _fixedSpawnPoints = new();

        public IReadOnlyList<FixedSpawnPoint> FixedSpawnPoints => _fixedSpawnPoints;
        public int FixedSpawnPointCount => _fixedSpawnPoints?.Count ?? 0;
        public FixedSpawnPoint GetFixedSpawnPoint(int i) => _fixedSpawnPoints[i];

        public void AddFixedSpawnPoint(Vector2 worldXZ, Vector3 eulerDeg) =>
            _fixedSpawnPoints.Add(new FixedSpawnPoint { WorldXZ = worldXZ, EulerDeg = eulerDeg });

        public void RemoveFixedSpawnPoint(int i) => _fixedSpawnPoints.RemoveAt(i);
        public void ClearFixedSpawnPoints() => _fixedSpawnPoints.Clear();

        public void SetFixedSpawnPointPos(int i, Vector2 worldXZ)
        {
            if (i < 0 || i >= _fixedSpawnPoints.Count) return;
            _fixedSpawnPoints[i].WorldXZ = worldXZ;
        }
        public void SetFixedSpawnPointEuler(int i, Vector3 eulerDeg)
        {
            if (i < 0 || i >= _fixedSpawnPoints.Count) return;
            _fixedSpawnPoints[i].EulerDeg = eulerDeg;
        }

        [Header("Shape preset (apply via inspector button)")]
        public ShapePreset Preset = ShapePreset.Circle;
        [Tooltip("Circle radius / Square half-size / Rectangle half-width.")]
        [Min(0.1f)] public float PresetSize = 5f;
        [Tooltip("Rectangle half-length (used only by the Rectangle preset).")]
        [Min(0.1f)] public float PresetSize2 = 3f;
        [Tooltip("Vertex count used by the Circle preset.")]
        [Range(3, 64)] public int PresetVertexCount = 24;

        [Header("Gizmo visibility")]
        public bool ShowContour = true;
        public bool ShowHeatmap = true;
        public bool ShowVertices = true;
        public bool ShowPreview = true;
        public bool ShowFixedPoints = true;

        [Header("Gizmo detail")]
        [Tooltip("Sub-samples per polygon edge for the surface-tracking contour.")]
        [Range(1, 32)] public int ContourSubSamples = 8;
        [Tooltip("Grid spacing for the validity heatmap (m).")]
        [Min(0.25f)] public float HeatmapSpacing = 0.5f;

        [Header("Colours")]
        public Color ContourColor = new Color(0.3f, 1f, 0.5f, 0.95f);
        public Color ContourBlockedColor = new Color(1f, 0.3f, 0.3f, 0.85f);
        public Color HeatmapValidColor = new Color(0.3f, 1f, 0.3f, 0.45f);
        public Color HeatmapBlockedColor = new Color(1f, 0.3f, 0.3f, 0.35f);
        public Color GhostColor = new Color(1f, 0.7f, 0.3f, 0.85f);
        public Color FixedGhostColor = new Color(0.4f, 0.9f, 1f, 0.85f);
        public Color GhostArrowColor = new Color(0.4f, 1f, 0.6f, 1f);

        [Header("Preview ghost")]
        [Tooltip("Player.prefab — rendered as a translucent ghost at the preview point.")]
        [SerializeField] private GameObject _previewPlayerPrefab;

        [Tooltip("Preview position in WORLD XZ (decoupled from the area's transform).")]
        [SerializeField] private Vector2 _previewWorldXZ;

        [Tooltip("Draft rotation shown on the ghost.")]
        [SerializeField] private Vector3 _previewEulerDeg;

        [SerializeField, HideInInspector] private bool _previewInitialized;

        public GameObject PreviewPlayerPrefab => _previewPlayerPrefab;
        public Vector2 PreviewWorldXZ { get => _previewWorldXZ; set => _previewWorldXZ = value; }
        public Vector3 PreviewEulerDeg { get => _previewEulerDeg; set => _previewEulerDeg = value; }
        public Quaternion PreviewRotation
        {
            get => Quaternion.Euler(_previewEulerDeg);
            set => _previewEulerDeg = value.eulerAngles;
        }

        public void ApplyPreviewRotation() => _spawnEulerDeg = _previewEulerDeg;
        public void ResetPreviewRotation() => _previewEulerDeg = _spawnEulerDeg;

        public bool PreviewRotationIsDraft =>
            Mathf.DeltaAngle(_previewEulerDeg.x, _spawnEulerDeg.x) != 0f ||
            Mathf.DeltaAngle(_previewEulerDeg.y, _spawnEulerDeg.y) != 0f ||
            Mathf.DeltaAngle(_previewEulerDeg.z, _spawnEulerDeg.z) != 0f;

        public void SnapPreviewToArea()
        {
            _previewWorldXZ = new Vector2(transform.position.x, transform.position.z);
            _previewInitialized = true;
        }

        public Vector3 LocalToWorld(Vector2 local) =>
            transform.position + transform.right * local.x + transform.forward * local.y;

        public Vector2 WorldToLocal(Vector3 world)
        {
            Vector3 d = world - transform.position;
            return new Vector2(Vector3.Dot(d, transform.right), Vector3.Dot(d, transform.forward));
        }

        public Rect GetLocalBounds()
        {
            if (_vertices == null || _vertices.Count == 0) return default;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in _vertices)
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public bool ContainsLocalXZ(Vector2 p)
        {
            var v = _vertices;
            int n = v?.Count ?? 0;
            if (n < 3) return false;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 vi = v[i], vj = v[j];
                if (((vi.y > p.y) != (vj.y > p.y)) &&
                    (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y) + vi.x))
                    inside = !inside;
            }
            return inside;
        }

        public bool TryResolveGround(Vector2 worldXZ, out Vector3 groundPos)
        {
            var origin = new Vector3(worldXZ.x, transform.position.y + 0.1f, worldXZ.y);
            if (Physics.Raycast(origin, Vector3.down, out var hit, MaxBelowDepth + 0.1f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                groundPos = hit.point;
                return true;
            }
            groundPos = origin;
            return false;
        }

        public void ApplyPreset()
        {
            _vertices.Clear();
            switch (Preset)
            {
                case ShapePreset.Circle:
                    for (int i = 0; i < PresetVertexCount; i++)
                    {
                        float a = (i / (float)PresetVertexCount) * Mathf.PI * 2f;
                        _vertices.Add(new Vector2(Mathf.Cos(a) * PresetSize, Mathf.Sin(a) * PresetSize));
                    }
                    break;
                case ShapePreset.Square:
                    _vertices.Add(new Vector2( PresetSize,  PresetSize));
                    _vertices.Add(new Vector2(-PresetSize,  PresetSize));
                    _vertices.Add(new Vector2(-PresetSize, -PresetSize));
                    _vertices.Add(new Vector2( PresetSize, -PresetSize));
                    break;
                case ShapePreset.Rectangle:
                    _vertices.Add(new Vector2( PresetSize,  PresetSize2));
                    _vertices.Add(new Vector2(-PresetSize,  PresetSize2));
                    _vertices.Add(new Vector2(-PresetSize, -PresetSize2));
                    _vertices.Add(new Vector2( PresetSize, -PresetSize2));
                    break;
            }
        }

        private void Reset()
        {
            _vertices = new List<Vector2>();
            _fixedSpawnPoints = new List<FixedSpawnPoint>();
            ApplyPreset();
            SnapPreviewToArea();
            _previewEulerDeg = Vector3.zero;
            _spawnEulerDeg = Vector3.zero;
        }

        private void OnValidate()
        {
            if (_vertices == null) _vertices = new List<Vector2>();
            if (_vertices.Count < 3) ApplyPreset();
            if (_fixedSpawnPoints == null) _fixedSpawnPoints = new List<FixedSpawnPoint>();
            if (!_previewInitialized)
                SnapPreviewToArea();
        }

        public bool TrySampleSpawn(
            IReadOnlyList<Vector3> avoidPositions,
            HashSet<int> claimedFixedIndices,
            out Vector3 worldGroundPos,
            out Quaternion rotation)
        {
            if (_fixedSpawnPoints != null && _fixedSpawnPoints.Count > 0)
            {
                List<int> available = null;
                for (int i = 0; i < _fixedSpawnPoints.Count; i++)
                {
                    if (claimedFixedIndices != null && claimedFixedIndices.Contains(i)) continue;
                    (available ??= new List<int>()).Add(i);
                }

                if (available != null && available.Count > 0)
                {
                    int pickIdx = available[UnityEngine.Random.Range(0, available.Count)];
                    var fp = _fixedSpawnPoints[pickIdx];
                    if (TryResolveGround(fp.WorldXZ, out var fixedGround))
                    {
                        claimedFixedIndices?.Add(pickIdx);
                        worldGroundPos = fixedGround;
                        rotation = Quaternion.Euler(fp.EulerDeg);
                        return true;
                    }
                    Debug.LogWarning(
                        $"[PlayerSpawnArea] Fixed spawn point #{pickIdx} has no ground beneath " +
                        $"(area Y={transform.position.y}, slot XZ={fp.WorldXZ}). Falling back to random.",
                        this);
                }
            }

            rotation = SpawnRotation;
            return TrySamplePosition(avoidPositions, out worldGroundPos);
        }

        public bool TrySamplePosition(IReadOnlyList<Vector3> avoid, out Vector3 worldGroundPos)
        {
            worldGroundPos = default;
            if (_vertices == null || _vertices.Count < 3) return false;

            Rect bounds = GetLocalBounds();
            const int maxAttempts = 100;

            for (int a = 0; a < maxAttempts; a++)
            {
                float lx = bounds.xMin + UnityEngine.Random.value * bounds.width;
                float ly = bounds.yMin + UnityEngine.Random.value * bounds.height;
                var local = new Vector2(lx, ly);
                if (!ContainsLocalXZ(local)) continue;

                Vector3 worldXZ = LocalToWorld(local);
                if (ObstaclePadding > 0f && Physics.CheckSphere(
                    new Vector3(worldXZ.x, transform.position.y, worldXZ.z), ObstaclePadding,
                    ~0, QueryTriggerInteraction.Ignore))
                    continue;

                var origin = new Vector3(worldXZ.x, transform.position.y + 0.1f, worldXZ.z);
                if (!Physics.Raycast(origin, Vector3.down, out var hit, MaxBelowDepth + 0.1f,
                        ~0, QueryTriggerInteraction.Ignore))
                    continue;

                bool tooClose = false;
                if (avoid != null)
                {
                    for (int i = 0; i < avoid.Count; i++)
                        if (Vector3.Distance(avoid[i], hit.point) < MinSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                worldGroundPos = hit.point;
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawOriginMarker(false);
            if (ShowContour) DrawSurfaceContour();
            if (ShowPreview) DrawPreviewGhost();
            if (ShowFixedPoints) DrawFixedPointGhosts();
        }

        private void OnDrawGizmosSelected()
        {
            DrawOriginMarker(true);
            if (ShowContour) DrawSurfaceContour();
            if (ShowHeatmap) DrawHeatmap();
            if (ShowVertices) DrawVertexDots();
            if (ShowPreview) DrawPreviewGhost();
            if (ShowFixedPoints) DrawFixedPointGhosts();
        }

        private void DrawOriginMarker(bool selected)
        {
            Gizmos.color = selected ? Color.yellow : new Color(1f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
        }

        private struct ContourPoint { public Vector3 Pt; public bool Valid; public bool Blocked; }

        private void DrawSurfaceContour()
        {
            int n = VertexCount;
            if (n < 3) return;
            int sub = Mathf.Max(2, ContourSubSamples);

            int total = n * sub;
            var pts = new ContourPoint[total];

            int idx = 0;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector2 a = _vertices[i], b = _vertices[next];
                for (int s = 0; s < sub; s++)
                {
                    float t = s / (float)sub;
                    Vector2 local = Vector2.Lerp(a, b, t);
                    Vector3 worldXZ = LocalToWorld(local);
                    var origin = new Vector3(worldXZ.x, transform.position.y + 0.1f, worldXZ.z);
                    bool valid = Physics.Raycast(origin, Vector3.down, out var hit,
                        MaxBelowDepth + 0.1f, ~0, QueryTriggerInteraction.Ignore);
                    Vector3 p = valid ? hit.point : worldXZ;
                    bool blocked = false;
                    if (valid && ObstaclePadding > 0f)
                    {
                        blocked = Physics.CheckSphere(
                            new Vector3(worldXZ.x, transform.position.y, worldXZ.z),
                            ObstaclePadding, ~0, QueryTriggerInteraction.Ignore);
                    }
                    pts[idx++] = new ContourPoint { Pt = p, Valid = valid, Blocked = blocked };
                }
            }

            for (int i = 0; i < total; i++)
            {
                int j = (i + 1) % total;
                if (!pts[i].Valid || !pts[j].Valid) continue;
                Gizmos.color = (pts[i].Blocked || pts[j].Blocked) ? ContourBlockedColor : ContourColor;
                Gizmos.DrawLine(pts[i].Pt, pts[j].Pt);
            }
        }

        private void DrawHeatmap()
        {
            if (VertexCount < 3) return;
            Rect bounds = GetLocalBounds();
            float step = Mathf.Max(0.1f, HeatmapSpacing);
            float dotSize = step * 0.4f;
            for (float lx = bounds.xMin; lx <= bounds.xMax + 0.001f; lx += step)
            {
                for (float ly = bounds.yMin; ly <= bounds.yMax + 0.001f; ly += step)
                {
                    var local = new Vector2(lx, ly);
                    if (!ContainsLocalXZ(local)) continue;
                    Vector3 worldXZ = LocalToWorld(local);
                    var origin = new Vector3(worldXZ.x, transform.position.y + 0.1f, worldXZ.z);
                    if (!Physics.Raycast(origin, Vector3.down, out var hit, MaxBelowDepth + 0.1f,
                            ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    bool clear = ObstaclePadding <= 0f || !Physics.CheckSphere(
                        new Vector3(worldXZ.x, transform.position.y, worldXZ.z),
                        ObstaclePadding, ~0, QueryTriggerInteraction.Ignore);
                    Gizmos.color = clear ? HeatmapValidColor : HeatmapBlockedColor;
                    Gizmos.DrawCube(hit.point + Vector3.up * 0.02f, new Vector3(dotSize, 0.02f, dotSize));
                }
            }
        }

        private void DrawVertexDots()
        {
            Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.95f);
            for (int i = 0; i < _vertices.Count; i++)
            {
                var world = LocalToWorld(_vertices[i]);
                world.y = transform.position.y;
                Gizmos.DrawSphere(world, 0.1f);
            }
        }

        private void DrawPreviewGhost()
        {
            if (!TryResolveGround(_previewWorldXZ, out var ground))
            {
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
                Gizmos.DrawWireSphere(new Vector3(_previewWorldXZ.x, transform.position.y, _previewWorldXZ.y), 0.3f);
                return;
            }
            DrawGhostPlayer(ground, PreviewRotation, GhostColor);
        }

        private void DrawFixedPointGhosts()
        {
            if (_fixedSpawnPoints == null) return;
            for (int i = 0; i < _fixedSpawnPoints.Count; i++)
            {
                var fp = _fixedSpawnPoints[i];
                if (!TryResolveGround(fp.WorldXZ, out var ground))
                {
                    Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
                    Gizmos.DrawWireSphere(new Vector3(fp.WorldXZ.x, transform.position.y, fp.WorldXZ.y), 0.3f);
                    continue;
                }
                DrawGhostPlayer(ground, Quaternion.Euler(fp.EulerDeg), FixedGhostColor);
            }
        }

        private void DrawGhostPlayer(Vector3 footPos, Quaternion rotation, Color tint)
        {
            float centerLift = GetGhostCenterLift();
            Vector3 centerPos = footPos + rotation * (Vector3.up * centerLift);

            if (_previewPlayerPrefab == null)
            {
                Gizmos.color = tint;
                DrawWireCapsule(footPos + Vector3.up * centerLift, 0.5f, 2f);
            }
            else
            {
                var mfs = _previewPlayerPrefab.GetComponentsInChildren<MeshFilter>(true);
                if (mfs != null && mfs.Length > 0)
                {
                    var prefabRoot = _previewPlayerPrefab.transform;
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.color = new Color(tint.r, tint.g, tint.b, tint.a * 0.55f);
                    foreach (var mf in mfs)
                    {
                        if (mf == null || mf.sharedMesh == null) continue;
                        Matrix4x4 localToPrefab = prefabRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        Matrix4x4 worldOfGhost = Matrix4x4.TRS(centerPos, rotation, Vector3.one);
                        Gizmos.matrix = worldOfGhost * localToPrefab;
                        Gizmos.DrawMesh(mf.sharedMesh, 0);
                    }
                    Gizmos.matrix = prevMatrix;
                }
            }

            Vector3 arrowStart = footPos + Vector3.up * 0.05f;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 arrowEnd = arrowStart + forward * 1.2f;
            Gizmos.color = GhostArrowColor;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Gizmos.DrawLine(arrowEnd, arrowEnd - forward * 0.25f + right * 0.12f);
            Gizmos.DrawLine(arrowEnd, arrowEnd - forward * 0.25f - right * 0.12f);
        }

        private float GetGhostCenterLift()
        {
            if (_previewPlayerPrefab == null) return 1f;
            var cc = _previewPlayerPrefab.GetComponent<CharacterController>();
            if (cc == null) return 1f;
            return cc.height * 0.5f - cc.center.y;
        }

        private static void DrawWireCapsule(Vector3 center, float radius, float height)
        {
            float halfH = Mathf.Max(0f, height * 0.5f - radius);
            Gizmos.DrawWireSphere(center + Vector3.up * halfH, radius);
            Gizmos.DrawWireSphere(center - Vector3.up * halfH, radius);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var off = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(center + Vector3.up * halfH + off, center - Vector3.up * halfH + off);
            }
        }
#endif
    }
}
