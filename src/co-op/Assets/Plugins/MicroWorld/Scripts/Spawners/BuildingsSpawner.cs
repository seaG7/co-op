using System.Collections;
using UnityEngine;
using MicroWorldNS.MeshBuilderNS;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.Serialization;

namespace MicroWorldNS.Spawners
{
    /// <summary> 
    /// The spawner is designed to create separate buildings or maze-like structures. Usually this spawner works in Ruins type cells. 
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.kw9sntryn2dw")]
    public class BuildingsSpawner : BaseSpawner, IExclusive
    {
        public string ExclusiveGroup => TerrainExclusiveGroup;
        [field: SerializeField] 
        [field: Tooltip("Defines the chance of selecting this spawner among all BuildingsSpawners of MicroWorld instance.")] 
        public float Chance { get; set; } = 1;
        public override int Order => 1200;
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] CellTypes = new string[] { "Ruins" };
        [SerializeField] DrawElement DrawElements = (DrawElement)0xffff ^ DrawElement.Ceil;
        [Header("Walls")]
        [SerializeField] Material WallMaterial;
        [SerializeField] float WallHeight = 6;
        [SerializeField] float WallWidth = 1;
        [SerializeField] float WallSegmentLength = 3;
        [SerializeField] float WallCornerPadding = 1.5f;
        [SerializeField, Range(0, 1)] float InnerPassageWallChance = 0.7f;
        [SerializeField, Range(0, 1)] float InnerFlatPassageWallChance = 0.5f;
        [SerializeField] bool AlwaysMakeWallBeforeDoor = false;
        [Header("Interior Walls")]
        [SerializeField, Range(0, 1)] float InteriorWallChance = 0.5f;
        [SerializeField] float InteriorWallCornerPadding = 2f;
        [Header("Basement")]
        [SerializeField] Material BasementMaterial;
        [SerializeField] float BasementPadding = 1f;
        [SerializeField] float BasementHeight = 2;
        [SerializeField] float BasementLiftUp = 0.01f;
        [Header("Ceil")]
        [SerializeField] Material CeilMaterial;
        [SerializeField] float CeilHeight = 0.1f;
        [SerializeField] float CeilDepth = 0f;
        [SerializeField] float CeilLiftUp = 0f;
        [SerializeField, Range(0, 1)] float CeilChance = 0.5f;
        [SerializeField][Popup(nameof(ProposedCellTypes), true)] string[] CeilCellTypes;
        [Header("Columns")]
        [SerializeField] float ColumnsOverDepth = 0.3f;
        [Header("Windows")]
        [SerializeField, Range(0, 1)] float WallWindowBalance = 0.5f;
        [SerializeField, Range(0, 1)] float OuterWindowsChance = 1f;
        [Header("Balcony")]
        [SerializeField, Range(0, 1), FormerlySerializedAs("InnerPassageBalconyChance")] float InnerPassageFenceChance = 0.5f;
        [SerializeField, Range(0, 1)] float WindowBalconyBalance = 0.2f;
        [SerializeField, Range(0, 1)] float WallBalconyBalance = 0.1f;
        [Header("Steps")]
        [SerializeField] StepsType StepsType = StepsType.Steps;
        [SerializeField] float StepDepth = 0.6f;
        [Header("Other")]
        public string TerrainExclusiveGroup = "BuildingsSpawner";
        [Range(0f, 1f)] public float ChanceInCell = 1;
        [SerializeField, Range(0, 1)] float CyclesChance = 0.1f;
        [SerializeField] float MaxHeightToMakeAddPassage = 3.1f;
        [SerializeField] RangeFloat HeightAboveWater = new RangeFloat(0, 100);
        [SerializeField, Layer] int ColliderLayer = 0;
        [SerializeField] bool SpawnCellsInSeparateMesh = false;
        [Header("Debug")]
        [SerializeField] bool ShowGoodSpawnPoints;

        MeshBuilder meshBuilder;
        HashSet<Vector2Int> processedCorners;
        Cell cell;
        Cell opCell;
        HashSet<Vector2Int> myHexes;
        Rnd cellRnd;
        float coeffElongation;
        float wallDiagonalAlongRadius;

        Dictionary<Vector2Int, CellInfo> cellInfos = new Dictionary<Vector2Int, CellInfo>();
        CellInfo cellInfo;
        List<RoomSpawner> roomSpawners;
        RoomSpawner roomSpawner;
        GeometryElement serviceGeometryElement;

        class CellInfo
        {
            public bool HasCeil;
        }

        [Flags]
        enum DrawElement : UInt16
        {
            None = 0x0,
            Basement = 0x1,
            Ceil = 0x2,
            Columns = 0x4,
            Steps = 0x8,
            ConnectingWalls = 0x10,
            ConnectingColumns = 0x20,
            ColumnElongation = 0x40,
            InteriorWalls = 0x80,
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            CheckTerrainSpawner();

