using UnityEngine;

public enum GameDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

// Stores the player's difficulty choice. It carries between scenes and is remembered the next time the game opens.
public static class GameSettings
{
    private const string DifficultyKey = "Difficulty";

    public static GameDifficulty Difficulty
    {
        get
        {
            return (GameDifficulty)PlayerPrefs.GetInt(DifficultyKey, (int)GameDifficulty.Medium);
        }
        set
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
        }
    }
}
