using UnityEngine;

public class BoatIdleBobbing : MonoBehaviour
{
    [Header("Bobbing (Up & Down)")]
    public float bobAmount = 0.15f;
    public float bobSpeed = 2f;

    [Header("Rocking (Rotation)")]
    public float rockAmount = 3f;
    public float rockSpeed = 1.5f;

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

    void Start()
    {
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
    }

    void Update()
    {
        // 1. Vertical bobbing
        float newY = initialLocalPos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);

        // 2. Side-to-side rocking (Z axis) & front-to-back pitch (X axis)
        float rockZ = Mathf.Sin(Time.time * rockSpeed) * rockAmount;
        float rockX = Mathf.Cos(Time.time * rockSpeed * 0.7f) * (rockAmount * 0.5f);

        transform.localRotation = initialLocalRot * Quaternion.Euler(rockX, 0f, rockZ);
    }
}