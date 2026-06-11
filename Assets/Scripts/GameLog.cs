using UnityEngine;
using System.Diagnostics;

/// <summary>
/// Lightweight logging wrapper for non-critical diagnostics.
///
/// The methods are marked <see cref="ConditionalAttribute"/>, so the compiler removes every call
/// — including its argument evaluation (string interpolation, allocations) — from release player
/// builds. They stay active in the Unity Editor and in Development Builds.
///
/// Use this for informational / diagnostic logging. For genuine errors that must surface even in
/// production, call <c>UnityEngine.Debug.LogError</c> directly.
/// </summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message) => UnityEngine.Debug.Log(message);

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(object message) => UnityEngine.Debug.LogWarning(message);
}
