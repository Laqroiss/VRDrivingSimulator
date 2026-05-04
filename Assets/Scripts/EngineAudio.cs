using UnityEngine;

/// <summary>
/// Звук двигателя — меняет pitch и volume в зависимости от RPM и газа.
/// Отдельный звук при переключении передач.
/// </summary>
public class EngineAudio : MonoBehaviour
{
    [Header("Ссылка на машину")]
    public Car car;

    [Header("Основной звук двигателя (looping)")]
    public AudioSource engineSource;

    [Header("Pitch по RPM")]
    [Tooltip("Pitch на холостых оборотах")]
    public float pitchAtIdle = 0.6f;
    [Tooltip("Pitch на максимальных оборотах")]
    public float pitchAtMax  = 2.2f;

    [Header("Volume по газу")]
    [Tooltip("Громкость на холостых (без газа)")]
    public float volumeIdle  = 0.4f;
    [Tooltip("Громкость при полном газе")]
    public float volumeMax   = 1.0f;
    [Tooltip("Скорость сглаживания volume")]
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
        if (car.rb == null) return; // машина ещё не инициализирована

        float rpm     = car.e.getRPM();
        float throttle = Mathf.Abs(car.userInput.y);

        // Pitch напрямую по RPM — без доп. сглаживания (RPM уже сглажен в Engine)
        float rpmT = Mathf.InverseLerp(car.e.idleRPM, car.e.maxRPM, rpm);
        engineSource.pitch = Mathf.Lerp(pitchAtIdle, pitchAtMax, rpmT);

        // Volume — громче при газе, тише на холостых
        _targetVolume = Mathf.Lerp(volumeIdle, volumeMax, throttle);
        engineSource.volume = Mathf.Lerp(engineSource.volume, _targetVolume,
                                          Time.deltaTime * volumeSmooth);

    }
}
