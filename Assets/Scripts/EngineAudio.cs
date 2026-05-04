using UnityEngine;

/// <summary>
/// Engine sound - changes pitch and volume based on RPM and throttle.
/// A separate sound on gear shifts.
/// </summary>
public class EngineAudio : MonoBehaviour
{
    [Header("Car reference")]
    public Car car;

    [Header("Main engine sound (looping)")]
    public AudioSource engineSource;

    [Header("Pitch by RPM")]
    [Tooltip("Pitch at idle")]
    public float pitchAtIdle = 0.6f;
    [Tooltip("Pitch at max RPM")]
    public float pitchAtMax  = 2.2f;

    [Header("Volume by throttle")]
    [Tooltip("Volume at idle (no throttle)")]
    public float volumeIdle  = 0.4f;
    [Tooltip("Volume at full throttle")]
    public float volumeMax   = 1.0f;
    [Tooltip("Volume smoothing speed")]
    public float volumeSmooth = 5f;

    private float _targetVolume;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<Car>();

        if (engineSource != null)
        {
            engineSource.loop       = true;
            engineSource.playOnAwake = false;
            if (!engineSource.isPlaying) engineSource.Play();
        }
    }

    void Update()
    {
        if (car == null || engineSource == null) return;
        if (car.rb == null) return; // car not initialized yet

        float rpm     = car.e.getRPM();
        float throttle = Mathf.Abs(car.userInput.y);

        // Pitch straight from RPM - no extra smoothing (RPM is already smoothed in Engine)
        float rpmT = Mathf.InverseLerp(car.e.idleRPM, car.e.maxRPM, rpm);
        engineSource.pitch = Mathf.Lerp(pitchAtIdle, pitchAtMax, rpmT);

        // Volume - louder on throttle, quieter at idle
        _targetVolume = Mathf.Lerp(volumeIdle, volumeMax, throttle);
        engineSource.volume = Mathf.Lerp(engineSource.volume, _targetVolume,
                                          Time.deltaTime * volumeSmooth);

    }
}
