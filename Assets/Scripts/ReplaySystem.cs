using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Система повторов.
/// Запись — по кнопке. Воспроизведение — из панели настроек.
/// Хранит до maxSavedReplays записей в памяти.
/// </summary>
public class ReplaySystem : MonoBehaviour
{
    // ── Данные одного кадра ────────────────────────────────────────────────

    struct ReplayFrame
    {
        public Vector3      carPos;
        public Quaternion   carRot;
        public Vector3[]    wheelPos;
        public Quaternion[] wheelRot;        // steering/position rotation (wheelObject)
        public Quaternion[] wheelVisualRot;  // spin rotation (wheelObject.GetChild(0))
        public float        speed;
        public int          gear;
        public float        rpm;
        // Огни
        public bool         brakeLights;
        public bool         reverseLights;
        public bool         leftBlink;
        public bool         rightBlink;
        public bool         blinkPhase;
    }

    // ── Одна запись (попытка) ─────────────────────────────────────────────

    class ReplaySession
    {
        public string            name;
        public List<ReplayFrame> frames = new List<ReplayFrame>();
        public float             duration;
    }

    // ── Инспектор ─────────────────────────────────────────────────────────

    [Header("Ссылки")]
    public Car         car;
    public Camera      replayCamera;
    public Camera      driverCamera;
    public EngineAudio engineAudio;  // для звука во время повтора

    [Header("Запись")]
    public float recordFPS      = 30f;
    public int   maxSavedReplays = 5;

    [Header("Горячие клавиши")]
    public KeyCode keyRecord     = KeyCode.F1; // начать/остановить запись
    public KeyCode keyPlayLast   = KeyCode.F2; // воспроизвести последний повтор
    public KeyCode keyStopReplay = KeyCode.F3; // остановить воспроизведение

    [Header("Кнопка записи (в сцене, необязательно)")]
    public Button            btnRecord;
    public TextMeshProUGUI   btnRecordLabel;

    [Header("UI повтора")]
    public GameObject        replayPlayerPanel; // панель с плеером
    public Slider            replaySlider;
    public TextMeshProUGUI   replayTimeLabel;
    public Button            btnReplayPlay;
    public Button            btnReplayStop;

    [Header("Список повторов (в Settings)")]
    public Transform         replayListParent;  // вертикальный layout где будут кнопки
    public GameObject        replayEntryPrefab; // префаб кнопки-строки (Button + TMP Text)

    [Header("Камера")]
    public Vector3 cameraOffset = new Vector3(0f, 3f, -8f);

    // ── Runtime ───────────────────────────────────────────────────────────

    private CarIndicators        _indicators;
    private GameObject[]         _ghostBrakeLights;
    private GameObject[]         _ghostReverseLights;
    private GameObject[]         _ghostLeftLights;
    private GameObject[]         _ghostRightLights;

    private List<ReplaySession> _sessions    = new List<ReplaySession>();
    private ReplaySession       _current;
    private bool                _recording   = false;
    private float               _recordTimer = 0f;
    private int                 _sessionNum  = 0;

    // Публичное время воспроизведения — читается из ReplayCRMSync для синхронизации HUD
    public float CurrentReplayTime { get; private set; } = 0f;
    public bool  IsReplaying       => _replaying;

    private AudioSource         _replayEngineSource;
    private bool                _autoCreatedCamera = false;
    private bool                _replaying   = false;
    private bool                _scrubbing   = false;
    private float               _replayStart = 0f;
    private float               _replayOffset = 0f;
    private ReplaySession       _activeSession;
    private Coroutine           _replayCoroutine;
    private GameObject          _ghost;
    private Transform[]         _ghostWheels;