            roomSpawners = Builder.Spawners.OfType<RoomSpawner>().ToList();
            if (roomSpawners.Count == 0)
                throw new ApplicationException("Should be at least one RoomSpawner");


            wallDiagonalAlongRadius = WallWidth / 2f / Mathf.Cos(Mathf.PI / 2 - CellGeometry.InnerAngle / 2);
            coeffElongation = Mathf.Tan(Mathf.PI / 2 - CellGeometry.InnerAngle / 2f);
            meshBuilder = new MeshBuilder();

            serviceGeometryElement = new GeometryElement();
            serviceGeometryElement.Prepare(null);

            InitMaterials();

            var rnd = rootRnd.GetBranch(83);
            myHexes = Map.AllHex().Where(h => CellTypes.CheckCellType(Map[h].Type.Name)).Where(_ => rnd.Bool(ChanceInCell)).ToHashSet();

            rnd = rootRnd.GetBranch(9437);
            // prepare cell infos
            foreach (var h in myHexes)
            {
                var cellInfo = new CellInfo();
                cellInfo.HasCeil = rnd.Bool(CeilChance) && DrawElements.HasFlag(DrawElement.Ceil) && CeilCellTypes.CheckCellType(Map[h].Type.Name);
                cellInfos[h] = cellInfo;
            }

            // build cells
            processedCorners = new HashSet<Vector2Int>();
            var i = 0;
            foreach (var hex in myHexes)
            {
                cellRnd = rnd.GetBranch(i++);
                BuildCell(hex);
                yield return null;
            }

            // remomve spawn point in taken areas
            foreach (var hex in myHexes)
                RemoveSpawnPointInTakenAreas(hex);

            if (!SpawnCellsInSeparateMesh)
                BuildMesh("Building");
        }

        private void BuildMesh(string name)
        {
            var go = meshBuilder.Build(Builder);
            go.layer = ColliderLayer;
            go.name = name;
            meshBuilder.SubMeshes.Clear();
            meshBuilder.Elements.Clear();
            meshBuilder.GameObjects.Clear();
        }

        private void InitMaterials()
        {
            if (WallMaterial == null)
                WallMaterial = Resources.Load<Material>("Terrain");
            meshBuilder.DefaultMaterial = WallMaterial;
        }

        private void RemoveSpawnPointInTakenAreas(Vector2Int hex)
        {
            var cell = Map[hex];
            if (cell.TakenAreas == null || cell.GoodSpawnPoints == null)
                return;
            if (cell.TakenAreas.Count == 0 || cell.GoodSpawnPoints.Count == 0)
                return;

            var r = WallSegmentLength / 2;
            var newList = new List<SpawnPoint>(cell.GoodSpawnPoints.Count);
            foreach (var sp in cell.GoodSpawnPoints)
            {
                var p = sp.Pos + sp.Normal * r;
                if (!cell.TakenAreas.HasIntersection(p.ToVector2(), r))
                    newList.Add(sp);
            }
            cell.GoodSpawnPoints = newList;
        }

        private void BuildCell(Vector2Int hex)
        {
            var cornersCount = CellGeometry.CornersCount;
            cell = Map[hex];
            cellHex = hex;
            cellInfo = cellInfos[hex];

            if (cell.GoodSpawnPoints == null)
                cell.GoodSpawnPoints = new List<SpawnPoint>();

            // check water condition
            if (!HeightAboveWater.InRange(cell.Height - Builder.WaterLevel))
                return;

            // get room spawner
            roomSpawner = cellRnd.GetRnd(roomSpawners);

            // build edges
            for (int iEdge = 0; iEdge < cornersCount; iEdge++)
            {
                BuildEdge(hex, iEdge);
            }

            // build corners
            if (DrawElements.HasFlag(DrawElement.Columns))
                for (int iCorner = 0; iCorner < cornersCount; iCorner++)
                    BuildCornerIfNotBuilt(hex, iCorner, false);

            // build interior
            BuildInteriorWalls(hex);

            // build basement
            BuildBasementAndCeil(hex);

            // build mesh
            if (SpawnCellsInSeparateMesh)
                BuildMesh("Building " + hex);
        }

