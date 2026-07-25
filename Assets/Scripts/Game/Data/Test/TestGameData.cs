using System.Text;
using UnityEngine;

public class TestGameData : MonoBehaviour
{
    [SerializeField] private int _testLevel;

    // Run this function in editor mode
    [ContextMenu(nameof(TestDisplayMap))]
    public void TestDisplayMap()
    {
        bool loaded = GameData.InitializedMap(_testLevel);
        Debug.Log($"[TestGameData] InitializedMap({_testLevel}) => {loaded}");

        if (!loaded)
        {
            Debug.LogWarning($"[TestGameData] Failed to load level {_testLevel}. Check that Resources/Maps/{_testLevel}.csv exists.");
            return;
        }

        LogGrid("Result (solution grid)", GameData.Result);
        LogGrid("DotsData (tips only)", GameData.DotsData);
        LogGrid("PlayerFillData (dots pre-filled)", GameData.PlayerFillData);
        LogPathDictionary("Solutions (color -> full solution cells)", GameData.Solutions);
        LogPathDictionary("Paths (color -> player progress, should start empty)", GameData.Paths);
    }

    private static void LogGrid(string label, int[,] grid)
    {
        int cols = grid.GetLength(0);
        int rows = grid.GetLength(1);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{label} ({cols}x{rows}), (0,0) = bottom-left:");
        // Print top row (highest y) first so it visually matches the source CSV.
        for (int y = rows - 1; y >= 0; y--)
        {
            for (int x = 0; x < cols; x++)
            {
                sb.Append(grid[x, y]).Append(' ');
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    private static void LogPathDictionary(string label, System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector2Int>> data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(label + ":");
        foreach (System.Collections.Generic.KeyValuePair<int, System.Collections.Generic.List<Vector2Int>> pair in data)
        {
            sb.AppendLine($"  Color {pair.Key}: {pair.Value.Count} cell(s) -> {string.Join(", ", pair.Value)}");
        }
        Debug.Log(sb.ToString());
    }
}