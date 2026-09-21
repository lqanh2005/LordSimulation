using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlacementResidentGridBatchTest
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Thi_Placement_Test.unity");
            Grid grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                Debug.LogError("[ResidentVisualTest] Scene thieu Grid.");
                EditorApplication.Exit(2);
                return;
            }

            GameObject go = new GameObject("TmpResidentManager");
            ResidentManager manager = go.AddComponent<ResidentManager>();
            manager.ConfigureVisualWorld(grid, go.transform, null, null, null);

            bool pass = Check(manager, grid, 0, 0, 1)
                        && Check(manager, grid, 3, 1, 1)
                        && Check(manager, grid, 4, 0, 2);

            Object.DestroyImmediate(go);

            if (pass)
            {
                Debug.Log("[ResidentVisualTest] BATCH PASS Grid isometric");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[ResidentVisualTest] BATCH FAIL Grid isometric");
                EditorApplication.Exit(1);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(3);
        }
    }

    private static bool Check(ResidentManager manager, Grid grid, int x, int y, int size)
    {
        Vector3Int origin = new Vector3Int(x, y, 0);
        Vector3Int top = origin + new Vector3Int(size - 1, size - 1, 0);
        Vector3 expected = (grid.GetCellCenterWorld(origin) + grid.GetCellCenterWorld(top)) * 0.5f;
        expected.z = 0f;
        Vector3 actual = manager.CellToWorld(x, y, size);
        float dist = Vector3.Distance(expected, actual);
        bool ok = dist < 0.001f;
        if (ok)
            Debug.Log($"[ResidentVisualTest] PASS cell ({x},{y}) size {size} -> {actual}");
        else
            Debug.LogError($"[ResidentVisualTest] FAIL cell ({x},{y}) size {size}. expected {expected} actual {actual} d={dist}");
        return ok;
    }
}