        private void BuildInteriorWalls(Vector2Int hex)
        {
            if (CellGeometry.CornersCount == 4)
                return;//TODO:

            var isHex = CellGeometry.CornersCount == 6;
            var iCorner = cellRnd.Int(CellGeometry.CornersCount);
            var iOpCorner = CellGeometry.OppositeCorner(iCorner);
            var e0 = CellGeometry.Corner(hex, iCorner).withSetY(cell.Height + BasementLiftUp);
            var e1 = CellGeometry.Corner(hex, iOpCorner).withSetY(cell.Height + BasementLiftUp);
            var pos = (e0 + e1) / 2;
            var dir = (e1 - e0).normalized;
            e0 += WallWidth / 3 * dir;
            e1 -= WallWidth / 3 * dir;
            var len = (e1 - e0).magnitude;
            var normal = dir.rotateAroundAxis(Vector3.up, 90);
            var p0 = Vector3.Lerp(e0, e1, cellRnd.Float(0.1f, 0.4f));
            var doorWidth = WallSegmentLength;// cellRnd.Float(InteriorDoorWidth.Min, InteriorDoorWidth.Max);
            var p1 = p0 + dir * doorWidth;

            // check chance
            if (!cellRnd.Bool(InteriorWallChance) || !DrawElements.HasFlag(DrawElement.InteriorWalls))
            {
                // add good spawn points
                var steps2 = len / WallSegmentLength;
                for (int i = 1; i < steps2 - 1; i++)
                {
                    var p = e0 + dir * i * WallSegmentLength;
                    cell.GoodSpawnPoints.Add(new SpawnPoint(SpawnPointType.Interior, p + normal * WallWidth / 2, normal, cellHex));
                    cell.GoodSpawnPoints.Add(new SpawnPoint(SpawnPointType.Interior, p - normal * WallWidth / 2, -normal, cellHex));
                }

                return;
            }

            // add taken areas
            var r = WallWidth / 2 + 2;
            cell.TakenAreas.Add(new TakenArea((e0 + dir * r).ToVector2(), r));
            cell.TakenAreas.Add(new TakenArea((e1 - dir * r).ToVector2(), r));
            cell.TakenAreas.Add(new TakenArea(((p0 + p1) / 2).ToVector2(), WallWidth + doorWidth / 2));

            // add good spawn points along wall
            var steps = len / WallSegmentLength;
            for (int i = 1; i < steps - 1; i++)
            {
                var p = e0 + dir * i * WallSegmentLength;
                cell.GoodSpawnPoints.Add(new SpawnPoint(SpawnPointType.InteriorWall, p + normal * WallWidth / 2, normal, cellHex));
                cell.GoodSpawnPoints.Add(new SpawnPoint(SpawnPointType.InteriorWall, p - normal * WallWidth / 2, -normal, cellHex));
            }

            edgeType = EdgeType.Interior;
            BuildWall(e0, e1, normal);
        }

        private void BuildWall(Vector3 e0, Vector3 e1, Vector3 normal)
        {
            var wallCornerPadding = edgeType == EdgeType.Interior ? InteriorWallCornerPadding : WallCornerPadding;
            dir = (e1 - e0);
            var wallLen = dir.magnitude;
            dir /= wallLen;
            var p0 = e0 + dir * wallCornerPadding;
            var p1 = e1 - dir * wallCornerPadding;

            // draw edge elements
            BuildSegment(e0, e1, normal, GeometryPlace.Edge);

            // build top cornice
            BuildSegment(e0, e1, normal, GeometryPlace.Cornice);

            // divide wall on segments
            var wallLengthWithoutPaddings = wallLen - wallCornerPadding * 2;
            var segCount = Mathf.Max(1, Mathf.RoundToInt(wallLengthWithoutPaddings / WallSegmentLength));
            var segLength = wallLengthWithoutPaddings / segCount;

            segTypes = CalcSegTypes(segCount, EdgeType.Interior, false, true);

            // build wall paddings
            if (wallCornerPadding > 0)
            {
                BuildSegment(e0, p0, normal, GeometryPlace.WallPadding);
                BuildSegment(p1, e1, normal, GeometryPlace.WallPadding);
            }

            for (int i = 0; i < segTypes.Length; i++)
            {
                var segType = segTypes[i];

                // build wall segment
                var t = (float)i / segCount;
                var P0 = Vector3.Lerp(p0, p1, (t + 0 / segCount));
                var P1 = Vector3.Lerp(p0, p1, (t + 1f / segCount));

                switch (segType)
                {
                    case SegType.Wall:
                        BuildSegment(P0, P1, normal, GeometryPlace.Wall, -1, i);
                        break;
                    case SegType.Window:
                        BuildSegment(P0, P1, normal, GeometryPlace.Window, -1, i);
                        break;
                    case SegType.Door:
                        BuildSegment(P0, P1, normal, GeometryPlace.Door, -1, i);
                        break;
                    case SegType.Balcony:
                        BuildSegment(P0, P1, normal, GeometryPlace.Balcony, -1, i);
                        break;
                }
            }
        }

