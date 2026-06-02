using MicroWorldNS.Spawners;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MicroWorldNS
{
    /// <summary>
    /// It is the root component of terrain builder, holder of spawners.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.v2z27f424of9")]
    public class MicroWorld : MonoBehaviour
    {
        public event Action<MicroWorld> OnStartSpawn;
        public event Action<MicroWorld> OnBuildCompleted;
        public event Action<MicroWorld, BuildPhase> OnBuildPhaseCompleted;

        [Tooltip("Root seed of random generator.")]
        public int Seed = 0;

        [Tooltip("Blocks terrain rebuilding.")]
        [SerializeField] public bool Locked;
        [Tooltip("List of terran gates.")]
        [SerializeField] public List<GateInfo> Gates = new List<GateInfo>();

        [Space]
        [SerializeField] DebugOptions Debug = new DebugOptions();

        [Space]
        [ShowIf(nameof(Locked), Op = DrawIfOp.AllFalse)]
        [Tooltip("Rebuild the terrain. Also you can press F5 to rebuild a single MicroWorld on scene.")]
        public InspectorButton _BuildInEditor_BuildMicroWorld;
        [ShowIf(nameof(Locked), Op = DrawIfOp.AllFalse)]
        [Tooltip("Remove the terrain and clear related data.")]
        public InspectorButton _Clear;

        [Space(30)]
        [Tooltip("The event is called after the terrain building is completed.")]
        public UnityEvent<MicroWorld> BuildCompleted;

        [Serializable]
        struct DebugOptions
        {
            public bool DrawCells;
            public bool DrawCellInfo;
            public bool DrawEdgeInfo;
        }

        public TerrainSpawner TerrainSpawner { get; set; }
        public MapSpawner MapSpawner { get; set; }
        [field: SerializeField, HideInInspector] public Map Map { get; internal set; }// it will be assigned by MapSpawner
        [field: SerializeField, HideInInspector] public Terrain Terrain { get; internal set; }// it will be assigned by TerrainSpawner
        public TerrainLayersBuilder TerrainLayersBuilder { get; set; }

        public HashSet<ExclusiveGroup> ProcessedExclusiveGroups { get; } = new HashSet<ExclusiveGroup> { };
        public Dictionary<Material, Material> CreatedMaterials { get; } = new Dictionary<Material, Material>();
        public List<BaseSpawner> Spawners { get; private set; }
        public HashSet<string> TakenTerrainExclusiveGroups { get; private set; }

        public ICellGeometry CellGeometry => Map?.Geometry;

        public float CellSizeScale => CellGeometry.Radius / 10f;
        public float DensityScale => Mathf.Lerp(1, CellGeometry.Area / 260f, Preferences.Instance.ScaleSpawnCountProportionallyToCellSize);
        public float LandscapeHeightScale => Mathf.Lerp(1, CellGeometry.Area / 260f, Preferences.Instance.ScaleLandscapeNoiseAmplProportionallyToCellSize);
        public float DetailResolutionScale { get; internal set; }// it will assigned by TerrainSpawner

        [field: SerializeField, HideInInspector] public float WaterLevel { get; internal set; }
        public bool IsBuilt { get; private set; }

        BaseSpawner currentSpawner;
        Queue<BaseSpawner> queueOfAddedSpawners = new Queue<BaseSpawner>();

        #region Public Build methods

        public void BuildInEditor()
        {
            if (Preferences.Instance.LogFeatures.HasFlag(LogFeatures.LogBuildTime))
                Timing.Start("BuildInEditor");

            var e = BuildInternal();
            while (e.Enumerate()) ;

            // activate terrain
            Terrain.gameObject.SetActive(true);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
            if (Preferences.Instance.LogFeatures.HasFlag(LogFeatures.LogBuildTime))
                Timing.Stop();
        }

        /// <summary> Builds MicroWorld and waits while building is completed. </summary>
        public void BuildAndWait(bool activateAfterBuilt = false )
        {
            var e = BuildInternal();
            while (e.Enumerate()) ;

            if (activateAfterBuilt)
                Terrain?.gameObject.SetActive(true);
        }

        /// <summary> Starts MicroWorld building in background mode. Call onCompleted callback after building is completed. </summary>
        public void BuildAsync(Action<MicroWorld> onCompleted = null, bool activateAfterBuilt = false)
        {
            EnqueueToBuild(this);
            OnBuildCompleted += Callback;

            void Callback(MicroWorld mw)
            {
                if (activateAfterBuilt)
                    Terrain?.gameObject?.SetActive(true);

                onCompleted?.Invoke(mw);
                OnBuildCompleted -= Callback;
            }
        }

        /// <summary> Starts MicroWorld building in background mode. </summary>
        public void BuildAsync(bool activateAfterBuilt)
        {
            BuildAsync(null, activateAfterBuilt);
        }

        /// <summary> Adds MicroWorld into build queue. It is suitable if you need to build multiple MicroWorlds in background mode. </summary>
        public static void EnqueueToBuild(MicroWorld world)
        {
            world.IsBuilt = false;
            queueToBuild.Enqueue(world);
        }

        /// <summary> Forces the entire MicroWorld build queue to be executed in the current frame. The approach if you need to urgently complete the background MicroWorld build queue. </summary>
        public static void FlushBuild()
        {
            flushBuild = true;
        }

        #endregion

        /// <summary> Removes terrain and clears all build-related data. </summary>
        public void Clear()
        {
            if (Locked)
                return;
            ClearInternal();
        }

        /// <summary> 
        /// Call this method to add spawners during build process (from Build method of current spawner).
        /// This method will ignore spawners whose Order is less than the order of the current spawner.
        /// Interface IExclusive does not work for spawners added this way.
        /// </summary>
        public void AddSpawnerRuntime(BaseSpawner spawner)
        {
            if (currentSpawner == null)
                throw new Exception($"The {nameof(AddSpawnerRuntime)} method can only be called during the build process.");

            if (!IsSpawnerAllowed(spawner)) 
                return;

            if (spawner.Order < currentSpawner.Order)
            {
                UnityEngine.Debug.LogWarning($"Spawner {spawner} can not be added to spawner list becauase its Order less than Order of current spawner.");
                return;
            }

            queueOfAddedSpawners.Enqueue(spawner);
        }

        #region Async building
        static Queue<MicroWorld> queueToBuild = new Queue<MicroWorld>();

        const bool DelayedFlush = true;
        private static bool flushBuild = false;

        [RuntimeInitializeOnLoadMethod]
        static void StartBuildLoop()
        {
            Dispatcher.OnStart += () =>
            {
                Dispatcher.StartCoroutine(Loop());
            };

            IEnumerator Loop()
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                while (true)
                {
                    sw.Restart();
                    while (queueToBuild.TryDequeue(out var builder))
                    {
                        if (builder == null || builder.gameObject == null)
                            continue;

                        var e = builder.BuildInternal();
                        while (true)
                        {
                            try
                            {
                                if (!e.Enumerate())
                                    break;
                            }
                            catch (Exception)
                            {
                                break;
                            }

                            // is free time in this frame => continue execute builder
                            if (sw.ElapsedMilliseconds < Preferences.Instance.MaxBuildDutyPerFrameInMs || flushBuild) continue;

                            // no time in this frame => goto next frame
                            yield return null;
                            sw.Restart();
                        }
                    }

                    flushBuild = false;
                    yield return null;
                }
            }
        }

        #endregion

        private IEnumerator BuildInternal()
        {
            if (Locked)
                yield break;

            // goto MicroWorld to zero position
            transform.position = Vector3.zero;
            //
            Terrain = null;
            TerrainSpawner = null;
            TerrainLayersBuilder = null;
            Map = null;

            var rnd = new Rnd(Seed);
            ProcessedExclusiveGroups.Clear();
            CreatedMaterials.Clear();

            // destroy before spawned Terrain
            ClearInternal();

            //
            TakenTerrainExclusiveGroups = new HashSet<string>();

            // prepare spawners
            yield return PrepareSpawners(rnd);

            // run spawners
            float runSpawnersStartedAt = Time.realtimeSinceStartup;
            yield return RunSpawners(rnd);
            UnityEngine.Debug.Log($"[MicroWorld] RunSpawners done in {Time.realtimeSinceStartup - runSpawnersStartedAt:0.00}s");

            // flush terrain layers
            if (TerrainLayersBuilder != null)
            {
                if (!Terrain.gameObject.activeInHierarchy && Application.isPlaying && DelayedFlush)
                {
                    // delayed flush - after terrain activation
                    var launcher = Terrain.gameObject.GetOrAddComponent<OnActivatedLauncher>();
                    launcher.OnActivated += () =>
                    {
                        var en = TerrainLayersBuilder.FlushTerrainLayers();
                        while (en.Enumerate()) ;
                    };
                }
                else
                {
                    float flushStartedAt = Time.realtimeSinceStartup;
                    // flush immediately
                    yield return TerrainLayersBuilder.FlushTerrainLayers();
                    UnityEngine.Debug.Log($"[MicroWorld] FlushTerrainLayers done in {Time.realtimeSinceStartup - flushStartedAt:0.00}s");
                }
            }

            // build variants
            if (Terrain)
            {
                float variantsStartedAt = Time.realtimeSinceStartup;
                Variant.Build(Terrain.gameObject, rnd.GetBranch("Variants"));
                UnityEngine.Debug.Log($"[MicroWorld] Variant.Build done in {Time.realtimeSinceStartup - variantsStartedAt:0.00}s");
            }

            yield return null;

            // ===== finish stuffs

            // call spawners finish phase
            foreach (var spawner in Spawners)
            {
                float finishStartedAt = Time.realtimeSinceStartup;
                spawner.OnBuildCompleted();
                UnityEngine.Debug.Log($"[MicroWorld] OnBuildCompleted for {spawner.GetType().Name} ({spawner.name}) done in {Time.realtimeSinceStartup - finishStartedAt:0.00}s");
                yield return null;
            }

            // Create Materials holder
            // This is only needed to show the editors of the created materials in the inspector.
            if (Terrain != null)
            {
                var matHolder = Terrain.GetOrAddComponent<MeshRenderer>();
#if UNITY_2022_2_OR_NEWER
                matHolder.SetSharedMaterials(CreatedMaterials.Values.ToList());
#else
                matHolder.sharedMaterials = CreatedMaterials.Values.ToArray();
#endif
                matHolder.forceRenderingOff = true;
            }

            IsBuilt = true;

            yield return OnPhaseCompleted(BuildPhase.BuildCompleted);
            OnBuildCompleted?.Invoke(this);

            // destroy Manipulator
            Manipulator.Destroy();

            BuildCompleted?.Invoke(this);

            yield return null;
        }

        private IEnumerator PrepareSpawners(Rnd rnd)
        {
            // prepare spawners list
            var spawnerHolders = new List<Transform>();

            if (spawnerHolders.Count == 0)
                spawnerHolders.Add(this.transform);

            // get all enabled spawners
            Spawners = new List<BaseSpawner>();
            foreach (var spawner in spawnerHolders.SelectMany(t => t.GetComponentsInChildren<BaseSpawner>()))
                if (IsSpawnerAllowed(spawner))
                    Spawners.Add(spawner);

            // sort by order (preserve hierarchy order)
            Spawners = Spawners.OrderBy(a => a.Order).ToList();

            // select one spawner from exclusive groups
            for (int i = 0; i < Spawners.Count; i++)
            {
                if (Spawners[i] is IExclusive es)
                    Spawners.SelectOneOfExclusiveGroup(i, rnd);
            }

            // call event OnStartSpawn after spawner list is ready
            yield return OnPhaseCompleted(BuildPhase.SpawnersListIsReady);
            OnStartSpawn?.Invoke(this);

            yield return null;

            // prepare spawners
            foreach (var spawner in Spawners)
                yield return spawner.Prepare(this);

            yield return OnPhaseCompleted(BuildPhase.SpawnersArePrepared);
        }

        private bool IsSpawnerAllowed(BaseSpawner spawner)
        {
            if (!spawner.gameObject.activeSelf || !spawner.enabled)
                return false;

            return true;
        }

        private IEnumerator RunSpawners(Rnd rnd)
        {
            currentSpawner = null;
            queueOfAddedSpawners.Clear();

            // run spawners
            var cellSpawnersAreDone = false;
            for (int i = 0; i < Spawners.Count; i++)
            {
                currentSpawner = Spawners[i];

                // Is CellSpawner? => call all CellSpawners by cells
                if (currentSpawner is CellSpawner)
                {
                    if (cellSpawnersAreDone)
                        continue;
                    // call ALL cell spawners as one for each cell
                    cellSpawnersAreDone = true;
                    float cellSpawnersStartedAt = Time.realtimeSinceStartup;
                    yield return BuildCellSpawners(Spawners.OfType<CellSpawner>(), rnd.GetBranch("Cells"));
                    UnityEngine.Debug.Log($"[MicroWorld] CellSpawners batch done in {Time.realtimeSinceStartup - cellSpawnersStartedAt:0.00}s");
                }
                else
                {
                    // call spawner
                    float spawnerStartedAt = Time.realtimeSinceStartup;
                    UnityEngine.Debug.Log($"[MicroWorld] Building spawner {currentSpawner.GetType().Name} ({currentSpawner.name})");
                    yield return currentSpawner.Build(this);
                    UnityEngine.Debug.Log($"[MicroWorld] Spawner {currentSpawner.GetType().Name} ({currentSpawner.name}) done in {Time.realtimeSinceStartup - spawnerStartedAt:0.00}s");
                }

                // process new runtime added spawners
                if (queueOfAddedSpawners.Count > 0)
                    yield return AddNewSpawnersRuntime();
            }

            currentSpawner = null;
        }

        private IEnumerator BuildCellSpawners(IEnumerable<CellSpawner> spawners, Rnd rnd)
        {
            foreach (var spawner in spawners)
            {
                yield return spawner.Prepare(this, rnd.GetBranch(spawner.name, 27433));
            }

            var cellInfo = new CellBuildInfo();

            //enumerate cells
            foreach (var hex in rnd.GetBranch(921).Shuffle(Map.AllHex()))
            {
                cellInfo.Clear();
                cellInfo.Hex = hex;

                // copy TakenAreas from prev spawners
                var cell = Map[hex];
                if (cell != null && cell.TakenAreas != null)
                {
                    cellInfo.TakenAreas.AddRange(cell.TakenAreas);
                    cellInfo.RoadSegments.AddRange(cell.TakenAreas.Where(a => a.Type == TakenAreaType.Road));
                }

                // build spawners for the cell
                foreach (var spawner in spawners)
                    yield return spawner.BuildCell(this, cellInfo);
            }
        }

        public class CellBuildInfo
        {
            public HashSet<Vector2> TakenPoints = new HashSet<Vector2>();
            public int HaltonStart;
            public Vector2Int Hex;
            public HashSet<string> TakenExclusiveGroups = new HashSet<string>();
            public HashSet<(int iEdge, string group)> TakenExclusiveSectors = new HashSet<(int iEdge, string group)>();
            public List<TakenArea> TakenAreas = new List<TakenArea>();
            public List<TakenArea> RoadSegments = new List<TakenArea>();

            internal void Clear()
            {
                TakenPoints.Clear();
                TakenExclusiveGroups.Clear();
                TakenAreas.Clear();
                RoadSegments.Clear();
            }
        }

        private void ClearInternal()
        {
            // destroy before spawned Terrain
            foreach (var ter in GetComponentsInChildren<Terrain>(true))
                Helper.DestroySafe(ter?.gameObject);

            if (TerrainSpawner && TerrainSpawner.Slopes)
            {
                Helper.DestroySafe(TerrainSpawner.Slopes);
                TerrainSpawner.Slopes = null;
            }

            Map = null;
            Terrain = null;
            MapSpawner = null;
            TerrainSpawner = null;
            TerrainLayersBuilder = null;
            IsBuilt = false;
            TakenTerrainExclusiveGroups = null;
        }

        private IEnumerator AddNewSpawnersRuntime()
        {
            foreach (var spawner in queueOfAddedSpawners)
            {
                // insert new spawner into Spawners list
                for (int i = this.Spawners.Count - 1; i >= 0; i--)
                {
                    if (Spawners[i].Order <= spawner.Order)
                    {
                        Spawners.Insert(i + 1, spawner);
                        break;
                    }
                }
            }

            // call Prepare for new spawners
            foreach (var spawner in queueOfAddedSpawners)
            {
                var phaseHandler = spawner as IBuildPhaseHandler;

                // call IBuildPhaseHandler for completed phases
                if (phaseHandler != null)
                {
                    yield return phaseHandler.OnPhaseCompleted(BuildPhase.SpawnersListIsReady);
                }

                // call Prepare
                yield return spawner.Prepare(this);

                // call IBuildPhaseHandler for completed phases
                if (phaseHandler != null)
                {
                    yield return phaseHandler.OnPhaseCompleted(BuildPhase.SpawnersArePrepared);

                    if (TerrainSpawner != null)
                    {
                        yield return phaseHandler.OnPhaseCompleted(BuildPhase.MapCreated);
                        yield return phaseHandler.OnPhaseCompleted(BuildPhase.TerrainCreated);
                    }
                }
            }

            queueOfAddedSpawners.Clear();
        }

        public IEnumerator OnPhaseCompleted(BuildPhase phase)
        {
            // call phase handlers
            foreach (var handler in Spawners.OfType<IBuildPhaseHandler>())
                yield return handler.OnPhaseCompleted(phase);

            yield return null;

            OnBuildPhaseCompleted?.Invoke(this, phase);
        }

        private void OnValidate()
        {
            foreach (var gate in Gates)
                if (gate != null)
                    gate.OnValidate();
        }

        #region Utils
        public Vector2Int PosToHex(Vector3 pos)
        {
            return CellGeometry.PointToHex(pos - transform.position);
        }

        public Vector3 HexToPos(Vector2Int hex)
        {
            var cell = Map[hex];
            var pos = CellGeometry.Center(hex).withSetY(cell.Height);
            return pos + transform.position;
        }
        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!(Debug.DrawCells || Debug.DrawCellInfo || Debug.DrawEdgeInfo))
                return;

            if (UnityEditor.Selection.activeGameObject != gameObject)
                return;

            if (this.Map == null || this.CellGeometry == null)
                return;

            foreach (var hex in this.Map.AllHex())
            {
                Gizmos.color = Map.IsBorderOrOutside(hex) ? Color.red : Color.green;
                var cell = Map[hex];
                if (Debug.DrawCells || Debug.DrawEdgeInfo)
                    CellGeometry.Draw(hex, cell.Height, transform.position);
                if (Debug.DrawCellInfo)
                    UnityEditor.Handles.Label(transform.TransformPoint(CellGeometry.Center(hex) + Vector3.up * cell.Height), $"{hex}\r\n{cell.Type?.Name}, H={cell.Height:0.}");

                if (Debug.DrawEdgeInfo)
                    for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                    {
                        var p = CellGeometry.EdgeCenter(hex, iEdge);
                        var angle = cell.Edges[iEdge].WallAngle;
                        if (angle > 0)
                        {
                            var passage = cell.Edges[iEdge].IsPassage ? "\r\nIsPassage" : "";
                            UnityEditor.Handles.Label(p + Vector3.up * cell.Height, $"{angle: 0.0°}{passage}");
                        }
                    }
            }
