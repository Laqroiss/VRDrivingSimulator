#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MapTileBuilder : MonoBehaviour
{
    [MenuItem("Tools/Build Map Tiles")]
    static void BuildMap()
    {
        int   cols     = 4;
        int   rows     = 4;
        float tileSize = 62.5f; // 250 / 4

        GameObject autodrome = GameObject.Find("AutoDrome");
        Vector3    basePos   = autodrome != null ? autodrome.transform.position : Vector3.zero;
        float      totalW    = tileSize * cols;
        float      totalH    = tileSize * rows;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var old = GameObject.Find($"Tile_r{r}_c{c}");
                if (old != null) DestroyImmediate(old);
            }

        GameObject parent = GameObject.Find("MapTileGrid") ?? new GameObject("MapTileGrid");
        parent.transform.position = basePos;

        int created = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                string texPath = $"Assets/map_tiles/tile_r{row}_c{col}.png";
                Texture2D tex  = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                if (tex == null)
                {
                    Debug.LogError($"Не найдено: {texPath}");
                    continue;
                }

                GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = $"Tile_r{row}_c{col}";
                plane.transform.SetParent(parent.transform, false);

                float x = ((cols - 1 - col) * tileSize) - (totalW / 2f) + (tileSize / 2f);
                float z = (row * tileSize) - (totalH / 2f) + (tileSize / 2f);
                plane.transform.localPosition = new Vector3(x, 0f, z);
                plane.transform.localScale    = new Vector3(tileSize / 10f, 1f, tileSize / 10f);

                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", tex);
                mat.SetFloat("_Smoothness", 0.05f);

                string matPath = $"Assets/map_tiles/mat_r{row}_c{col}.mat";
                AssetDatabase.CreateAsset(mat, matPath);
                plane.GetComponent<Renderer>().sharedMaterial = mat;

                created++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"MapTileBuilder: создано {created} тайлов. Скройте AutoDrome если нужно.");
    }
}
#endif
