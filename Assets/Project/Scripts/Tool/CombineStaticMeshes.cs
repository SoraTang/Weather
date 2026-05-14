using UnityEngine;

public class CombineStaticMeshes : MonoBehaviour
{
    [ContextMenu("Combine Child Meshes")]
    private void CombineChildMeshes()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("No MeshFilters found under this object.");
            return;
        }

        CombineInstance[] combines = new CombineInstance[meshFilters.Length];

        Material sharedMaterial = null;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();

            if (mf.sharedMesh == null)
            {
                Debug.LogWarning($"MeshFilter {mf.name} has no mesh.");
                continue;
            }

            if (mr != null && sharedMaterial == null)
            {
                sharedMaterial = mr.sharedMaterial;
            }

            combines[i].mesh = mf.sharedMesh;
            combines[i].transform = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.name = gameObject.name + "_CombinedMesh";

        // 如果总顶点数可能超过 65535，需要这一行
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        combinedMesh.CombineMeshes(combines, true, true);

        MeshFilter targetMeshFilter = GetComponent<MeshFilter>();
        if (targetMeshFilter == null)
        {
            targetMeshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer targetMeshRenderer = GetComponent<MeshRenderer>();
        if (targetMeshRenderer == null)
        {
            targetMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        targetMeshFilter.sharedMesh = combinedMesh;

        if (sharedMaterial != null)
        {
            targetMeshRenderer.sharedMaterial = sharedMaterial;
        }

        // 关闭子物体的渲染器，保留原物体作为备份
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.gameObject == gameObject) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
            }
        }

        Debug.Log($"Combined {meshFilters.Length} meshes into {gameObject.name}.");
    }
}