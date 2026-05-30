using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurvedTVScreenMesh : MonoBehaviour
{
    [Header("Screen Size")]
    [Min(0.01f)] public float width = 1.2f;
    [Min(0.01f)] public float height = 0.9f;

    [Header("Curvature")]
    public float curveDepth = 0.05f;

    [Header("Resolution")]
    [Min(2)] public int xSegments = 32;
    [Min(2)] public int ySegments = 16;

    private MeshFilter meshFilter;

    private void OnEnable()
    {
        GenerateMesh();
    }

    private void OnValidate()
    {
        GenerateMesh();
    }

    public void GenerateMesh()
    {
        if (width <= 0.001f) width = 1.2f;
        if (height <= 0.001f) height = 0.9f;
        if (xSegments < 2) xSegments = 2;
        if (ySegments < 2) ySegments = 2;

        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        Mesh mesh = new Mesh();
        mesh.name = "Curved TV Screen Mesh";

        int vertCountX = xSegments + 1;
        int vertCountY = ySegments + 1;

        Vector3[] vertices = new Vector3[vertCountX * vertCountY];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xSegments * ySegments * 6];

        int v = 0;

        for (int y = 0; y <= ySegments; y++)
        {
            float vCoord = (float)y / ySegments;
            float localY = Mathf.Lerp(-height * 0.5f, height * 0.5f, vCoord);

            for (int x = 0; x <= xSegments; x++)
            {
                float uCoord = (float)x / xSegments;
                float localX = Mathf.Lerp(-width * 0.5f, width * 0.5f, uCoord);

                float normalizedX = localX / (width * 0.5f);
                float z = curveDepth * (1f - normalizedX * normalizedX);

                if (float.IsNaN(z) || float.IsInfinity(z))
                    z = 0f;

                vertices[v] = new Vector3(localX, localY, z);
                uvs[v] = new Vector2(uCoord, vCoord);
                v++;
            }
        }

        int t = 0;

        for (int y = 0; y < ySegments; y++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int i0 = y * vertCountX + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertCountX;
                int i3 = i2 + 1;

                triangles[t++] = i0;
                triangles[t++] = i2;
                triangles[t++] = i1;

                triangles[t++] = i1;
                triangles[t++] = i2;
                triangles[t++] = i3;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }

#if UNITY_EDITOR
    [ContextMenu("Save Mesh As Asset")]
    public void SaveMeshAsAsset()
    {
        meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("No mesh found. Please generate mesh first.");
            return;
        }

        Mesh meshToSave = Instantiate(meshFilter.sharedMesh);
        meshToSave.name = "Curved_TV_Screen_Mesh";

        string folderPath = "Assets/GeneratedMeshes";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            folderPath + "/Curved_TV_Screen_Mesh.asset"
        );

        AssetDatabase.CreateAsset(meshToSave, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        meshFilter.sharedMesh = meshToSave;

        Debug.Log("Mesh saved to: " + assetPath);
    }
#endif
}