#endif
        }
        #endregion
    }

    [Serializable]
    public class GateInfo
    {
        [HideInInspector] public string Name;
        public WorldSide WorldSide;
        [ShowIf(nameof(WorldSide), WorldSide.Custom)]
        public Vector2Int Cell;
        public GateReferenceType ReferenceType = GateReferenceType.MicroWorldPrefab;
        [ShowIf(nameof(ReferenceType), GateReferenceType.MicroWorldPrefab)]
        public MicroWorld Target;
        [ShowIf(nameof(ReferenceType), GateReferenceType.Id)]
        public string TargetId;
        public Gate Gate;
        public GameObject GatePrefab;

        public enum GateReferenceType
        {
            MicroWorldPrefab = 0, 
            Id = 1
        }

        public void OnValidate()
        {
            if (WorldSide == WorldSide.Custom)
                Name = "Gate " + Cell;
            else
                Name = WorldSide + " Gate";

            switch (ReferenceType)
            {
                case GateReferenceType.MicroWorldPrefab: if (Target != null) Name += " to " + Target.name; break;
                case GateReferenceType.Id: if (TargetId.NotNullOrEmpty()) Name += " to " + TargetId; break;
            }
        }
    }

    public enum WorldSide
    {
        North = 0, 
        East = 10, 
        South = 20, 
        West = 30,
        Custom = 40,
    }

    public enum MicroWorldStatus
    {
        IsNotReady, InProgress, Ready, Active, Destroyed
    }

    public struct ExclusiveGroup
    {
        public Vector2Int Cell;
        public int Edge;
        public string Name;

        public ExclusiveGroup(Vector2Int cell, int edge, string name)
        {
            Cell = cell;
            Edge = edge;
            Name = name;
        }
    }

}