        private void BuildCornerIfNotBuilt(Vector2Int hex, int iCorner, bool isConnector)
        {
            var pos = CellGeometry.Corner(hex, iCorner);
            var key = Vector2Int.RoundToInt(pos.ToVector2());
            if (!processedCorners.Add(key))// already built?
                return;

            var drawConnectors = DrawElements.HasFlag(DrawElement.ConnectingColumns) | DrawElements.HasFlag(DrawElement.Columns);

            if (isConnector && !drawConnectors)
                return;
            if (!isConnector && !DrawElements.HasFlag(DrawElement.Columns))
                return;

            var cellsAroundCorner = CellGeometry.CellsAroundCorner(hex, iCorner).ToArray();
            var minH = cellsAroundCorner.Min(h => Map[h].Height) + BasementLiftUp;
            var maxH = cellsAroundCorner.Where(h => myHexes.Contains(h)).Max(h => Map[h].Height) + BasementLiftUp;

            var connectorH = maxH - minH + WallHeight;
            //var connectorWidth = WallWidth + 0.1f;
            var connectorWidth = WallWidth + ColumnsOverDepth * 2;
            if (isConnector)
                connectorWidth = Mathf.Max(connectorWidth, WallWidth * 2f);
            pos.y = minH;
            Vector3 n = Vector3.forward;
            if (CellGeometry.CornersCount == 6)
            {
                var n0 = CellGeometry.EdgeNormal(iCorner - 1);
                var n1 = CellGeometry.EdgeNormal(iCorner);
                n = (n1 + n0).normalized;
            }

            // column
            pos.y = maxH;
            BuildCorner(pos, connectorWidth, n, GeometryPlace.Corner, iCorner);

            // column elongation
            if (DrawElements.HasFlag(DrawElement.ColumnElongation))
            {
                pos.y = minH;
                var A = 0.05f;
                BuildServiceWall(pos, new Vector3(connectorWidth + A, maxH - minH, connectorWidth + A), n, BasementMaterial);
            }
        }

        EdgeType GetEdgeType(Vector2Int hex, Cell cell, Vector2Int opHex, Cell opCell)
        {
            var isPassage = cell.Parent == opHex || opCell.Parent == hex;

            if (!myHexes.Contains(opHex))
                return isPassage ? EdgeType.OuterPassage : EdgeType.Outer;

            if (isPassage)
                return EdgeType.InnerPassage;

            if ((maxY - minY) < WallHeight)
                return EdgeType.InnerOverlap;

            return EdgeType.InnerSplit;
        }

        enum EdgeType
        {
            InnerPassage, OuterPassage, InnerOverlap, InnerSplit, Outer, Interior
        }

        private void BuildBasementAndCeil(Vector2Int hex)
        {
            var type = CellGeometry.CornersCount == 4 ? MeshBuilder.ElementType.Box : MeshBuilder.ElementType.Hex;
            var normal = CellGeometry.EdgeNormal(0);
            var h = BasementHeight + BasementLiftUp;
            var pos = CellGeometry.Center(hex).withSetY(cell.Height - BasementHeight);
            var basementWidth = CellGeometry.InnerRadius * 2 + BasementPadding * 2;

            if (DrawElements.HasFlag(DrawElement.Basement))
                meshBuilder.Elements.Add(new MeshBuilder.Element(pos, normal, new Vector3(basementWidth, h, basementWidth), BasementMaterial, type));

            // ceil
            //if (DrawCeil && (cell.Type == Builder.MapSpawner.GateCellType || cellRnd.Bool(CeilChance)))
            if (cellInfos[hex].HasCeil)
            {
                basementWidth = CellGeometry.InnerRadius * 2 - 0.02f;
                pos.y = cell.Height + WallHeight + BasementLiftUp + CeilLiftUp;
                meshBuilder.Elements.Add(new MeshBuilder.Element(pos, normal, new Vector3(basementWidth + CeilDepth * 2, CeilHeight, basementWidth + CeilDepth * 2), CeilMaterial, type));
            }
        }

        Vector3 dir; 
        EdgeType edgeType;
        Vector2Int cellHex;
        float minY;// min cell height for edge
        float maxY;// max cell height for edge
        Vector2Int opHex;
        bool isLower;// current cell is lower than opposite cell
        SegType[] segTypes;

