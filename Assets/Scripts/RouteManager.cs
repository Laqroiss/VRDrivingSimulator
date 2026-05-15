using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Управляет маршрутом из стрелок-указателей.
///
/// Режимы показа:
///   Sequential — только следующая стрелка (и несколько предпросмотра)
///   AllVisible  — все стрелки видны сразу
/// </summary>
public class RouteManager : MonoBehaviour
{
    public enum ShowMode { Sequential, AllVisible }

    [Header("Стрелки маршрута (по порядку)")]
    public List<WaypointArrow> waypoints = new List<WaypointArrow>();

    [Header("Режим отображения")]
    public ShowMode showMode = ShowMode.Sequential;

    [Tooltip("Сколько стрелок показывать вперёд (только в Sequential)")]
    public int previewCount = 2;

    [Header("Связь с экзаменом")]
    [Tooltip("Показывать стрелки только во время экзамена (InProgress)")]
    public bool onlyDuringExam = true;

    private int  _current    = 0;
    private bool _routeActive = false;
    private ExamManager _exam;

    void Awake()
    {
        // Регистрируем себя во всех стрелках
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            waypoints[i].routeManager  = this;
            waypoints[i].waypointIndex = i;
        }
    }

    void Start()
    {
        _exam = FindAnyObjectByType<ExamManager>();

        if (onlyDuringExam && _exam != null)
        {
            // Скрываем все до старта
            HideAll();
            _exam.OnExamStart.AddListener(StartRoute);
            _exam.OnExamFinish.AddListener(StopRoute);
        }
        else
        {
            StartRoute();
        }
    }

    void OnDestroy()
    {
        if (_exam != null)
        {
            _exam.OnExamStart.RemoveListener(StartRoute);
            _exam.OnExamFinish.RemoveListener(StopRoute);
        }
    }

    // ── Управление маршрутом ─────────────────────────────────────────────────

    public void StartRoute()
    {
        _current     = 0;
        _routeActive = true;

        foreach (var w in waypoints)
            if (w != null) w.Reset();

        RefreshVisibility();
        Debug.Log($"[RouteManager] Маршрут начат: {waypoints.Count} точек");
    }

    public void StopRoute()
    {
        _routeActive = false;
        HideAll();
    }

    // Вызывается из WaypointArrow когда машина проехала точку
    public void OnWaypointReached(int index)
    {
        Debug.Log($"[RouteManager] Точка {index + 1}/{waypoints.Count} пройдена");
        _current = index + 1;

        if (_current >= waypoints.Count)
        {
            Debug.Log("[RouteManager] Маршрут завершён!");
            return;
        }

        RefreshVisibility();
    }

    // ── Видимость ────────────────────────────────────────────────────────────

    void RefreshVisibility()
    {
        if (!_routeActive) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            bool show = showMode == ShowMode.AllVisible
                ? i >= _current
                : (i >= _current && i < _current + previewCount);

            waypoints[i].gameObject.SetActive(show);
        }
    }

    void HideAll()
    {
        foreach (var w in waypoints)
            if (w != null) w.gameObject.SetActive(false);
    }

    // ── Публичный интерфейс ───────────────────────────────────────────────────

    public int CurrentWaypointIndex => _current;
    public bool IsComplete          => _current >= waypoints.Count;
    public int  TotalWaypoints      => waypoints.Count;

    // Перезапустить маршрут вручную
    public void RestartRoute() => StartRoute();

    void OnDrawGizmos()
    {
        // Рисуем линию маршрута в Scene View
        if (waypoints == null || waypoints.Count < 2) return;
        Gizmos.color = new Color(1f, 0.75f, 0f, 0.6f);
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].transform.position,
                            waypoints[i + 1].transform.position);
        }
    }
}
