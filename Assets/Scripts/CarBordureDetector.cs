using UnityEngine;

/// <summary>
/// Detects a wheel touching a curb.
/// Checks every wheel position via Car.GetWheelPosition(). When a wheel is near
/// a curb -> penalty, camera shake and a "suspension bottoming" sound emitted
/// from the exact wheel that hit the curb.
/// </summary>
public class CarBordureDetector : MonoBehaviour
{
    [Header("Check radius around each wheel")]
    public float wheelRadius  = 0.45f;

    [Header("Delay between repeated penalties (sec)")]
    public float cooldown     = 2f;

    [Header("Curb-touch sound (suspension)")]
    [Tooltip("Impact/bottoming clip. If empty, generated procedurally. A 3D source is created per wheel automatically")]
    public AudioClip kerbClip;
    [Range(0f, 1f)] public float kerbVolume = 1f;
    [Tooltip("Pitch spread ±, so repeated hits don't sound identical")]
    [Range(0f, 0.5f)] public float pitchVariation = 0.1f;
    [Tooltip("Min interval between sounds from one wheel (sec) - kills chatter on the contact edge")]
    public float soundCooldown = 0.15f;
    [Tooltip("Hit speed (m/s) at which the impact plays at full volume")]
    public float fullImpactSpeed = 6f;

    [Header("Curb resistance (slowdown)")]
    [Tooltip("Slow the car while a wheel is on a curb - otherwise it drives over without losing speed")]
    public bool  curbResistanceEnabled = true;
    [Tooltip("Resistance force per wheel (N per 1 m/s of horizontal speed). Higher = brakes harder")]
    public float curbResistance = 320f;
    [Tooltip("Cap on resistance force per wheel (N) - avoids a jerk at high speed")]
    public float curbResistanceClamp = 8000f;

    // Fields ExamTrigger needs for CheckCarOverlap() - don't remove
    [HideInInspector] public float capsuleRadius = 0.85f;
    [HideInInspector] public float centerOffsetY = -0.7f;
    [HideInInspector] public float halfLength    = 1.8f;

    private Car   _car;
    private float _lastPenaltyTime = -100f;

    // Per-wheel state
    private AudioSource[] _wheelSources;
    private bool[]        _wheelTouching;
    private float[]       _lastSoundTime;

    // Buffer for OverlapSphereNonAlloc - no per-frame allocations
    private readonly Collider[] _overlapBuffer = new Collider[16];

    void Start()
    {
        _car = GetComponentInParent<Car>();
        if (_car == null) _car = FindAnyObjectByType<Car>();

        int count = (_car != null && _car.WheelCount > 0) ? _car.WheelCount : 4;
        _wheelSources  = new AudioSource[count];
        _wheelTouching = new bool[count];
        _lastSoundTime = new float[count];

        // A separate 3D source per wheel - so sound comes from the wheel's position
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"KerbAudio_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.loop         = false;
            src.spatialBlend = 1f;                         // fully 3D
            src.minDistance  = 2f;
            src.maxDistance  = 30f;
            src.rolloffMode  = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0f;
            _wheelSources[i]  = src;
            _lastSoundTime[i] = -100f;
        }

