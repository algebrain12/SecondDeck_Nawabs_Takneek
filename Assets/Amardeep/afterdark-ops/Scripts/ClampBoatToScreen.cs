using UnityEngine;

public class ClampBoatToScreen : MonoBehaviour
{
    public Camera mainCamera;
    public float screenPadding = 0.05f;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        // 1. Get current screen position
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);

        // 2. Clamp X and Y within screen bounds
        viewportPos.x = Mathf.Clamp(viewportPos.x, screenPadding, 1f - screenPadding);
        viewportPos.y = Mathf.Clamp(viewportPos.y, screenPadding, 1f - screenPadding);

        // 3. Convert back to world position
        Vector3 targetWorldPos = mainCamera.ViewportToWorldPoint(viewportPos);

        // 4. Apply horizontal position while retaining fixed water height (Y)
        transform.position = new Vector3(targetWorldPos.x, transform.position.y, targetWorldPos.z);
    }
}