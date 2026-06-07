using UnityEngine;

/// <summary>
/// Speed-based Perlin noise camera shake for VR immersion.
///
/// Attach to the XR Camera Offset object (parent of the tracked camera),
/// NOT to the tracked camera itself — the XR runtime owns the camera's
/// local transform, so shaking a parent keeps head-tracking intact.
///
/// Typical OpenXR hierarchy:
///   XR Origin
///     └─ Camera Offset  ← attach here
///          └─ Main Camera  (head-tracked, do not attach here)
///
/// TriggerKerbShake() is called from BordureContact / CarBordureDetector.
/// </summary>
public class SpeedCameraShake : MonoBehaviour
{
    public static SpeedCameraShake Instance { get; private set; }

    [Header("Speed-Based Shake")]
    [SerializeField] private float shakeStartSpeed   = 20f;    // km/h — below this: no shake
    [SerializeField] private float shakeMaxSpeed     = 80f;    // km/h — full shake above this
    [SerializeField] private float maxPositionIntensity = 0.005f; // metres
    [SerializeField] private float maxRotationIntensity = 0.3f;   // degrees
    [SerializeField] private float shakeFrequency    = 12f;    // Perlin time scale

    [Header("Kerb Impact Shake")]
    [SerializeField] private float kerbDuration          = 0.4f;   // seconds
    [SerializeField] private float kerbPositionIntensity = 0.02f;  // metres
    [SerializeField] private float kerbRotationIntensity = 1.0f;   // degrees
    [SerializeField] private float kerbFrequency         = 25f;

    private Rigidbody _rb;
    private float _kerbTimer;

    void Awake() => Instance = this;

    void Start()
    {
        _rb = GetComponentInParent<Rigidbody>();
        if (_rb == null)
            Debug.LogWarning("SpeedCameraShake: no Rigidbody found in parent hierarchy.");
    }

    void LateUpdate()
    {
        float speedKmh = _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;
        float speedT = Mathf.Clamp01(Mathf.InverseLerp(shakeStartSpeed, shakeMaxSpeed, speedKmh));

        if (_kerbTimer > 0f) _kerbTimer -= Time.deltaTime;
        float kerbT = Mathf.Clamp01(_kerbTimer / Mathf.Max(kerbDuration, 0.001f));

        if (speedT <= 0f && kerbT <= 0f)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return;
        }

        float t = Time.time;

        Vector3 posShake = SampleNoise3(t, shakeFrequency,        0f) * (maxPositionIntensity  * speedT)
                         + SampleNoise3(t, kerbFrequency,       200f) * (kerbPositionIntensity * kerbT);

        Vector3 rotShake = SampleNoise3(t, shakeFrequency * 0.7f, 100f) * (maxRotationIntensity  * speedT)
                         + SampleNoise3(t, kerbFrequency  * 0.7f, 300f) * (kerbRotationIntensity * kerbT);

        transform.localPosition = posShake;
        transform.localRotation = Quaternion.Euler(rotShake);
    }

    /// <summary>
    /// Call this when the car hits a kerb or obstacle to trigger a short impact shake.
    /// </summary>
    public void TriggerKerbShake()
    {
        _kerbTimer = kerbDuration;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Returns a [-1, 1]³ vector sampled from Perlin noise with independent axes.
    static Vector3 SampleNoise3(float t, float freq, float seed)
    {
        return new Vector3(
            (Mathf.PerlinNoise(t * freq + seed,        0f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t * freq + seed,       50f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t * freq + seed + 25f, 25f) - 0.5f) * 2f
        );
    }
}