        private void BuildEdge(Vector2Int hex, int iEdge)
        {
            opHex = CellGeometry.Neighbor(hex, iEdge);
            opCell = Map[opHex];
            // calc min and max cell hights for edge
            if (myHexes.Contains(opHex))
            {
                minY = Mathf.Min(opCell.Height, cell.Height);
                maxY = Mathf.Max(opCell.Height, cell.Height);
            }else
            {
                minY = maxY = cell.Height;
            }

            // calc edge type
            edgeType = GetEdgeType(hex, cell, opHex, opCell);
            isLower = cell.Height < opCell.Height;

            // avoid duplicate wall buiding for inside wall, select cell with lower height
            if (isLower)
            if (edgeType == EdgeType.InnerPassage || edgeType == EdgeType.InnerOverlap || edgeType == EdgeType.InnerSplit)
                return;

            // make passage if needed
            if (edgeType != EdgeType.Outer && edgeType != EdgeType.OuterPassage)
            if (cellRnd.Bool(CyclesChance) && Mathf.Abs(cell.Height - opCell.Height) <= MaxHeightToMakeAddPassage)
                edgeType = EdgeType.InnerPassage;

            // detremine parts to draw
            bool drawSegments = true;
            bool drawFenceOnly = false;
            bool balconyInsteadOfWall = false;
            bool balconyInsteadOfWindow = false;
            bool forcedDrawCornice = false;
            var blindWall = false;
            {
                var isFlatPassage = false;

                if (edgeType == EdgeType.InnerPassage)
                {
                    var diff = Mathf.Abs(opCell.Height - cell.Height);
                    isFlatPassage = diff < 0.1f;

                    drawSegments = cellRnd.Bool(isFlatPassage ? InnerFlatPassageWallChance : InnerPassageWallChance);
                    if (!drawSegments)
                    {
                        var drawFenceInPassage = cellRnd.Bool(InnerPassageFenceChance);
                        if (diff > 2)
                            drawFenceOnly = true;
                        else
                            drawFenceOnly = !isFlatPassage && drawFenceInPassage;
                    }
                }

                // balcony instead of windows
                if (cellRnd.Bool(WindowBalconyBalance))
                    balconyInsteadOfWindow = true;
                // wide balcony (for windows and walls)
                if (cellRnd.Bool(WallBalconyBalance))
                    balconyInsteadOfWall = balconyInsteadOfWindow = true;

                var isSignificantHeightDifference = Mathf.Abs(opCell.Height - cell.Height) > WallHeight / 2;
                if (edgeType != EdgeType.InnerPassage && edgeType != EdgeType.OuterPassage)
                if (isSignificantHeightDifference && (isLower || !cellRnd.Bool(OuterWindowsChance)))
                    blindWall = true;

                if (!isFlatPassage && cellInfo.HasCeil)
                    forcedDrawCornice = true;

                if (isFlatPassage && (cellInfo.HasCeil ^ cellInfos[opHex].HasCeil))
                    forcedDrawCornice = true;

                if (blindWall)
                {
                    drawSegments = true;
                    balconyInsteadOfWall = balconyInsteadOfWindow = false;
                    drawFenceOnly = false;
                }
            }

            // prepare wall
            var normal = CellGeometry.EdgeNormal(iEdge);
            var e0 = CellGeometry.Corner(hex, iEdge + 1);
            var e1 = CellGeometry.Corner(hex, iEdge);
            e0 -= CellGeometry.CornerNormal(iEdge + 1) * wallDiagonalAlongRadius;
            e1 -= CellGeometry.CornerNormal(iEdge) * wallDiagonalAlongRadius;
            var wallLen = (e0 - e1).magnitude;

            // divide wall on segments
            var wallLengthWithoutPaddings = wallLen - WallCornerPadding * 2;
            var segCount = Mathf.Max(1, Mathf.RoundToInt(wallLengthWithoutPaddings / WallSegmentLength));
            var segLength = wallLengthWithoutPaddings / segCount;
            // calc seg type
            segTypes = CalcSegTypes(segCount, edgeType, blindWall, false);

            //
            var Y = edgeType == EdgeType.InnerOverlap || cell.Height > opCell.Height ? maxY : minY;
            if (edgeType == EdgeType.InnerPassage)
                Y = maxY;
            Y += BasementLiftUp;
            e0.y = Y; e1.y = Y;
            dir = (e1 - e0).normalized;
            var p0 = e0 + dir * WallCornerPadding;
            var p1 = e1 - dir * WallCornerPadding;

            if (edgeType == EdgeType.InnerOverlap || edgeType == EdgeType.InnerPassage)
            {
                BuildCornerIfNotBuilt(hex, iEdge, true);
                BuildCornerIfNotBuilt(hex, iEdge + 1, true);
            }

            // build expanded wall base
            if (DrawElements.HasFlag(DrawElement.ConnectingWalls))
            if (edgeType == EdgeType.InnerOverlap || edgeType == EdgeType.InnerSplit)
            {
                var baseWallHeight = Mathf.Min(WallHeight, maxY - minY);
                var pos = (e0 + e1) / 2 + normal * WallWidth;
                pos.y = minY;
                if (baseWallHeight > 0.1f)
                {
                    BuildServiceWall(pos, new Vector3(CellGeometry.EdgeLength, baseWallHeight, WallWidth - 0.01f), normal);
                }
            }

            // build wall between roofs
            if (edgeType == EdgeType.InnerPassage || edgeType == EdgeType.InnerOverlap)
            if (/*cellInfos[hex].HasCeil &&*/ cellInfos[opHex].HasCeil)
            if (!blindWall)
            if (Mathf.Abs(minY - maxY) > 0.1f)
            {
                const float W = 0.5f;
                var baseWallHeight = maxY - minY;
                var pos = (e0 + e1) / 2 + normal * W;
                pos.y = minY + WallHeight + BasementLiftUp + CeilHeight / 2f;
                BuildServiceWall(pos, new Vector3(CellGeometry.EdgeLength, baseWallHeight, W), normal);
            }

            // draw edge elements
            if (drawSegments)
                BuildSegment(e0, e1, normal, GeometryPlace.Edge, iEdge, -1);

            // build top cornice
            if (drawSegments || forcedDrawCornice)
                BuildSegment(e0, e1, normal, GeometryPlace.Cornice, iEdge, -1);

            // build wall paddings
            if (WallCornerPadding > 0 && (drawSegments || drawFenceOnly))
            {
                BuildSegment(e0 - WallWidth / 2 * dir * coeffElongation, p0, normal, GeometryPlace.WallPadding, iEdge, -1);
                BuildSegment(p1, e1 + WallWidth / 2 * dir * coeffElongation, normal, GeometryPlace.WallPadding, iEdge, -1);
            }

            // adjust seg types
            for (int i = 0; i < segCount; i++)
            {
                if (balconyInsteadOfWall && segTypes[i] == SegType.Wall)
                    segTypes[i] = SegType.Balcony;
                if (balconyInsteadOfWindow && segTypes[i] == SegType.Window)
                    segTypes[i] = SegType.Balcony;
            }

            if (AlwaysMakeWallBeforeDoor)
                InsertWallBeforeDoor(segTypes);

            // build wall by segments
            {
                // build segments
                for (int i = 0; i < segCount; i++)
                {
                    var segType = segTypes[i];

                    // build wall segment
                    var t = (float)i / segCount;
                    var P0 = Vector3.Lerp(p0, p1, (t + 0 / segCount));
                    var P1 = Vector3.Lerp(p0, p1, (t + 1f / segCount));

                    if (drawSegments)
                    switch (segType)
                    {
                        case SegType.Wall:
                                BuildSegment(P0, P1, normal, GeometryPlace.Wall, iEdge, i);
                            break;
                        case SegType.Window:
                                BuildSegment(P0, P1, normal, GeometryPlace.Window, iEdge, i);
                            break;
                        case SegType.Door:
                            BuildSegment(P0, P1, normal, GeometryPlace.Door, iEdge, i);
                            BuildSteps(P0, P1, segLength, normal);
                            BuildTakenArea(P0, P1, segLength, normal);
                            break;
                        case SegType.Balcony:
                            BuildSegment(P0, P1, normal, GeometryPlace.Balcony, iEdge, i);
                            break;
                    }

                    if (!drawSegments)
                        switch (segTypes[i])
                        {
                            case SegType.Door: 
                                BuildSteps(P0, P1, segLength, normal);
                                BuildTakenArea(P0, P1, segLength, normal);
                                break;
                            default:
                                if (drawFenceOnly)
                                    BuildSegment(P0, P1, normal, GeometryPlace.Fence, iEdge, i);
                                break;
                        }
                }
            }
        }

