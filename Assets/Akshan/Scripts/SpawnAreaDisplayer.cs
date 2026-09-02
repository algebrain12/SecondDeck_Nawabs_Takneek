using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One rectangular spawn zone, positioned by a local offset from the owning
/// transform, sized independently, and drawn with its own gizmo color.
/// </summary>
[System.Serializable]
public class SpawnRectangle
{
    public string label = "Area";

    [Tooltip("Offset from the SpawnAreaDisplayer's transform, in its local space.")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("Width (X) and Depth (Z) of this rectangle.")]
    public Vector2 size = new Vector2(10f, 10f);

    [Tooltip("Local Y offset (on top of localOffset.y) at which points are spawned.")]
    public float height = 0f;

    public Color gizmoColor = new Color(0f, 1f, 0f, 1f);

    /// <summary>Center of this rectangle in the owning transform's local space.</summary>
    public Vector3 LocalCenter => localOffset + new Vector3(0f, height, 0f);

    public float Area => Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y);
}

/// <summary>
/// Defines one or more rectangular spawn areas on the X-Z plane (relative to this
/// transform), visualized with Gizmos and editable via handles in the Scene View
/// (see SpawnAreaDisplayerEditor). Generates a set of random points spread across
/// all rectangles, with a minimum spacing enforced between every point regardless
/// of which rectangle it came from.
/// </summary>
[ExecuteAlways]
public class SpawnAreaDisplayer : MonoBehaviour
{
    [Header("Rectangles (Local Space, X-Z Plane)")]
    public List<SpawnRectangle> rectangles = new List<SpawnRectangle> { new SpawnRectangle() };

    [Header("Point Generation")]
    [Min(1)] public int pointCount = 10;

    [Tooltip("Minimum distance required between any two generated points.")]
    [Min(0f)] public float minSpacing = 1f;

    [Tooltip("How many random tries are allowed (in aggregate) before giving up on placing all points.")]
    [Min(1)] public int maxAttemptsPerPoint = 30;

    public bool useRandomSeed = false;
    public int randomSeed = 0;

    [Header("Gizmo Settings")]
    public Color pointColor = Color.red;
    public float pointGizmoRadius = 0.3f;
    public bool showLabels = false;

    [HideInInspector]
    public List<Vector3> generatedPoints = new List<Vector3>();

    /// <summary>
    /// World-space bounds enclosing every rectangle (flat on Y).
    /// </summary>
    public Bounds GetWorldBounds()
    {
        if (rectangles == null || rectangles.Count == 0)
            return new Bounds(transform.position, Vector3.zero);

        Bounds b = new Bounds(transform.TransformPoint(rectangles[0].LocalCenter), Vector3.zero);
        foreach (var rect in rectangles)
        {
            float halfW = rect.size.x * 0.5f;
            float halfD = rect.size.y * 0.5f;
            Vector3 c = rect.LocalCenter;
            b.Encapsulate(transform.TransformPoint(c + new Vector3(halfW, 0, halfD)));
            b.Encapsulate(transform.TransformPoint(c + new Vector3(-halfW, 0, -halfD)));
            b.Encapsulate(transform.TransformPoint(c + new Vector3(halfW, 0, -halfD)));
            b.Encapsulate(transform.TransformPoint(c + new Vector3(-halfW, 0, halfD)));
        }
        return b;
    }

    /// <summary>
    /// Generates up to pointCount world-space points spread across all rectangles
    /// (chosen per-attempt, weighted by each rectangle's area), rejecting
    /// candidates that fall within minSpacing of an already-accepted point.
    /// </summary>
    public void GeneratePoints()
    {
        generatedPoints.Clear();

        if (rectangles == null || rectangles.Count == 0)
        {
            Debug.LogWarning($"[SpawnAreaDisplayer] No rectangles defined on '{name}'.");
            return;
        }

        System.Random rng = useRandomSeed ? new System.Random(randomSeed) : new System.Random();

        float totalArea = 0f;
        foreach (var rect in rectangles) totalArea += rect.Area;

        if (totalArea <= 0f)
        {
            Debug.LogWarning($"[SpawnAreaDisplayer] All rectangles on '{name}' have zero area.");
            return;
        }

        int maxAttempts = Mathf.Max(pointCount * maxAttemptsPerPoint, maxAttemptsPerPoint);
        int attempts = 0;

        while (generatedPoints.Count < pointCount && attempts < maxAttempts)
        {
            attempts++;

            SpawnRectangle rect = PickWeightedRectangle(rng, totalArea);
            if (rect == null || rect.Area <= 0f) continue;

            float halfW = rect.size.x * 0.5f;
            float halfD = rect.size.y * 0.5f;

            float x = (float)(rng.NextDouble() * rect.size.x - halfW);
            float z = (float)(rng.NextDouble() * rect.size.y - halfD);

            Vector3 localPoint = rect.LocalCenter + new Vector3(x, 0f, z);
            Vector3 worldPoint = transform.TransformPoint(localPoint);

            if (minSpacing > 0f && !IsFarEnough(worldPoint))
                continue;

            generatedPoints.Add(worldPoint);
        }

        if (generatedPoints.Count < pointCount)
        {
            Debug.LogWarning(
                $"[SpawnAreaDisplayer] Only placed {generatedPoints.Count}/{pointCount} points on '{name}'. " +
                "Try lowering minSpacing, adding/enlarging rectangles, or raising maxAttemptsPerPoint.");
        }
    }

    private SpawnRectangle PickWeightedRectangle(System.Random rng, float totalArea)
    {
        double roll = rng.NextDouble() * totalArea;
        double cumulative = 0.0;
        foreach (var rect in rectangles)
        {
            cumulative += rect.Area;
            if (roll <= cumulative)
                return rect;
        }
        return rectangles[rectangles.Count - 1];
    }

    private bool IsFarEnough(Vector3 candidate)
    {
        for (int i = 0; i < generatedPoints.Count; i++)
        {
            if (Vector3.Distance(generatedPoints[i], candidate) < minSpacing)
                return false;
        }
        return true;
    }

    public void ClearPoints()
    {
        generatedPoints.Clear();
    }

    private void OnDrawGizmos()
    {
        DrawArea();
    }

    private void OnDrawGizmosSelected()
    {
        DrawArea();
        DrawPoints();
    }

    private void DrawArea()
    {
        if (rectangles == null) return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        foreach (var rect in rectangles)
        {
            Vector3 center = rect.LocalCenter;
            Vector3 size = new Vector3(rect.size.x, 0f, rect.size.y);

            Gizmos.color = rect.gizmoColor;
            Gizmos.DrawWireCube(center, size);

            Color fill = rect.gizmoColor;
            fill.a = 0.08f;
            Gizmos.color = fill;
            Gizmos.DrawCube(center, size);

#if UNITY_EDITOR
            if (showLabels)
            {
                UnityEditor.Handles.matrix = Gizmos.matrix;
                UnityEditor.Handles.Label(center + Vector3.up * 0.3f, rect.label);
                UnityEditor.Handles.matrix = Matrix4x4.identity;
            }
#endif
        }

        Gizmos.matrix = oldMatrix;
    }

    private void DrawPoints()
    {
        Gizmos.color = pointColor;
        for (int i = 0; i < generatedPoints.Count; i++)
        {
            Gizmos.DrawSphere(generatedPoints[i], pointGizmoRadius);

#if UNITY_EDITOR
            if (showLabels)
            {
                UnityEditor.Handles.Label(
                    generatedPoints[i] + Vector3.up * (pointGizmoRadius + 0.2f), $"P{i}");
            }
#endif
        }
    }
}