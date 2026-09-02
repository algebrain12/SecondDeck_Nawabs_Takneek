using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds draggable handles in the Scene View for every rectangle on a
/// SpawnAreaDisplayer (one move handle + four edge-resize handles each),
/// plus inspector buttons to add rectangles and generate / clear points.
/// </summary>
[CustomEditor(typeof(SpawnAreaDisplayer))]
public class SpawnAreaDisplayerEditor : Editor
{
    private SpawnAreaDisplayer area;

    private void OnEnable()
    {
        area = (SpawnAreaDisplayer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Rectangle"))
        {
            Undo.RecordObject(area, "Add Spawn Rectangle");

            // Offset the new rectangle next to the last one so it doesn't
            // land exactly on top of an existing one.
            Vector3 offset = Vector3.zero;
            if (area.rectangles.Count > 0)
            {
                var last = area.rectangles[area.rectangles.Count - 1];
                offset = last.localOffset + new Vector3(last.size.x + 2f, 0f, 0f);
            }

            area.rectangles.Add(new SpawnRectangle
            {
                label = "Area " + area.rectangles.Count,
                localOffset = offset,
                gizmoColor = Random.ColorHSV(0f, 1f, 0.6f, 0.9f, 0.8f, 1f)
            });

            EditorUtility.SetDirty(area);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated Points", area.generatedPoints.Count.ToString());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Points"))
        {
            Undo.RecordObject(area, "Generate Spawn Points");
            area.GeneratePoints();
            EditorUtility.SetDirty(area);
        }
        if (GUILayout.Button("Clear Points"))
        {
            Undo.RecordObject(area, "Clear Spawn Points");
            area.ClearPoints();
            EditorUtility.SetDirty(area);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnSceneGUI()
    {
        if (area.rectangles == null) return;

        Handles.matrix = area.transform.localToWorldMatrix;

        for (int i = 0; i < area.rectangles.Count; i++)
        {
            DrawRectangleHandles(area.rectangles[i], i);
        }

        Handles.matrix = Matrix4x4.identity;

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 300, 40), EditorStyles.helpBox);
        GUILayout.Label("Drag the center sphere to move, the cubes to resize");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void DrawRectangleHandles(SpawnRectangle rect, int index)
    {
        Vector3 center = rect.LocalCenter;
        float handleSize = HandleUtility.GetHandleSize(center) * 0.12f;

        Handles.color = rect.gizmoColor;
        Handles.Label(center + Vector3.up * 0.3f, string.IsNullOrEmpty(rect.label) ? $"Rect {index}" : rect.label);

        // --- Move handle (constrained to the local X/Z plane) ---
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.Slider2D(
            center, Vector3.up, Vector3.right, Vector3.forward,
            handleSize * 1.6f, Handles.SphereHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(area, "Move Spawn Rectangle");
            rect.localOffset += (newCenter - center);
            EditorUtility.SetDirty(area);
            center = newCenter;
        }

        // --- Resize handles (one per edge, constrained to its own axis) ---
        float halfW = rect.size.x * 0.5f;
        float halfD = rect.size.y * 0.5f;

        Vector3 right = center + new Vector3(halfW, 0f, 0f);
        Vector3 left = center + new Vector3(-halfW, 0f, 0f);
        Vector3 fwd = center + new Vector3(0f, 0f, halfD);
        Vector3 back = center + new Vector3(0f, 0f, -halfD);

        EditorGUI.BeginChangeCheck();

        Handles.color = Color.yellow;
        Vector3 newRight = Handles.Slider(right, Vector3.right, handleSize, Handles.CubeHandleCap, 0f);
        Vector3 newLeft = Handles.Slider(left, Vector3.left, handleSize, Handles.CubeHandleCap, 0f);
        Vector3 newFwd = Handles.Slider(fwd, Vector3.forward, handleSize, Handles.CubeHandleCap, 0f);
        Vector3 newBack = Handles.Slider(back, Vector3.back, handleSize, Handles.CubeHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(area, "Resize Spawn Rectangle");

            float newHalfW = Mathf.Max(0.05f, Mathf.Max(newRight.x - center.x, center.x - newLeft.x));
            float newHalfD = Mathf.Max(0.05f, Mathf.Max(newFwd.z - center.z, center.z - newBack.z));

            rect.size = new Vector2(newHalfW * 2f, newHalfD * 2f);
            EditorUtility.SetDirty(area);
        }
    }
}