        private void BuildTakenArea(Vector3 p0, Vector3 p1, float segLength, Vector3 normal)
        {
            // add taken area
            var pos = (p0 + p1) / 2;
            const float r = 2;
            opCell.TakenAreas.Add(new TakenArea((pos + normal * r).ToVector2(), r));
            cell.TakenAreas.Add(new TakenArea((pos - normal * r).ToVector2(), r));
        }

        private void InsertWallBeforeDoor(SegType[] segTypes)
        {
            var iDoorSegm = Array.IndexOf(segTypes, SegType.Door);
            if (AlwaysMakeWallBeforeDoor && iDoorSegm > 0)
                segTypes[iDoorSegm - 1] = SegType.Wall;
        }

        private SegType[] CalcSegTypes(int segCount, EdgeType edgeType, bool blind, bool allowBalconies)
        {
            var segTypes = new SegType[segCount];
            for (int i = 0; i < segCount; i++)
            {
                segTypes[i] = cellRnd.Bool(WallWindowBalance) ? SegType.Window : SegType.Wall;
                if (blind)
                    segTypes[i] = SegType.Wall;
            }

            if (!blind && allowBalconies)
            {
                if (cellRnd.Bool(WindowBalconyBalance))
                    for (int i = 0; i < segCount; i++)
                    if (segTypes[i] == SegType.Window)
                        segTypes[i] = SegType.Balcony;
            }

            if (edgeType == EdgeType.InnerPassage || edgeType == EdgeType.OuterPassage || edgeType == EdgeType.Interior)
            {
                var iDoorSegm = segCount / 2;
                segTypes[iDoorSegm] = SegType.Door;
            }

            InsertWallBeforeDoor(segTypes);

            return segTypes;
        }

        enum SegType
        {
            Wall, Window, Balcony, Door
        }

        private void BuildServiceWall(Vector3 pos, Vector3 scale, Vector3 normal, Material mat = null)
        {
            var up = Vector3.up;
            var mats = roomSpawner.Materials;
            var dir = normal.rotateAroundAxis(Vector3.up, -90);

            serviceGeometryElement.Build(meshBuilder, pos, dir, up, -normal, scale, 0, roomSpawner.StretchByY, mats);

            if (mat != null)
            {
                var i = meshBuilder.SubMeshes.Count - 1;
                var pair = meshBuilder.SubMeshes[i];
                pair.Item3 = mat;
                meshBuilder.SubMeshes[i] = pair;
            }
        }

