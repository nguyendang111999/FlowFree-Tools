public static class GameData
{
    private static int _currentLevel;
    public static int CurrentLevel
    {
        get => _currentLevel;
        set => _currentLevel = value;
    }
    private static int[,] _mapData;
    public static int[,] MapData => _mapData;

    public static bool InitializedMap(int level)
    {
        CurrentLevel = level;
        // TODO:
        // Load map data from Resources/Maps
        // Fetch the map data for the specified level and store it in _mapData
        return _mapData != null;
    }
}