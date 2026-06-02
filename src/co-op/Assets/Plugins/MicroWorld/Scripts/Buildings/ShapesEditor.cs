#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MicroWorldNS.Buildings
{
    [CustomPropertyDrawer(typeof(Shapes))]
    class ShapesEditor : PropertyDrawer
    {
        private List<ShapePoint> polygonPoints; // Polygon points (3D)
        private int selectedPoint = -1; // Index of the selected point (-1 if not selected)

        List<int> draggedPoints = new List<int>();
        bool selectedFrontView;
        Vector2 prevLocPos;
        bool extededMode;

        Shapes shapes;
        float gridStep => Shapes.GridStep * zoom;
        float LineH => EditorGUIUtility.singleLineHeight + 3;
        float zoom => shapes.Zoom;
        Rect sourceRect;
        Rect graphRect;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var h = Shapes.GraphHeight + LineH * 7 + 10;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.serializedObject.Update();
            DrawFields(position, property);

            position.x -= 20;
            position.width += 20;
            position.y += LineH;
            sourceRect = position;
            DrawGraph(property, position);

            EditorGUI.EndProperty();
        }

        private void DrawFields(Rect position, SerializedProperty property)
        {
            //var frameSizeProperty = property.FindPropertyRelative("FrameSizeInGridUnits");
            //EditorGUI.PropertyField(position, frameSizeProperty, new GUIContent("Frame Size"));

            property.serializedObject.ApplyModifiedProperties();
        }

        bool wasChanged = false;

        public void DrawGraph(SerializedProperty property, Rect sourceRect)
        {
#if UNITY_2022_1_OR_NEWER
            var shapes = property.boxedValue as Shapes;
#else
            var shapes = MemberInfoCache.GetTargetObjectOfProperty(property) as Shapes;
#endif    

            if (shapes == null)
                return;

            this.shapes = shapes;
            wasChanged = false;

            polygonPoints = shapes.GetActiveShapes().SelectMany(s => s.Points).ToList();

            // global Y
            var Y = sourceRect.y;
            Y += LineH;
            graphRect = sourceRect;
            graphRect.y = Y;
            graphRect.height = Shapes.GraphHeight;

            // Create two areas for front and side projections
            var frontViewRect = new Rect(graphRect.x, graphRect.y, Shapes.GraphWidth, graphRect.height);
            var c = frontViewRect.center;
            var frameSize = shapes.FrameSize * zoom;

            Rect frontViewPadRect = new Rect(
                c.x - frameSize.x / 2,
                c.y - frameSize.y / 2,
                frameSize.x,
                frameSize.y
            );

            var sideViewRect = new Rect(graphRect.x + Shapes.GraphWidth, graphRect.y, Shapes.GraphDepth, graphRect.height);
            c = sideViewRect.center;
            Rect sideViewPadRect = new Rect(
                c.x - frameSize.z / 2,
                c.y - frameSize.y / 2,
                frameSize.z,
                frameSize.y
            );

            // Draw background fill and frames for projections
            DrawViewWithBorder(frontViewRect, frontViewPadRect, "Front View (XY)");
            DrawViewWithBorder(sideViewRect, sideViewPadRect, "Side View (YZ)");

            DrawDirLines(frontViewRect, true);
            DrawDirLines(sideViewRect, false);

            // Draw the polygon and points in both projections
            DrawPolygon(shapes, frontViewRect, true);
            DrawPolygon(shapes, sideViewRect, false);

            if (Event.current.type == EventType.MouseUp) // Release of LMB
                ClearSelection();

            HandlePolygonEvents(frontViewRect, true);
            HandlePolygonEvents(sideViewRect, false);

            Y += graphRect.height + 4;
            DrawButtons(shapes, sourceRect, ref Y);

            Y += 5;
            if (shapes.SelectedShapeIndex >= 0)
                DrawShapeUI(sourceRect, shapes.Items[shapes.SelectedShapeIndex], ref Y);

            Event e = Event.current;

            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
                if (graphRect.Contains(e.mousePosition))
                    e.Use();

            property.serializedObject.Update();

#if UNITY_2022_1_OR_NEWER
            if (wasChanged)
                property.boxedValue = shapes;
#endif

            property.serializedObject.ApplyModifiedProperties();
        }

        GUIStyle labelStyle = new GUIStyle();

        private void DrawDirLines(Rect rect, bool isFrontView)
        {
            Event e = Event.current;

            if (!rect.Contains(e.mousePosition))
                return;

            using (new Handles.DrawingScope(Color.white))
            {
                labelStyle.normal.textColor = Color.white * 1.2f;

                if (selectedPoint >= 0)
                {
                    var selectedP = polygonPoints[selectedPoint];
                    var np = shapes.GetNormalized(selectedP.Point);
                    np.Scale(Shapes.TypicalSize);
                    if (isFrontView)
                    {
                        Handles.Label(new Vector3(e.mousePosition.x, rect.yMax - 15), np.x.ToString("0.00"), labelStyle);
                        Handles.Label(new Vector3(sourceRect.xMin - 25, e.mousePosition.y), np.y.ToString("0.00"), labelStyle);
                    }
                    else
                    {
                        Handles.Label(new Vector3(e.mousePosition.x, rect.yMax - 15), np.z.ToString("0.00"), labelStyle);
                        Handles.Label(new Vector3(sourceRect.xMin - 25, e.mousePosition.y), np.y.ToString("0.00"), labelStyle);
                    }

                    var y = selectedP.Point.y * zoom + rect.center.y;
                    var x = (isFrontView ? selectedP.Point.x : selectedP.Point.z) * zoom + rect.center.x;
                    //Handles.color = Color.gray;
                    Handles.DrawDottedLine(new Vector3(graphRect.xMin, y), new Vector3(graphRect.xMax, y), 0.5f);
                    Handles.DrawDottedLine(new Vector3(x, graphRect.yMin), new Vector3(x, graphRect.yMax), 0.5f);
                }
            }
        }

        private float DrawShapeUI(Rect sourceRect, Shape shape, ref float Y)
        {
            EditorGUI.BeginChangeCheck();

            var startX = sourceRect.x - 15;
            var X = startX;

            shape.Type = (ShapeType)EditorGUI.EnumPopup(new Rect(X, Y, 100, LineH - 3), shape.Type);
            X += 100;

            shape.Features = (ShapeFeatures)EditorGUI.EnumFlagsField(new Rect(X, Y, 200, LineH - 3), shape.Features);
            X += 200;

            if (shapes.Items.Count > 1)
            {
                if (GUI.Button(new Rect(X, Y, 100, LineH - 3), "Remove"))
                {
                    wasChanged = true;
                    shapes.Items.Remove(shape);
                    shapes.SelectedShapeIndex = -1;
                }
                X += 125;
            }

            Y += LineH;
            X = startX;

            shape.Material = EditorGUI.ObjectField(new Rect(X, Y, sourceRect.width, LineH - 3), "Material", shape.Material, typeof(Material), false) as Material;
            Y += LineH;
            if (shape.Material == null)
            {
                shape.MaterialIndex = EditorGUI.IntField(new Rect(X, Y, sourceRect.width, LineH - 3), "Material Index", shape.MaterialIndex);
                Y += LineH;
            }

            if (shape.Type == ShapeType.ScaledMesh)
            {
                X = startX;
                shape.Prefab = EditorGUI.ObjectField(new Rect(X, Y, sourceRect.width, LineH - 3), "Mesh", shape.Prefab, typeof(Mesh), false) as Mesh;
            }

            var w = sourceRect.width * 0.8f;

            if (shape.Type == ShapeType.GameObject)
            {
                X = startX;
                shape.Prefab = EditorGUI.ObjectField(new Rect(X, Y, w, LineH - 3), "Prefab", shape.Prefab, typeof(GameObject), true) as GameObject;
                X += w;
                shape.MeshScale = EditorGUI.FloatField(new Rect(X, Y, sourceRect.width - w, LineH - 3), shape.MeshScale);
            }

            if (shape.Type == ShapeType.Mesh)
            {
                X = startX;
                shape.Prefab = EditorGUI.ObjectField(new Rect(X, Y, w, LineH - 3), "Mesh", shape.Prefab, typeof(Mesh), false) as Mesh;
                X += w;
                shape.MeshScale = EditorGUI.FloatField(new Rect(X, Y, sourceRect.width - w, LineH - 3), shape.MeshScale);
            }

            if (EditorGUI.EndChangeCheck())
                wasChanged = true;

            return Y;
        }

        private void DrawButtons(Shapes shapes, Rect sourceRect, ref float Y)
        {
            var X = sourceRect.x;
            var buttonWidth = 45;
            var buttonsSpace = 5;

            var zoomed = shapes.Zoom != 1f;
            GUI.backgroundColor = zoomed ? Color.gray : Color.white;
            if (GUI.Button(new Rect(X, Y, buttonWidth, LineH - 3), "Zoom"))
            {
                shapes.Zoom = zoomed ? 1 : 0.5f;
                ClearSelection();
                wasChanged = true;
            }
            GUI.backgroundColor = Color.white;
            X += buttonWidth + buttonsSpace;

            if (GUI.Button(new Rect(X, Y, buttonWidth, LineH - 3), "Add"))
            {
                AddShape(shapes);
                ClearSelection();
                wasChanged = true;
            }
            X += buttonWidth + buttonsSpace;

            X += 10;
            for (int i = 0; i < shapes.Items.Count; i++)
            {
                GUI.backgroundColor = shapes.SelectedShapeIndex == i ? Color.gray : Color.white;
                if (GUI.Button(new Rect(X, Y, 25, LineH - 3), i.ToString()))
                {
                    if (shapes.SelectedShapeIndex == i)
                        shapes.SelectedShapeIndex = -1;
                    else
                        shapes.SelectedShapeIndex = i;
                    wasChanged = true;
                }

                X += 25 + 3;
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1, 0.7f, 0.7f, 1);
            if (GUI.Button(new Rect(sourceRect.xMax - buttonWidth, Y, buttonWidth, LineH - 3), "Clear"))
            {
                shapes.Items.Clear();
                AddShape(shapes);
                ClearSelection();
                wasChanged = true;
            }
            GUI.backgroundColor = Color.white;

            Y += LineH;
        }

        private void ClearSelection()
        {
            selectedPoint = -1;
            draggedPoints.Clear();
            extededMode = false;
        }

        private void AddShape(Shapes shapes)
        {
            var shape = new Shape();
            shape.CreateCubePoints(shapes.FrameSize);
            shapes.Items.Add(shape);
            shapes.SelectedShapeIndex = shapes.Items.Count - 1;
        }

        // Method for drawing a projection with a frame and title
        private void DrawViewWithBorder(Rect rect, Rect padRect, string title)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f)); // background filling
            DrawBorder(padRect); // frame
            GUI.Label(new Rect(rect.x, rect.y - LineH, rect.width, LineH - 3), title, EditorStyles.boldLabel);
        }

        // Method for drawing a frame
        private void DrawBorder(Rect rect)
        {
            Handles.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            Handles.DrawAAPolyLine(3f,
                new Vector3(rect.x, rect.y),
                new Vector3(rect.xMax, rect.y),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.x, rect.yMax),
                new Vector3(rect.x, rect.y));
        }

        // Draw a polygon depending on the projection
        private void DrawPolygon(Shapes shapes, Rect rect, bool isFrontView)
        {
            DrawPoints(shapes, rect, isFrontView);

            if (polygonPoints.Count < 2) return;

            if (shapes.SelectedShapeIndex == -1)
            foreach (var shape in shapes.Items)
                DrawShape(shape);
            else
            {
                for (int i = 0; i< shapes.Items.Count; i++)
                if (i != shapes.SelectedShapeIndex)
                    DrawShape(shapes.Items[i]);
                DrawShape(shapes.Items[shapes.SelectedShapeIndex]);
            }

            void DrawShape(Shape shape)
            {
                var isSelected = shapes.IsShapeSelected(shape);
                for (int iFace = 0; iFace < shape.VisibleFaces.Length; iFace++)
                {
                    var points = shape.GetCubeFace(iFace).ToArray();
                    if (shape.VisibleFaces[iFace])
                    for (int j = 0; j < points.Length; j++)
                    {
                        var p1 = GetProjectedPoint(points[j].Point, rect, isFrontView);
                        var p2 = GetProjectedPoint(points[(j + 1) % points.Length].Point, rect, isFrontView);
                        Handles.color = isSelected ? new Color(0, 1, 0, 1f) : new Color(0, 0.6f, 0, 1f);
                        Handles.DrawLine(p1, p2);
                    }
                }

                for (int iFace = 0; iFace < shape.VisibleFaces.Length; iFace++)
                {
                    var points = shape.GetCubeFace(iFace).ToArray();
                    if (isSelected)
                    if ((isFrontView ? Shape.SideFaces : Shape.FrontalFaces).Contains(iFace))
                    if (!shape.VisibleFaces[iFace])
                    {
                        var p1 = GetProjectedPoint(points[0].Point, rect, isFrontView);
                        var p2 = GetProjectedPoint(points[2].Point, rect, isFrontView);
                        Handles.color = new Color(1, 0, 0, 1f);
                        var p = (p1 + p2) / 2;
                        p1 = p - Vector3.one * 3;
                        p2 = p + Vector3.one * 3;
                        Handles.DrawLine(new Vector2(p1.x, p1.y), new Vector2(p2.x, p2.y));
                        Handles.DrawLine(new Vector2(p2.x, p1.y), new Vector2(p1.x, p2.y));
                    }
                }
            }
        }

        // We draw points depending on the projection
        private void DrawPoints(Shapes shapes, Rect rect, bool isFrontView)
        {
            Handles.color = Color.green;
            foreach (var point in polygonPoints)
            {
                Vector3 projectedPoint = GetProjectedPoint(point.Point, rect, isFrontView);
                Handles.DrawSolidDisc(projectedPoint, Vector3.forward, 3f);
            }
        }

        // Process click and drag events taking into account projection and indentation
        private void HandlePolygonEvents(Rect rect, bool isFrontView)
        {
            Event e = Event.current;

            if (!rect.Contains(e.mousePosition))
                return;

            Vector2 localPos = GetLocalPosition(e.mousePosition, rect);

            if (e.type == EventType.MouseDown && e.button == 0) // LMB
            {
                // We check whether we hit an existing point
                var pointIndex = FindPointIndex(localPos, isFrontView, false).FirstOrDefault();
                if (pointIndex >= 0)
                {
                    ClearSelection();
                    selectedPoint = pointIndex;
                    selectedFrontView = isFrontView;
                    prevLocPos = localPos;
                }
                else
                {
                    // hit the segment?
                    var foundSegments = FindSegments(localPos, rect, isFrontView, true, true).ToArray();

                    foreach (var shape in shapes.GetActiveShapes())
                    {
                        foreach (var seg in foundSegments)
                        {
                            var found = false;
                            var allowedFaces = isFrontView ? Shape.SideFaces : Shape.FrontalFaces;
                            foreach (int iFace in allowedFaces)
                            {
                                if (!shape.GetCubeFace(iFace).Contains(seg.Item1)) continue;
                                if (!shape.GetCubeFace(iFace).Contains(seg.Item2)) continue;
                                shape.VisibleFaces[iFace] = !shape.VisibleFaces[iFace];
                                found = true;
                                break;
                            }
                            if (found)
                                break;
                        }
                    }

                    wasChanged = true;
                }
            }
            else if (e.type == EventType.MouseDrag && selectedPoint >= 0) // Dragging
            {
                if (isFrontView != selectedFrontView)
                    return;

                var offset = localPos - prevLocPos;
                if (draggedPoints.Count == 0)
                {
                    extededMode = !e.control;
                    draggedPoints = FindPointIndex(prevLocPos, isFrontView, extededMode).ToList();
                }

                if (extededMode)
                {
                    if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y))
                        offset.y = 0;
                    else
                        offset.x = 0;
                }

                foreach (var i in draggedPoints)
                {
                    Vector3 pointOffset;
                    var p = polygonPoints[i];

                    pointOffset = isFrontView ? new Vector3(offset.x * p.draggedAxes.x, offset.y * p.draggedAxes.y, 0) : new Vector3(0, offset.y * p.draggedAxes.y, offset.x * p.draggedAxes.x);
                    p.Point += pointOffset / zoom;

                    if (!e.shift && pointOffset != Vector3.zero)
                        p.Point = SnapToGrid(p.Point);
                }

                prevLocPos += offset;

                wasChanged = true;
            }
        }

        // Convert the mouse position to local coordinates
        private Vector2 GetLocalPosition(Vector2 mousePosition, Rect rect)
        {
            Vector2 localPos = mousePosition - new Vector2(rect.center.x, rect.center.y);

            if (!Event.current.shift)
            {
                localPos = SnapToGrid(localPos);
            }

            return localPos;
        }

        // Bind the coordinates to the grid with the specified step
        private Vector2 SnapToGrid(Vector2 position)
        {
            float x = Mathf.Round(position.x / gridStep) * gridStep;
            float y = Mathf.Round(position.y / gridStep) * gridStep;
            return new Vector2(x, y);
        }

        // Bind the coordinates to the grid with the specified step
        private Vector3 SnapToGrid(Vector3 position)
        {
            float x = Mathf.RoundToInt(position.x / gridStep) * gridStep;
            float y = Mathf.RoundToInt(position.y / gridStep) * gridStep;
            float z = Mathf.RoundToInt(position.z / gridStep) * gridStep;
            return new Vector3(x, y, z);
        }

        // Convert a 3D point to 2D coordinates for the given projection
        private Vector3 GetProjectedPoint(Vector3 point, Rect rect, bool isFrontView)
        {
            var c = rect.center;
            return isFrontView
                ? new Vector3(c.x + point.x * zoom, c.y + point.y * zoom, 0)
                : new Vector3(c.x + point.z * zoom, c.y + point.y * zoom, 0);
        }

        private IEnumerable<(ShapePoint, ShapePoint)> FindSegments(Vector2 localPos, Rect rect, bool isFrontView, bool getEdgesByX, bool getEdgesByZ)
        {
            foreach (var shape in shapes.GetActiveShapes())
            foreach (var edge in shape.GetEdges(getEdgesByX, getEdgesByZ))
            {
                var e0 = GetProjectedPoint(edge.Item1.Point, Rect.zero, isFrontView);
                var e1 = GetProjectedPoint(edge.Item2.Point, Rect.zero, isFrontView);
                if (Helper.DistanceSqToSegment(e0, e1, localPos) <= 4 * 4)
                    yield return edge;
            }
        }

        // Looking for a point in the projection area
        private IEnumerable<int> FindPointIndex(Vector2 localPos, bool isFrontView, bool extended)
        {
            var was = false;
            for (int i = 0; i < polygonPoints.Count; i++)
            {
                Vector3 projectedPoint = GetProjectedPoint(polygonPoints[i].Point, Rect.zero, isFrontView);

                var diff = new Vector2(projectedPoint.x, projectedPoint.y) - localPos;
                var dist = diff.magnitude;
                var distX = Mathf.Abs(diff.x);
                var distY = Mathf.Abs(diff.y);
                if (extended)
                {
                    dist = Mathf.Min(dist, distX);
                    dist = Mathf.Min(dist, distY);
                }

                const float d = 6;
                if (dist <= d)
                {
                    var da = polygonPoints[i].draggedAxes;
                    da.x = distX < d || !extended ? 1 : 0;
                    da.y = distY < d || !extended ? 1 : 0;
                    polygonPoints[i].draggedAxes = da;

                    was = true;
                    yield return i;
                }
            }

            if (!was)
                yield return -1;
        }
    }
}
#endif