        private void BuildCorner(Vector3 pos, float connectorWidth, Vector3 normal, GeometryPlace place, int iEdge)
        {
            if (!roomSpawner.placeToElements.TryGetValue(place, out var elements))
                return;

            var up = Vector3.up;
            var scale = new Vector3(connectorWidth, WallHeight, connectorWidth);
            var dir = normal.rotateAroundAxis(Vector3.up, -90);

            // build elements
            var list = elements.SelectByChance(cellRnd.GetBranch(iEdge, 23));
            foreach (var el in list)
            {
                el.Build(meshBuilder, pos, dir, up, -normal, scale, 0, roomSpawner.StretchByY, roomSpawner.Materials);
            }
        }

        private void BuildSegment(Vector3 P0, Vector3 P1, Vector3 normal, GeometryPlace place, int iEdge = -1, int iSegment = -1)
        {
            if (!roomSpawner.placeToElements.TryGetValue(place, out var elements))
                return;

            var segLength = (P1 - P0).magnitude;
            var isZeroSegment = segLength < 0.01f;
            var dir = isZeroSegment ? normal.rotateAroundAxis(Vector3.up, -90) : (P1 - P0) / segLength;
            var up = Vector3.up;
            var scale = new Vector3(isZeroSegment ? WallSegmentLength : segLength, WallHeight, WallWidth);
            normal *= Mathf.Sign(Vector3.Cross(dir, normal).y);// adj normal to LHS

            var elongation = 0f;
            if (edgeType != EdgeType.Interior)
            if (place == GeometryPlace.Edge || place == GeometryPlace.Cornice)
                elongation = this.coeffElongation;

            BuildingEdgeType type = 0;
            if (edgeType == EdgeType.Outer || edgeType == EdgeType.OuterPassage || edgeType == EdgeType.InnerSplit)
                type |= BuildingEdgeType.Outer;
            if (edgeType == EdgeType.InnerOverlap || edgeType == EdgeType.InnerPassage)
                type |= Mathf.Abs(cell.Height - opCell.Height) < 0.1f ? BuildingEdgeType.InnerFlat : BuildingEdgeType.InnerOverlap;
            if (edgeType == EdgeType.Interior)
                type |= BuildingEdgeType.Interior;

            var isLeft = iSegment <= 0 || segTypes[iSegment - 1] != segTypes[iSegment];
            var isRight = iSegment < 0 || iSegment == segTypes.Length - 1 || segTypes[iSegment + 1] != segTypes[iSegment];
            var isMid = iSegment < 0 || (iSegment < segTypes.Length - 1 && segTypes[iSegment + 1] == segTypes[iSegment]);
            var isCentral = iSegment < 0 || iSegment == segTypes.Length / 2;

            // check conditions and chances
            var filtered = elements.Where(el =>
            {
                switch (el.SegmentOrder)
                {
                    case SegmentOrderType.Central:
                        if (!isCentral) return false;
                        break;
                    case SegmentOrderType.Left:
                        if (!isLeft) return false;
                        break;
                    case SegmentOrderType.Right:
                        if (!isRight) return false;
                        break;
                    case SegmentOrderType.Mid:
                        if (!isMid) return false;
                        break;
                }

                if ((el.EdgeType & type) == 0)
                    return false;

                return true;
            });

            // check chance
            filtered = filtered.SelectByChance(cellRnd.GetRndBranch());

            // build elements
            foreach (var el in filtered)
            {
                el.Build(meshBuilder, (P0 + P1) / 2, dir, up, -normal, scale, elongation, roomSpawner.StretchByY, roomSpawner.Materials);
            }
        }

        private void AddSpawnPoint(Vector3 normal, float wallWidth, Vector3 pos, SpawnPointType type)
        {
            cell.GoodSpawnPoints.Add(new SpawnPoint(type, pos - normal * wallWidth / 2, -normal, cellHex));
        }