        // No clip assigned - synthesize a "suspension bottoming" sound
        if (kerbClip == null) kerbClip = GenerateSuspensionClip();
    }

    void FixedUpdate()
    {
        if (_car == null || _wheelSources == null) return;

        int count = _wheelSources.Length;
        bool anyTouching = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 wheelPos = _car.GetWheelPosition(i);
            if (_wheelSources[i] != null) _wheelSources[i].transform.position = wheelPos;

            // Is this wheel touching a curb
            bool touching = false;
            int nHits = Physics.OverlapSphereNonAlloc(wheelPos, wheelRadius, _overlapBuffer);
            for (int j = 0; j < nHits; j++)
            {
                var col = _overlapBuffer[j];
                if (col != null && col.gameObject.name.StartsWith("Bordure_"))
                {
                    touching = true;
                    break;
                }
            }

            if (touching)
            {
                anyTouching = true;

                // Longitudinal resistance: the curb slows the wheel (the raycast model
                // doesn't brake against a vertical wall on its own). Force opposes the
                // horizontal speed at the wheel, ~linear with speed, with a cap.
                if (curbResistanceEnabled && _car.rb != null)
                {
                    Vector3 horiz = Vector3.ProjectOnPlane(_car.rb.GetPointVelocity(wheelPos), _car.transform.up);
                    float hs = horiz.magnitude;
                    if (hs > 0.2f)
                    {
                        float f = Mathf.Min(curbResistance * hs, curbResistanceClamp);
                        _car.rb.AddForceAtPosition(-horiz.normalized * f, wheelPos, ForceMode.Force);
                    }
                }

                // Sound only at the MOMENT of contact (rising edge), not every frame,
                // so driving along a curb doesn't produce endless chatter.
                if (!_wheelTouching[i] && Time.time - _lastSoundTime[i] >= soundCooldown)
                {
                    _lastSoundTime[i] = Time.time;
                    PlayKerbSoundAt(i, wheelPos);
                }
            }
            _wheelTouching[i] = touching;
        }

        // Penalty and shake - with their own (longer) cooldown, so we don't penalize every frame
        if (anyTouching && Time.time - _lastPenaltyTime >= cooldown)
        {
            _lastPenaltyTime = Time.time;
            SpeedCameraShake.Instance?.TriggerKerbShake();
            ExamManager.Instance?.AddCollision();
        }
    }

    void PlayKerbSoundAt(int i, Vector3 wheelPos)
    {
        var src = _wheelSources[i];
        if (src == null || kerbClip == null) return;

        // Volume and pitch scale with hit speed: a light touch is quieter and duller,
        // a sharp hit louder and brighter.
        float speed  = _car.rb != null ? _car.rb.GetPointVelocity(wheelPos).magnitude : 3f;
        float impact = Mathf.Clamp01(speed / Mathf.Max(0.1f, fullImpactSpeed));

        src.transform.position = wheelPos;
        src.pitch = (1f + Random.Range(-pitchVariation, pitchVariation)) * Mathf.Lerp(0.9f, 1.1f, impact);
        src.PlayOneShot(kerbClip, kerbVolume * Mathf.Lerp(0.6f, 1f, impact));
    }

    /// <summary>
    /// Procedural suspension-bottoming sound: a dull "whump" with a falling pitch
    /// and quick decay (not a noisy crackle). Overridden by an assigned clip.
    /// </summary>
    static AudioClip GenerateSuspensionClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.20f;
        int n = (int)(sampleRate * duration);
        float[] data = new float[n];

        float phase = 0f;
        float lp = 0f; // muffled noise for a "mechanical" texture
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            // Pitch falls from ~115 to ~42 Hz - the feel of the suspension bottoming out
            float freq = Mathf.Lerp(115f, 42f, t);
            phase += 2f * Mathf.PI * freq / sampleRate;
            float body = Mathf.Sin(phase);

            // Envelope: fast attack, smooth decay
            float env = (1f - Mathf.Exp(-t * 240f)) * Mathf.Exp(-t * 8.5f);

            float white = Random.Range(-1f, 1f);
            lp = Mathf.Lerp(lp, white, 0.1f); // strong low-pass -> dull, no "grit"

            data[i] = Mathf.Clamp((body * 0.92f + lp * 0.1f) * env, -1f, 1f);
        }

        var clip = AudioClip.Create("KerbSuspension_Procedural", n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDrawGizmosSelected()
    {
        if (_car == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        int count = _car.WheelCount > 0 ? _car.WheelCount : 4;
        for (int i = 0; i < count; i++)
        {
            Vector3 wp = _car.GetWheelPosition(i);
            Gizmos.DrawWireSphere(wp, wheelRadius);
        }
    }
}
