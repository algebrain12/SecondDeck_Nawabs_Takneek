using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

[RequireComponent(typeof(AudioSource))]
public class MicVolumeDetector : MonoBehaviour
{
    public float loudness = 0;
    public int sampleWindow = 128;

    private string _device;
    private AudioClip _clipRecord;
    private bool _isInitialized;

    void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
#endif

        InitMicrophone();
    }

    void InitMicrophone()
    {
        if (_device == null)
        {
            _device = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        }

        if (_device != null)
        {
            _clipRecord = Microphone.Start(_device, true, 10, 44100);
            _isInitialized = true;
        }
    }

    void Update()
    {
        if (!_isInitialized) return;

        loudness = GetLoudnessFromMicrophone();

        //Debug.Log(loudness);
    }

    float GetLoudnessFromMicrophone()
    {
        float[] waveData = new float[sampleWindow];

        int micPosition = Microphone.GetPosition(_device) - (sampleWindow + 1);
        if (micPosition < 0) return 0;

        _clipRecord.GetData(waveData, micPosition);

        float sum = 0;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += waveData[i] * waveData[i];
        }

        float rms = Mathf.Sqrt(sum / sampleWindow);
        return rms;
    }

    void OnDisable()
    {
        StopMicrophone();
    }

    void OnDestroy()
    {
        StopMicrophone();
    }

    void StopMicrophone()
    {
        if (_isInitialized)
        {
            Microphone.End(_device);
            _isInitialized = false;
        }
    }
}