        private void BuildSteps(Vector3 P0, Vector3 P1, float segLength, Vector3 normal)
        {
            var diffH = opCell.Height - cell.Height;
            if (edgeType == EdgeType.OuterPassage)
                if (edgeType == EdgeType.OuterPassage && diffH > 0)
                    return;

            if (!DrawElements.HasFlag(DrawElement.Steps))
                return;

            var stepHeight = Preferences.Instance.StepHeight;

            var stepsCount = Mathf.CeilToInt(Math.Abs(diffH) / stepHeight);
            var pos = (P0 + P1) / 2;
            var H = stepsCount * stepHeight;
            pos.y -= H;
            pos += normal * (BasementPadding + WallWidth / 2f);
            var POS = pos;

            //// add taken area
            //if (stepsCount > 0)
            //{
            //    var r = StepDepth * stepsCount / 2;
            //    opCell.TakenAreas.Add(((pos + normal * r).ToVector2(), r * 1.5f));
            //}

            const float A = 2f;
            float elongationWidth = segLength;

            if (StepsType == StepsType.Steps)
            {
                pos += normal * StepDepth / 2;

                var h = H;
                for (int i = 0; i < stepsCount; i++)
                {
                    BuildServiceWall(pos, new Vector3(segLength, h, StepDepth), normal, BasementMaterial);
                    pos += normal * StepDepth;
                    h -= stepHeight;
                }
            }

            if (StepsType == StepsType.Pyramid)
            {
                var dx = StepDepth;// segLength / (stepsCount + 0.1f);
                pos += normal * StepDepth / 2;
                const int B = 2;
                elongationWidth -= B * dx;

                var h = H;
                for (int i = 0; i < stepsCount; i++)
                {
                    BuildServiceWall(pos, new Vector3(segLength + dx * (i - B), h, StepDepth * (i + 1)), normal, BasementMaterial);
                    pos += normal * StepDepth / 2;
                    h -= stepHeight;
                }
            }

            if (StepsType == StepsType.Ramp)
            {
                var el = new MeshBuilder.Element(pos, normal.rotateAroundAxis(Vector3.up, -90), new Vector3(StepDepth * stepsCount, H, segLength), BasementMaterial, MeshBuilder.ElementType.Ramp);
                meshBuilder.Elements.Add(el);
            }

            // steps elongation
            if (!DrawElements.HasFlag(DrawElement.Basement))
                BuildServiceWall(POS - (A / 2) * normal, new Vector3(elongationWidth, H, A), normal, BasementMaterial);
        }

        private void OnValidate()
        {
            //GrassRockBalance = GrassRockBalance.Clamp(0, 1);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var world = GetComponentInParent<MicroWorld>();
            if (world == null || world.Map == null || world.Map.Cells == null)
                return;

            //if (CellGeometry != null)
            //foreach (var hex in world.Map.AllHex())
            //{
            //    for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
            //    {
            //        var e0 = CellGeometry.Corner(hex, iEdge).withSetY(world.Map[hex].Height);
            //        var e1 = CellGeometry.Corner(hex, iEdge + 1).withSetY(world.Map[hex].Height);
            //        Gizmos.DrawLine(e0, e1);

            //        e0 -= CellGeometry.CornerNormal(iEdge) * WallWidth / 2f / Mathf.Cos(Mathf.PI / 2 - CellGeometry.InnerAngle / 2);
            //        e1 -= CellGeometry.CornerNormal(iEdge + 1) * WallWidth / 2f / Mathf.Cos(Mathf.PI / 2 - CellGeometry.InnerAngle / 2);

            //        Gizmos.DrawLine(e0, e1);
            //    }
            //}

            if (ShowGoodSpawnPoints)
                foreach (var hex in world.Map.AllHex())
                {
                    Gizmos.color = Color.yellow;
                    var cell = world.Map[hex];
                    if (cell.GoodSpawnPoints != null)
                        foreach (var sp in cell.GoodSpawnPoints)
                        {
                            Gizmos.DrawWireSphere(sp.Pos, 0.1f);
                            Gizmos.DrawRay(sp.Pos, sp.Normal);
                            //UnityEditor.Handles.Label(sp.Pos, hex + " " + sp.OwnerCellHex);
                        }

                    Gizmos.color = Color.red;
                    if (cell.TakenAreas != null)
                        foreach (var area in cell.TakenAreas)
                        {
                            var pos = area.A.withY(cell.Height + 1);
                            Gizmos.DrawWireSphere(pos, area.Radius);
                            //UnityEditor.Handles.Label(pos, hex.ToString());
                        }
                }
        }
#endif
    }

    [Serializable]
    enum StepsType
    {
        Steps = 0, Ramp = 1, Pyramid = 2
    }

    static class ChanceHelper
    {
        static float[] sumProb = new float[100];
        static List<GeometryElement> elList = new List<GeometryElement>();

        public static IEnumerable<GeometryElement> SelectByChance(this IEnumerable<GeometryElement> filteredElements, Rnd rnd)
        {
            elList.Clear();

            foreach (var el in filteredElements)
            {
                if (el.Chance <= 0.00001f)
                    continue;
                if (el.ExclusiveSegmentGroupId == -1)
                {
                    if (rnd.Bool(el.Chance))
                        yield return el;
                    continue;
                }

                sumProb[el.ExclusiveSegmentGroupId] = 0;
                elList.Add(el);
            }

            for (int i = 0; i < elList.Count; i++)
            {
                var el = elList[i];
                sumProb[el.ExclusiveSegmentGroupId] += el.Chance;
            }

            for (int i = 0; i < elList.Count; i++)
            {
                var el = elList[i];
                var sum = sumProb[el.ExclusiveSegmentGroupId];
                if (sum <= 0)
                    continue;
                var prob = el.Chance / sum;
                if (rnd.Bool(prob))
                {
                    yield return el;
                    sumProb[el.ExclusiveSegmentGroupId] = -1;
                    continue;
                }
                sumProb[el.ExclusiveSegmentGroupId] = sum - el.Chance;
            }
        }
    }
}