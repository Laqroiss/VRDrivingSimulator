using UnityEngine;
using System.Collections.Generic;

/// <summary>
///    -.
///
///  :
///   Sequential вЂ”    (  )
///   AllVisible  вЂ”    
/// </summary>
public class RouteManager : MonoBehaviour
{
    public enum ShowMode { Sequential, AllVisible }

    [Header("  ( )")]
    public List<WaypointArrow> waypoints = new List<WaypointArrow>();

    [Header(" ")]
    public ShowMode showMode = ShowMode.Sequential;

    [Tooltip("    (  Sequential)")]
    public int previewCount = 2;

    [Header("  ")]
    [Tooltip("      (InProgress)")]
    public bool onlyDuringExam = true;

    private int  _current    = 0;
    private bool _routeActive = false;
    private ExamManager _exam;

    void Awake()
    {
        //     
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
            //    
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

    // вв   ввввввввввввввввввввввввввввввввввввввввввввввввв

    public void StartRoute()
    {
        _current     = 0;
        _routeActive = true;

        foreach (var w in waypoints)
            if (w != null) w.Reset();

        RefreshVisibility();
        Debug.Log($"[RouteManager]  : {waypoints.Count} ");
    }

    public void StopRoute()
    {
        _routeActive = false;
        HideAll();
    }

    //   WaypointArrow    
    public void OnWaypointReached(int index)
    {
        Debug.Log($"[RouteManager]  {index + 1}/{waypoints.Count} ");
        _current = index + 1;

        if (_current >= waypoints.Count)
        {
            Debug.Log("[RouteManager]  !");
            return;
        }

        RefreshVisibility();
    }

    // вв  вввввввввввввввввввввввввввввввввввввввввввввввввввввввввввв

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

    // вв   ввввввввввввввввввввввввввввввввввввввввввввввввввв

    public int CurrentWaypointIndex => _current;
    public bool IsComplete          => _current >= waypoints.Count;
    public int  TotalWaypoints      => waypoints.Count;
    public bool RouteActive         => _routeActive; //  RouteRibbon вЂ”      

    //   
    public void RestartRoute() => StartRoute();

    void OnDrawGizmos()
    {
        //     Scene View
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
