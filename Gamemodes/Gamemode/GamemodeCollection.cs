using UnityEngine;

[CreateAssetMenu(fileName = "GamemodeCollection", menuName = "Scriptable Objects/Gamemodes/GamemodeCollection")]
public class GamemodeCollection : ScriptableObject
{
    public GamemodeData[] gamemodes;

    public string[] GetGamemodeTitles()
    {
        string[] titles = new string[gamemodes.Length];
        for (int i = 0; i < gamemodes.Length; i++)
        {
            titles[i] = gamemodes[i].friendlyName;
        }

        return titles;
    }
}
