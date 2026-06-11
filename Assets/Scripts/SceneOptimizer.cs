using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime draw-call optimizer for the already-built track.
///
/// The ground/curbs were generated as ~1000 individual "Bordure" cubes (plus a few
/// hundred "ControlLine" strips), each a separate renderer. Even with GPU instancing
/// that is ~4700 draw calls submitted from the CPU main thread every frame, which is
/// the current frame-time bottleneck on weak PCs.
///
/// On scene load this merges all renderers sharing a name prefix into ONE combined
/// mesh (Mesh.CombineMeshes) drawn in a single call, and disables the original
/// renderers. The source GameObjects are kept intact, so their colliders/triggers
/// (wheel-on-curb / control-line checks) keep working — only the per-cube visual is
/// replaced. No scene edits required; it bootstraps itself.
/// </summary>
public static class SceneOptimizer
{
    // Repeated static props to merge. Each prefix becomes one combined mesh / draw call.
    static readonly string[] Prefixes = { "Bordure", "ControlLine" };

    static bool _logResult = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Optimize()
    {
        // Active renderers only; intentionally hidden objects stay hidden.
        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);

        foreach (var prefix in Prefixes)
            CombineByPrefix(all, prefix);
    }

    static void CombineByPrefix(MeshRenderer[] all, string prefix)
    {
        var group = new List<MeshRenderer>();
        foreach (var r in all)
            if (r != null && r.gameObject.name.StartsWith(prefix))
                group.Add(r);

        if (group.Count == 0) return;

        // One shared material taken from the first instance so the look stays identical.
        var src = group[0].sharedMaterial;
        Material shared = src != null
            ? new Material(src)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // Gather every source mesh with its world-space transform, then drop the
        // original visual (collider/trigger components on the same object are untouched).
        var combines = new List<CombineInstance>(group.Count);
        foreach (var r in group)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            combines.Add(new CombineInstance
            {
                mesh      = mf.sharedMesh,
                transform = r.transform.localToWorldMatrix
            });
            r.enabled = false;
        }

        if (combines.Count == 0) return;

        // 32-bit indices so the merged mesh is never capped at 65k vertices.
        var combinedMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        combinedMesh.name = $"Combined_{prefix}";
        combinedMesh.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);

        var go = new GameObject($"Combined_{prefix}");
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = shared;

        if (_logResult)
            GameLog.Info($"[SceneOptimizer] Merged {combines.Count} '{prefix}' meshes into 1 draw call.");
    }
}
