using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BasicGamemodeData", menuName = "Scriptable Objects/Gamemodes/BasicData")]
public class BasicGamemodeData : GamemodeData<BasicGamerules>
{
    public override Gamemode GetGamemode(int id)
    {
        return new BasicGamemode(id, this);
    }
}

[Serializable]
public class BasicGamerules : Gamerules
{
    public override Gamerules Duplicate()
    {
        var gamerules = new BasicGamerules();
        gamerules.CopyFrom(this);
        return gamerules;
    }

    public override void SetupUI(Transform parent)
    {}

    public override void OnDisplay(bool canControlPanel)
    {}
}

[Serializable]
public class BasicGamemode : Gamemode<BasicGamerules, BasicGamemodeData, BasicGamemodePanel>
{
    public BasicGamemode(int id, BasicGamemodeData gamemodeData) : base(id, gamemodeData) {}

    public override void Start()
    {
        base.Start();
        
        if (NetworkManager.IsServer)
        {
            string sceneName = LevelManager.activeSceneName;
            var levelManager = LevelManager.GetLevelManagerForScene(sceneName);

            foreach (Player player in Player.RegisteredPlayers.Values)
            {
                Transform teleportTarget = levelManager.GetNextSpawnpoint(GameManager.gameState != GameState.InProgress && player.isHost);
                player.TeleportPlayer(teleportTarget, sceneName);
            }
        }
    }

    public override void CalculatePlayerScore(Player _) { }
}
