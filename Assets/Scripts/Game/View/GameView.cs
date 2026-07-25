using UnityEngine;

public class GameView : MonoBehaviour
{
    [SerializeField] private GameObject _cellPrefab;

    public void DisplayMap(int level)
    {
        GameData.InitializedMap(level);
        // TODO:
        // Display the map based on GameData.MapData
    }
}