    // ── Камера повтора ────────────────────────────────────────────────────
    public enum CameraMode { Follow, Orbit, Free }
    private CameraMode _camMode   = CameraMode.Follow;
    private float      _orbitYaw  = 180f;
    private float      _orbitPitch = 20f;
    private float      _orbitDist  = 8f;
    private Vector3    _freePos;
    private float      _freeYaw;
    private float      _freePitch;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<Car>();
        if (engineAudio == null) engineAudio = FindAnyObjectByType<EngineAudio>();
        if (car != null) _indicators = car.GetComponent<CarIndicators>();

        // 2D AudioSource для повтора — клонируем клип из engineAudio
        if (engineAudio != null && engineAudio.engineSource != null)
        {
            _replayEngineSource = gameObject.AddComponent<AudioSource>();
            _replayEngineSource.clip        = engineAudio.engineSource.clip;
            _replayEngineSource.loop        = true;
            _replayEngineSource.spatialBlend = 0f; // 2D — позиция не важна
            _replayEngineSource.volume      = 0f;
            _replayEngineSource.playOnAwake = false;
            _replayEngineSource.Play();
        }

        btnRecord?.onClick.AddListener(ToggleRecording);
        btnReplayPlay?.onClick.AddListener(() => StartReplay(_activeSession));
        btnReplayStop?.onClick.AddListener(StopReplay);

        if (replaySlider != null)
            replaySlider.onValueChanged.AddListener(OnSliderChanged);

        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(false);

