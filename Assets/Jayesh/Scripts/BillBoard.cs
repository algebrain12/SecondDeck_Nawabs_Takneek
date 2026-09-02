using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCam == null) return;

        // Point the canvas's forward axis directly at the camera's position
        Vector3 directionToCamera = mainCam.transform.position - transform.position;
       // transform.rotation = Quaternion.Euler(90, 0, 0);
        transform.rotation = Quaternion.LookRotation(-directionToCamera, Vector3.up);
    }
}