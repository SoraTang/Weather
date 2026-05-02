using UnityEditor;
using UnityEngine;

public static class SceneViewReset
{
    [MenuItem("Tools/Scene View/Reset To Front")]
    public static void ResetToFront()
    {
        SetSceneView(Quaternion.LookRotation(Vector3.forward, Vector3.up));
    }

    [MenuItem("Tools/Scene View/Reset To Top")]
    public static void ResetToTop()
    {
        SetSceneView(Quaternion.LookRotation(Vector3.down, Vector3.forward));
    }

    [MenuItem("Tools/Scene View/Reset To Iso")]
    public static void ResetToIso()
    {
        SetSceneView(Quaternion.Euler(30f, -45f, 0f));
    }

    private static void SetSceneView(Quaternion rotation)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;

        view.LookAt(view.pivot, rotation, view.size, view.orthographic, true);
        view.Repaint();
    }
}