using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class AccelerometerInputSystem : MonoBehaviour
{
    public float speedMultiplier = 10f;
    public float deadZone = 0.1f;

    private Rigidbody rb;

    private void OnEnable()
    {
        rb = GetComponent<Rigidbody>();

        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);
    }

    private void OnDisable()
    {
        if (Accelerometer.current != null)
            InputSystem.DisableDevice(Accelerometer.current);
    }

    private void FixedUpdate()
    {
        rb.AddForce(rb.GetAccumulatedForce() * -1);
        if (Accelerometer.current == null)
            return;

        Vector3 acceleration =
            Accelerometer.current.acceleration.ReadValue();

        Vector3 movement = new Vector3(acceleration.x, 0f, acceleration.y);

        if (movement.magnitude < deadZone)
            movement = Vector3.zero;
        else
            Debug.Log(movement);
        //Debug.Log(rb.linearVelocity);

        rb.linearVelocity += movement * 200 *Time.deltaTime;
    }

    private void OnGUI()
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 24;
        labelStyle.normal.textColor = Color.white;

        GUILayout.BeginArea(new Rect(20, 20, 400, 200));
        GUILayout.Label($"Vel X: {rb.linearVelocity.x:F2} Y: {rb.linearVelocity.y:F2} Z: {rb.linearVelocity.z:F2}", labelStyle);
        GUILayout.EndArea();
    }
}