        UpdateRecordButton();
    }

    void Update()
    {
        // Tab — показать/скрыть курсор для работы с UI повторов
        if (LegacyInput.GetKeyDown(KeyCode.Tab))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = locked;
        }

        if (LegacyInput.GetKeyDown(keyRecord))
            ToggleRecording();

        if (LegacyInput.GetKeyDown(keyPlayLast))
        {
            if (_sessions.Count > 0)
                StartReplay(_sessions[_sessions.Count - 1]);
            else
                Debug.Log("[Replay] Нет записанных повторов. Нажмите F1 чтобы начать запись.");
        }

        if (LegacyInput.GetKeyDown(keyStopReplay))
            StopReplay();

        // Камера повтора — обновляем в Update чтобы ввод всегда читался корректно
        if (_replaying && replayCamera != null && _ghost != null)
            UpdateReplayCamera();
    }

    void FixedUpdate()
    {
        if (!_recording || _current == null || car?.rb == null) return;

        _recordTimer += Time.fixedDeltaTime;
        if (_recordTimer < 1f / recordFPS) return;
        _recordTimer = 0f;

        var f = new ReplayFrame
        {
            carPos      = car.transform.position,
            carRot      = car.transform.rotation,
            speed       = car.rb.linearVelocity.magnitude * 3.6f,
            gear        = car.e.getCurrentGear(),
            rpm         = car.e.getRPM(),
            brakeLights   = car.BrakeLightsOn,
            reverseLights = car.ReverseLightsOn,
            leftBlink   = _indicators != null && (_indicators.LeftIndicatorOn  || _indicators.HazardLightsOn),
            rightBlink  = _indicators != null && (_indicators.RightIndicatorOn || _indicators.HazardLightsOn),
            blinkPhase  = _indicators != null && _indicators.BlinkVisible,
        };

        if (car.wheels != null)
        {
            f.wheelPos       = new Vector3[car.wheels.Length];
            f.wheelRot       = new Quaternion[car.wheels.Length];
            f.wheelVisualRot = new Quaternion[car.wheels.Length];
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var wo = car.wheels[i].wheelObject;
                if (wo != null)
                {
                    f.wheelPos[i] = wo.transform.position;
                    f.wheelRot[i] = wo.transform.rotation;
                    // Спин (вращение вокруг оси) живёт на первом дочернем объекте
                    if (wo.transform.childCount > 0)
                        f.wheelVisualRot[i] = wo.transform.GetChild(0).rotation;
                }
            }
        }

        _current.frames.Add(f);
        _current.duration = _current.frames.Count / recordFPS;
    }

    // ── Запись ────────────────────────────────────────────────────────────

    void ToggleRecording()
    {
        if (_recording) StopRecording();
        else            StartRecording();
    }

    public void StartRecording(string sessionName = null)
    {
        _sessionNum++;
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        _current = new ReplaySession { name = sessionName ?? $"Попытка {_sessionNum}  ({timestamp})" };
        _recording   = true;
        _recordTimer = 0f;
        Debug.Log($"[Replay] Запись начата: {_current.name}");
        UpdateRecordButton();
    }

    public void StopRecording()
    {
        _recording = false;
        if (_current != null && _current.frames.Count > 0)
        {
            if (_sessions.Count >= maxSavedReplays)
                _sessions.RemoveAt(0);

            _sessions.Add(_current);
            Debug.Log($"[Replay] Записано {_current.frames.Count} кадров ({_current.duration:F1} сек)");
            RefreshReplayList();
        }
        _current = null;
        UpdateRecordButton();
    }

    /// <summary>Запустить повтор из данных CRM (список кадров в формате ReplayCRMSync).</summary>
    public void StartReplayFromCRMData(List<ReplayCRMSync.CRMFrame> crmFrames, float fps)
    {
        if (crmFrames == null || crmFrames.Count == 0) return;

        // Приблизительный радиус колеса — для расчёта вращения
        float wheelRadius = 0.33f;
        if (car != null && car.wheels != null && car.wheels.Length > 0 && car.wheels[0].wheelObject != null)
        {
            // Пытаемся взять реальный размер из первого колеса (collider)
            var col = car.wheels[0].wheelObject.GetComponentInChildren<SphereCollider>();
            if (col != null) wheelRadius = col.radius;
        }

        // Локальные позиции колёс относительно машины
        Vector3[] wheelLocalPos = null;
        if (car != null && car.wheels != null)
        {
            wheelLocalPos = new Vector3[car.wheels.Length];
            for (int i = 0; i < car.wheels.Length; i++)
                if (car.wheels[i].wheelObject != null)
                    wheelLocalPos[i] = car.transform.InverseTransformPoint(car.wheels[i].wheelObject.transform.position);
        }

        var session   = new ReplaySession { name = "CRM Повтор" };
        float spinAng = 0f;

        for (int fi = 0; fi < crmFrames.Count; fi++)
        {
            var cf  = crmFrames[fi];
            var carRot = new Quaternion(cf.qx, cf.qy, cf.qz, cf.qw);
            var carPos = new Vector3(cf.x, cf.y, cf.z);

            // Вращение колёс: спин из скорости, поворот из yaw-дельты
            float dt          = 1f / fps;
            float speedMs     = cf.speed / 3.6f;
            float circumf     = 2f * Mathf.PI * wheelRadius;
            float rps         = circumf > 0f ? speedMs / circumf : 0f;
            spinAng          += rps * 360f * dt;

            // Дельта поворота машины для руления (только для передних колёс)
            float steerAngle = 0f;
            if (fi > 0)
            {
                var prev  = crmFrames[fi - 1];
                var prevY = new Quaternion(prev.qx, prev.qy, prev.qz, prev.qw).eulerAngles.y;
                var curY  = carRot.eulerAngles.y;
                float dy  = Mathf.DeltaAngle(prevY, curY);
                steerAngle = Mathf.Clamp(dy / dt * 0.04f, -40f, 40f);
            }

            Vector3[]    wp = null;
            Quaternion[] wr = null;
            Quaternion[] wv = null;

            if (wheelLocalPos != null)
            {
                int wn = wheelLocalPos.Length;
                wp = new Vector3[wn];
                wr = new Quaternion[wn];
                wv = new Quaternion[wn];
                for (int wi = 0; wi < wn; wi++)
                {
                    wp[wi] = carPos + carRot * wheelLocalPos[wi];
                    bool isFront = wi < 2;
                    wr[wi] = carRot * Quaternion.Euler(0f, isFront ? steerAngle : 0f, 0f);
                    wv[wi] = Quaternion.Euler(spinAng, 0f, 0f);
                }
            }

            session.frames.Add(new ReplayFrame
            {
                carPos        = carPos,
                carRot        = carRot,
                wheelPos      = wp,
                wheelRot      = wr,
                wheelVisualRot = wv,
                speed         = cf.speed,
                gear          = cf.gear,
                rpm           = cf.rpm,
                brakeLights   = cf.bl,
                reverseLights = cf.rl,
                leftBlink     = cf.lb,
                rightBlink    = cf.rb,
                blinkPhase    = cf.bp,
            });
        }

        session.duration = session.frames.Count / fps;
        recordFPS        = fps;
        StartReplay(session);
    }

    void UpdateRecordButton()
    {
        if (btnRecordLabel == null) return;
        btnRecordLabel.text = _recording ? "[ ] Стоп запись" : "[o] Начать запись";
        if (btnRecord != null)
        {
            var img = btnRecord.GetComponent<Image>();
            if (img != null) img.color = _recording
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.3f, 1f, 0.5f);
        }
    }

    // ── Список повторов ───────────────────────────────────────────────────

    void RefreshReplayList()
    {
        if (replayListParent == null) return;

        // Удаляем старые кнопки
        foreach (Transform child in replayListParent)
            Destroy(child.gameObject);

        // Создаём кнопку для каждой сессии
        foreach (var session in _sessions)
        {
            var s = session; // захватываем переменную

            if (replayEntryPrefab != null)
            {
                var entry = Instantiate(replayEntryPrefab, replayListParent);
                entry.SetActive(true);
                var label = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = $"{s.name}  —  {s.duration:F1} сек";
                var btn = entry.GetComponent<Button>();
                btn?.onClick.AddListener(() => StartReplay(s));

            }
            else
            {
                Debug.Log($"[Replay] Доступен повтор: {s.name} ({s.duration:F1} сек)");
            }
        }
    }

    void SelectReplay(ReplaySession session)
    {
        _activeSession = session;
        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(true);
        if (replaySlider != null)
        {
            replaySlider.minValue = 0;
            replaySlider.maxValue = session.frames.Count - 1;
            replaySlider.value   = 0;
        }
        Debug.Log($"[Replay] Выбран: {session.name}");
    }

    // ── Воспроизведение ───────────────────────────────────────────────────

    void StartReplay(ReplaySession session)
    {
        if (session == null || session.frames.Count == 0) return;
        if (_replaying) StopReplay();

        CreateGhost();

        // Если replayCamera не назначена — создаём временную
        if (replayCamera == null)
        {
            var camGO = new GameObject("ReplayCam_Auto");
            replayCamera = camGO.AddComponent<Camera>();
            replayCamera.fieldOfView = driverCamera != null ? driverCamera.fieldOfView : 60f;
            _autoCreatedCamera = true;
        }

        if (driverCamera != null) driverCamera.enabled = false;
        replayCamera.gameObject.SetActive(true);
        // Отключаем все скрипты на replayCamera чтобы они не перехватывали управление
        foreach (var mono in replayCamera.GetComponents<MonoBehaviour>())
            mono.enabled = false;
        if (engineAudio != null) engineAudio.enabled = false;
        if (_replayEngineSource != null) _replayEngineSource.volume = engineAudio?.volumeIdle ?? 0.4f;

        // Показываем панель плеера и настраиваем слайдер
        _activeSession = session;
        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(true);
        if (replaySlider != null)
        {
            replaySlider.minValue = 0;
            replaySlider.maxValue = session.frames.Count - 1;
            replaySlider.value    = 0;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        _camMode    = CameraMode.Follow;
        _orbitDist  = Mathf.Abs(cameraOffset.z);
        _orbitPitch = 20f;
        _orbitYaw   = 180f;

        // Начальная позиция камеры за машиной
        if (replayCamera != null && _ghost != null)
        {
            var startPos = _ghost.transform.TransformPoint(cameraOffset);
            replayCamera.transform.position = startPos;
            replayCamera.transform.LookAt(_ghost.transform.position + Vector3.up);
            _freePos   = startPos;
            _freeYaw   = replayCamera.transform.eulerAngles.y;
            _freePitch = replayCamera.transform.eulerAngles.x;
        }

        _replaying = true;
        _replayCoroutine = StartCoroutine(ReplayRoutine(session));
    }

    void StopReplay()
    {
        _replaying = false;
        if (_replayCoroutine != null) StopCoroutine(_replayCoroutine);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (driverCamera != null) driverCamera.enabled = true;
        if (replayCamera != null)
        {
            if (_autoCreatedCamera)
            {
                Destroy(replayCamera.gameObject);
                replayCamera = null;
                _autoCreatedCamera = false;
            }
            else
            {
                // Возвращаем скрипты и прячем камеру
                foreach (var mono in replayCamera.GetComponents<MonoBehaviour>())
                    mono.enabled = true;
                replayCamera.gameObject.SetActive(false);
            }
        }
        if (engineAudio != null) engineAudio.enabled = true;
        if (_replayEngineSource != null) _replayEngineSource.volume = 0f;
        DestroyGhost();
        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(false);
    }

    IEnumerator ReplayRoutine(ReplaySession session)
    {
        _replayStart      = Time.time;
        _replayOffset     = 0f;
        CurrentReplayTime = 0f;
        float duration = session.duration;

        while (_replaying)
        {
            int idx;

            float frac = 0f;
            if (_scrubbing && replaySlider != null)
            {
                idx = Mathf.Clamp(Mathf.RoundToInt(replaySlider.value), 0, session.frames.Count - 1);
                CurrentReplayTime = idx / recordFPS;
            }
            else
            {
                float elapsed = (Time.time - _replayStart) + _replayOffset;
                if (elapsed >= duration) { StopReplay(); yield break; }
                float tNorm = elapsed / duration * session.frames.Count;
                idx  = Mathf.Clamp(Mathf.FloorToInt(tNorm), 0, session.frames.Count - 1);
                frac = tNorm - idx;
                CurrentReplayTime = elapsed;

                replaySlider?.SetValueWithoutNotify(idx);
            }

            // Интерполяция между кадрами — убирает рывки при низком fps записи
            int nextIdx = Mathf.Min(idx + 1, session.frames.Count - 1);
            if (frac > 0f && nextIdx != idx)
                ApplyFrameInterpolated(session.frames[idx], session.frames[nextIdx], frac);
            else
                ApplyFrame(session.frames[idx]);

            if (replayTimeLabel != null)
            {
                var f = session.frames[idx];
                float secs = idx / recordFPS;
                replayTimeLabel.text = $"{secs:F1}s / {duration:F1}s  •  {f.speed:F0} км/ч  D{f.gear}  {f.rpm:F0} RPM";
            }

            // Звук двигателя по записанным данным
            if (_replayEngineSource != null && engineAudio != null && car != null)
            {
                var rf = session.frames[idx];
                float rpmT = Mathf.InverseLerp(car.e.idleRPM, car.e.maxRPM, rf.rpm);
                _replayEngineSource.pitch  = Mathf.Lerp(engineAudio.pitchAtIdle, engineAudio.pitchAtMax, rpmT);
                float speedT = Mathf.Clamp01(rf.speed / 100f);
                _replayEngineSource.volume = Mathf.Lerp(engineAudio.volumeIdle, engineAudio.volumeMax, speedT);
            }

            yield return null;
        }
    }

    // ── Камера повтора ────────────────────────────────────────────────────

    void UpdateReplayCamera()
    {
        var ghostPos = _ghost.transform.position;

        // C — переключение режима
        if (LegacyInput.GetKeyDown(KeyCode.C))
        {
            _camMode = (CameraMode)(((int)_camMode + 1) % 3);
            if (_camMode == CameraMode.Free)
            {
                _freePos   = replayCamera.transform.position;
                _freeYaw   = replayCamera.transform.eulerAngles.y;
                _freePitch = replayCamera.transform.eulerAngles.x;
            }
            Debug.Log($"[Replay Cam] Режим: {_camMode}  pos={replayCamera.transform.position}");
        }

        bool  rmb    = LegacyInput.GetKey(KeyCode.Mouse1);
        float mouseX = LegacyInput.GetAxis("Mouse X");
        float mouseY = LegacyInput.GetAxis("Mouse Y");
        float scroll = UnityEngine.InputSystem.Mouse.current?.scroll.y.ReadValue() * 0.01f ?? 0f;


        switch (_camMode)
        {
            case CameraMode.Follow:
            {
                var target = _ghost.transform.TransformPoint(cameraOffset);
                replayCamera.transform.position = Vector3.Lerp(
                    replayCamera.transform.position, target, Time.deltaTime * 5f);
                replayCamera.transform.LookAt(ghostPos + Vector3.up);
                break;
            }

            case CameraMode.Orbit:
            {
                if (rmb)
                {
                    _orbitYaw   += mouseX * 3f;
                    _orbitPitch -= mouseY * 3f;
                    _orbitPitch  = Mathf.Clamp(_orbitPitch, -30f, 80f);
                }
                _orbitDist -= scroll * 5f;
                _orbitDist  = Mathf.Clamp(_orbitDist, 2f, 40f);

                var rot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
                replayCamera.transform.position = ghostPos + rot * new Vector3(0f, 0f, -_orbitDist);
                replayCamera.transform.LookAt(ghostPos + Vector3.up * 0.8f);
                break;
            }

            case CameraMode.Free:
            {
                if (rmb)
                {
                    _freeYaw   += mouseX * 3f;
                    _freePitch -= mouseY * 3f;
                    _freePitch  = Mathf.Clamp(_freePitch, -89f, 89f);
                }
                var freeRot = Quaternion.Euler(_freePitch, _freeYaw, 0f);

                float speed = 10f * Time.deltaTime;
                if (LegacyInput.GetKey(KeyCode.LeftShift)) speed *= 3f;
                Vector3 move = Vector3.zero;
                if (LegacyInput.GetKey(KeyCode.W) || LegacyInput.GetKey(KeyCode.UpArrow))    move += freeRot * Vector3.forward;
                if (LegacyInput.GetKey(KeyCode.S) || LegacyInput.GetKey(KeyCode.DownArrow))  move -= freeRot * Vector3.forward;
                if (LegacyInput.GetKey(KeyCode.A) || LegacyInput.GetKey(KeyCode.LeftArrow))  move -= freeRot * Vector3.right;
                if (LegacyInput.GetKey(KeyCode.D) || LegacyInput.GetKey(KeyCode.RightArrow)) move += freeRot * Vector3.right;
                if (LegacyInput.GetKey(KeyCode.E)) move += Vector3.up;
                if (LegacyInput.GetKey(KeyCode.Q)) move -= Vector3.up;

                _freePos += move * speed;
                replayCamera.transform.SetPositionAndRotation(_freePos, freeRot);
                break;
            }
        }
    }

    void OnSliderChanged(float value)
    {
        if (!_replaying || _activeSession == null) return;

        // Помечаем что идёт scrub (на следующем кадре корутина возьмёт значение слайдера)
        _scrubbing = true;

        // После небольшой паузы (когда юзер отпустил мышь) возобновляем нормальное воспроизведение
        CancelInvoke(nameof(EndScrub));
        Invoke(nameof(EndScrub), 0.1f);
    }

    void EndScrub()
    {
        if (!_replaying || _activeSession == null) { _scrubbing = false; return; }

        // Возобновляем воспроизведение с позиции слайдера
        int idx = Mathf.RoundToInt(replaySlider.value);
        _replayOffset = idx / recordFPS;
        _replayStart  = Time.time;
        _scrubbing    = false;
    }

    void ApplyFrameInterpolated(ReplayFrame a, ReplayFrame b, float t)
    {
        if (_ghost == null) return;
        var pos = Vector3.Lerp(a.carPos, b.carPos, t);
        var rot = Quaternion.Slerp(a.carRot, b.carRot, t);
        _ghost.transform.SetPositionAndRotation(pos, rot);

        if (_ghostWheels != null && a.wheelPos != null && b.wheelPos != null)
            for (int i = 0; i < _ghostWheels.Length && i < a.wheelPos.Length; i++)
            {
                var gw = _ghostWheels[i];
                if (gw == null) continue;
                var wp = Vector3.Lerp(a.wheelPos[i], b.wheelPos[i], t);
                var wr = Quaternion.Slerp(a.wheelRot[i], b.wheelRot[i], t);
                gw.localPosition = _ghost.transform.InverseTransformPoint(wp);
                gw.localRotation = Quaternion.Inverse(rot) * wr;
                if (a.wheelVisualRot != null && b.wheelVisualRot != null && gw.childCount > 0)
                    gw.GetChild(0).localRotation = Quaternion.Inverse(gw.rotation) *
                        Quaternion.Slerp(a.wheelVisualRot[i], b.wheelVisualRot[i], t);
            }

        SetGhostLights(_ghostBrakeLights,   a.brakeLights);
        SetGhostLights(_ghostReverseLights, a.reverseLights);
        SetGhostLights(_ghostLeftLights,    a.leftBlink  && a.blinkPhase);
        SetGhostLights(_ghostRightLights,   a.rightBlink && a.blinkPhase);
    }

    void ApplyFrame(ReplayFrame f)
    {
        if (_ghost == null) return;
        _ghost.transform.SetPositionAndRotation(f.carPos, f.carRot);

        if (_ghostWheels != null && f.wheelPos != null)
            for (int i = 0; i < _ghostWheels.Length && i < f.wheelPos.Length; i++)
            {
                var gw = _ghostWheels[i];
                if (gw == null) continue;
                gw.localPosition = _ghost.transform.InverseTransformPoint(f.wheelPos[i]);
                gw.localRotation = Quaternion.Inverse(f.carRot) * f.wheelRot[i];
                if (f.wheelVisualRot != null && gw.childCount > 0)
                    gw.GetChild(0).localRotation =
                        Quaternion.Inverse(gw.rotation) * f.wheelVisualRot[i];
            }

        // Огни
        SetGhostLights(_ghostBrakeLights,   f.brakeLights);
        SetGhostLights(_ghostReverseLights, f.reverseLights);
        SetGhostLights(_ghostLeftLights,    f.leftBlink  && f.blinkPhase);
        SetGhostLights(_ghostRightLights,   f.rightBlink && f.blinkPhase);
    }

    // ── Призрак ───────────────────────────────────────────────────────────

    void CreateGhost()
    {
        if (car == null) return;
        _ghost = Instantiate(car.gameObject);
        _ghost.name = "ReplayGhost";

        foreach (var rb   in _ghost.GetComponentsInChildren<Rigidbody>())     Destroy(rb);
        foreach (var col  in _ghost.GetComponentsInChildren<Collider>())     Destroy(col);
        foreach (var mono in _ghost.GetComponentsInChildren<MonoBehaviour>()) Destroy(mono);
        // Компоненты не наследующие MonoBehaviour — удаляем отдельно
        foreach (var cam in _ghost.GetComponentsInChildren<Camera>())        Destroy(cam);
        foreach (var al  in _ghost.GetComponentsInChildren<AudioListener>()) Destroy(al);
        foreach (var au  in _ghost.GetComponentsInChildren<AudioSource>())   Destroy(au);

        // Скрываем оригинальные колёса в клоне машины — они дублируются нашими клонами ниже
        if (car.wheels != null)
        {
            var hiddenWheels = new System.Collections.Generic.HashSet<GameObject>();
            foreach (var w in car.wheels)
            {
                if (w.wheelObject == null || !hiddenWheels.Add(w.wheelObject)) continue;
                string path = GetRelativePath(car.transform, w.wheelObject.transform);
                if (path == null) continue;
                var orig = _ghost.transform.Find(path);
                if (orig != null) orig.gameObject.SetActive(false);
            }
        }

        // Колёса — клонируем отдельно и прикрепляем к ghost (они будут анимироваться)
        var ghostWheelList = new List<Transform>();
        if (car.wheels != null)
        {
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var w = car.wheels[i];
                if (w.wheelObject == null)
                {
                    Debug.LogWarning($"[Replay] wheels[{i}].wheelObject == null — колесо пропущено");
                    ghostWheelList.Add(null);
                    continue;
                }

                // Проверяем дубликат — если этот wheelObject уже клонировали, берём тот же клон
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (car.wheels[j].wheelObject == w.wheelObject)
                    {
                        ghostWheelList.Add(ghostWheelList[j]);
                        Debug.LogWarning($"[Replay] wheels[{i}] дублирует wheels[{j}] — общий объект {w.wheelObject.name}");
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                var clone = Instantiate(w.wheelObject, _ghost.transform); // дочерний объект ghost
                clone.name = $"GhostWheel_{i}";
                // Начальная локальная позиция — как у оригинала относительно машины
                clone.transform.localPosition = car.transform.InverseTransformPoint(w.wheelObject.transform.position);
                clone.transform.localRotation = Quaternion.Inverse(car.transform.rotation) * w.wheelObject.transform.rotation;
                foreach (var mono in clone.GetComponentsInChildren<MonoBehaviour>()) Destroy(mono);
                foreach (var col  in clone.GetComponentsInChildren<Collider>())      Destroy(col);
                ghostWheelList.Add(clone.transform);
            }
        }
        _ghostWheels = ghostWheelList.ToArray();

        // Кешируем ссылки на огни в ghost по тем же путям что у оригинала
        _ghostBrakeLights   = FindGhostObjects(car.brakeLights,                    car.transform, _ghost.transform);
        _ghostReverseLights = FindGhostObjects(car.reverseLights,                  car.transform, _ghost.transform);
        _ghostLeftLights    = FindGhostObjects(_indicators?.leftIndicatorLights,   car.transform, _ghost.transform);
        _ghostRightLights   = FindGhostObjects(_indicators?.rightIndicatorLights,  car.transform, _ghost.transform);

        // По умолчанию все огни выключены
        SetGhostLights(_ghostBrakeLights,   false);
        SetGhostLights(_ghostReverseLights, false);
        SetGhostLights(_ghostLeftLights,    false);
        SetGhostLights(_ghostRightLights,   false);
    }

    void DestroyGhost()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost              = null;
        _ghostWheels        = null;
        _ghostBrakeLights   = null;
        _ghostReverseLights = null;
        _ghostLeftLights    = null;
        _ghostRightLights   = null;
    }

    static GameObject[] FindGhostObjects(GameObject[] originals, Transform origRoot, Transform ghostRoot)
    {
        if (originals == null) return new GameObject[0];
        var result = new GameObject[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i] == null) continue;
            string path = GetRelativePath(origRoot, originals[i].transform);
            if (path == null) continue;
            var t = ghostRoot.Find(path);
            if (t != null) result[i] = t.gameObject;
        }
        return result;
    }

    static void SetGhostLights(GameObject[] lights, bool active)
    {
        if (lights == null) return;
        foreach (var go in lights)
            if (go != null) go.SetActive(active);
    }

    // Возвращает путь от root до target в виде "Child/SubChild/..."
    static string GetRelativePath(Transform root, Transform target)
    {
        if (target == null || root == null) return null;
        if (target == root) return "";

        var parts = new System.Collections.Generic.Stack<string>();
        var t = target;
        while (t != null && t != root)
        {
            parts.Push(t.name);
            t = t.parent;
        }
        if (t == null) return null; // target не является потомком root
        return string.Join("/", parts);
    }
}
