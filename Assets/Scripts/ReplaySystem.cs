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
        public Vector3     carPos;
        public Quaternion  carRot;
        public Vector3[]   wheelPos;
        public Quaternion[] wheelRot;
        public float       speed;
        public int         gear;
        public float       rpm;
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
    public Car    car;
    public Camera replayCamera;
    public Camera driverCamera;

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

    private List<ReplaySession> _sessions    = new List<ReplaySession>();
    private ReplaySession       _current;
    private bool                _recording   = false;
    private float               _recordTimer = 0f;
    private int                 _sessionNum  = 0;

    private bool                _replaying   = false;
    private ReplaySession       _activeSession;
    private Coroutine           _replayCoroutine;
    private GameObject          _ghost;
    private Transform[]         _ghostWheels;

    // ── Unity ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<Car>();

        btnRecord?.onClick.AddListener(ToggleRecording);
        btnReplayPlay?.onClick.AddListener(() => StartReplay(_activeSession));
        btnReplayStop?.onClick.AddListener(StopReplay);

        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(false);

        UpdateRecordButton();
    }

    void Update()
    {
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
    }

    void FixedUpdate()
    {
        if (!_recording || _current == null || car?.rb == null) return;

        _recordTimer += Time.fixedDeltaTime;
        if (_recordTimer < 1f / recordFPS) return;
        _recordTimer = 0f;

        var f = new ReplayFrame
        {
            carPos = car.transform.position,
            carRot = car.transform.rotation,
            speed  = car.rb.linearVelocity.magnitude * 3.6f,
            gear   = car.e.getCurrentGear(),
            rpm    = car.e.getRPM(),
        };

        if (car.wheels != null)
        {
            f.wheelPos = new Vector3[car.wheels.Length];
            f.wheelRot = new Quaternion[car.wheels.Length];
            for (int i = 0; i < car.wheels.Length; i++)
            {
                var wo = car.wheels[i].wheelObject;
                if (wo != null)
                {
                    f.wheelPos[i] = wo.transform.position;
                    f.wheelRot[i] = wo.transform.rotation;
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

    void StartRecording()
    {
        _sessionNum++;
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        _current = new ReplaySession { name = $"Попытка {_sessionNum}  ({timestamp})" };
        _recording   = true;
        _recordTimer = 0f;
        Debug.Log($"[Replay] Запись начата: {_current.name}");
        UpdateRecordButton();
    }

    void StopRecording()
    {
        _recording = false;
        if (_current != null && _current.frames.Count > 0)
        {
            // Ограничиваем кол-во сохранённых попыток
            if (_sessions.Count >= maxSavedReplays)
                _sessions.RemoveAt(0);

            _sessions.Add(_current);
            Debug.Log($"[Replay] Записано {_current.frames.Count} кадров ({_current.duration:F1} сек)");
            RefreshReplayList();
        }
        _current = null;
        UpdateRecordButton();
    }

    void UpdateRecordButton()
    {
        if (btnRecordLabel == null) return;
        btnRecordLabel.text = _recording ? "⏹ Стоп запись" : "⏺ Начать запись";
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
                var label = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = $"{s.name}  —  {s.duration:F1} сек";
                var btn = entry.GetComponent<Button>();
                btn?.onClick.AddListener(() => SelectReplay(s));
            }
            else
            {
                // Нет префаба — просто лог
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
        if (driverCamera != null) driverCamera.gameObject.SetActive(false);
        if (replayCamera != null) replayCamera.gameObject.SetActive(true);

        _replaying = true;
        _replayCoroutine = StartCoroutine(ReplayRoutine(session));
    }

    void StopReplay()
    {
        _replaying = false;
        if (_replayCoroutine != null) StopCoroutine(_replayCoroutine);
        DestroyGhost();
        if (driverCamera != null) driverCamera.gameObject.SetActive(true);
        if (replayCamera != null) replayCamera.gameObject.SetActive(false);
        if (replayPlayerPanel != null) replayPlayerPanel.SetActive(false);
    }

    IEnumerator ReplayRoutine(ReplaySession session)
    {
        float startTime = Time.time;
        float duration  = session.duration;

        while (_replaying)
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= duration) { StopReplay(); yield break; }

            float t   = elapsed / duration;
            int   idx = Mathf.Clamp(Mathf.FloorToInt(t * session.frames.Count), 0, session.frames.Count - 1);

            ApplyFrame(session.frames[idx]);

            if (replaySlider    != null) replaySlider.value = idx;
            if (replayTimeLabel != null)
            {
                var f = session.frames[idx];
                replayTimeLabel.text = $"{elapsed:F1}s  {f.speed:F0} км/ч  D{f.gear}  {f.rpm:F0} RPM";
            }

            if (replayCamera != null && _ghost != null)
            {
                var target = _ghost.transform.TransformPoint(cameraOffset);
                replayCamera.transform.position = Vector3.Lerp(
                    replayCamera.transform.position, target, Time.deltaTime * 5f);
                replayCamera.transform.LookAt(_ghost.transform.position + Vector3.up);
            }

            yield return null;
        }
    }

    void ApplyFrame(ReplayFrame f)
    {
        if (_ghost == null) return;
        _ghost.transform.SetPositionAndRotation(f.carPos, f.carRot);

        if (_ghostWheels != null && f.wheelPos != null)
            for (int i = 0; i < _ghostWheels.Length && i < f.wheelPos.Length; i++)
                if (_ghostWheels[i] != null)
                    _ghostWheels[i].SetPositionAndRotation(f.wheelPos[i], f.wheelRot[i]);
    }

    // ── Призрак ───────────────────────────────────────────────────────────

    void CreateGhost()
    {
        if (car == null) return;
        _ghost = Instantiate(car.gameObject);
        _ghost.name = "ReplayGhost";

        foreach (var rb   in _ghost.GetComponentsInChildren<Rigidbody>())    Destroy(rb);
        foreach (var col  in _ghost.GetComponentsInChildren<Collider>())     Destroy(col);
        foreach (var mono in _ghost.GetComponentsInChildren<MonoBehaviour>()) Destroy(mono);

        var ghostWheelList = new List<Transform>();
        if (car.wheels != null)
            foreach (var w in car.wheels)
                if (w.wheelObject != null)
                {
                    var found = _ghost.transform.Find(w.wheelObject.name);
                    if (found != null) ghostWheelList.Add(found);
                }
        _ghostWheels = ghostWheelList.ToArray();
    }

    void DestroyGhost()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost = _ghostWheels == null ? null : null;
        _ghostWheels = null;
    }
}
