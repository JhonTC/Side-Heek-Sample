using Riptide;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

[CreateAssetMenu(fileName = "HideAndSeekData", menuName = "Scriptable Objects/Gamemodes/HideAndSeekData")]
public class HideAndSeekData : GamemodeData<HideAndSeekRules>
{
    public float proximityMax;
    public float seenDistanceMax;


    public override Gamemode GetGamemode(int id)
    {
        return new HideAndSeekMode(id, this);
    }
}

[System.Serializable]
public class HideAndSeekRules : Gamerules
{
    public LimitedRule<float> gameLength = new LimitedRule<float>("Game Length", 180, 60, 600, "s");
    public LimitedRule<float> numberOfHunters = new LimitedRule<float>("Number of Hunters", 1, 1, 4); //todo: - requires setup to actually spawn more hunters
    public EnumRule<CatchMethod> catchType = new EnumRule<CatchMethod>("Catch Method", CatchMethod.OnFlop);
    public LimitedRule<float> hidingTime = new LimitedRule<float>("Hiding Time", 20, 0, 120, "s");
    public EnumRule<SpeedBoostTarget> speedBoostTarget = new EnumRule<SpeedBoostTarget>("Speed Boost Target", SpeedBoostTarget.FirstHunter);
    public LimitedRule<float> speedMultiplier = new LimitedRule<float>("Hunter Speed Multiplier", 1.1f, 0.8f, 2f, "x");
    public EnumRule<FallRespawnRole> fallRespawnRole = new EnumRule<FallRespawnRole>("Fall Respawn Role", FallRespawnRole.Hunter);
    public EnumRule<FallRespawnLocation> fallRespawnLocation = new EnumRule<FallRespawnLocation>("Fall Respawn Location", FallRespawnLocation.Centre);

    public override Gamerules Duplicate()
    {
        var gamerules = new HideAndSeekRules();
        gamerules.CopyFrom(this);
        return gamerules;
    }

    public override void AddMessageValues(ref Message message)
    {
        base.AddMessageValues(ref message);

        message.AddFloat(gameLength.value);
        message.AddFloat(numberOfHunters.value);
        message.AddInt(catchType.enumIndex);
        message.AddFloat(hidingTime.value);
        message.AddInt(speedBoostTarget.enumIndex);
        message.AddFloat(speedMultiplier.value);
        message.AddInt(fallRespawnRole.enumIndex);
        message.AddInt(fallRespawnLocation.enumIndex);
    }

    public override void ReadMessageValues(Message message)
    {
        base.ReadMessageValues(message);

        gameLength.SetValue(message.GetFloat());
        numberOfHunters.SetValue(message.GetFloat());
        catchType.SetValue((CatchMethod)message.GetInt());
        hidingTime.SetValue(message.GetFloat());
        speedBoostTarget.SetValue((SpeedBoostTarget)message.GetInt());
        speedMultiplier.SetValue(message.GetFloat());
        fallRespawnRole.SetValue((FallRespawnRole)message.GetInt());
        fallRespawnLocation.SetValue((FallRespawnLocation)message.GetInt());
    }

    public override void CopyFrom(Gamerules other)
    {
        HideAndSeekRules copyFrom = other as HideAndSeekRules;
        if (copyFrom == null) return;

        base.CopyFrom(copyFrom);

        gameLength.SetValue(copyFrom.gameLength.value);
        numberOfHunters.SetValue(copyFrom.numberOfHunters.value);
        catchType.SetValue(copyFrom.catchType.value);
        hidingTime.SetValue(copyFrom.hidingTime.value);
        speedBoostTarget.SetValue(copyFrom.speedBoostTarget.value);
        speedMultiplier.SetValue(copyFrom.speedMultiplier.value);
        fallRespawnRole.SetValue(copyFrom.fallRespawnRole.value);
        fallRespawnLocation.SetValue(copyFrom.fallRespawnLocation.value);
    }

    public override void SetupUI(Transform parent)
    {
        UIUtils.CreateUI(gameLength, parent);
        //UIUtils.CreateUI(numberOfHunters, parent);
        UIUtils.CreateUI<CatchMethod>(catchType, parent);
        UIUtils.CreateUI(hidingTime, parent);
        UIUtils.CreateUI<SpeedBoostTarget>(speedBoostTarget, parent);
        UIUtils.CreateUI(speedMultiplier, parent, false);
        UIUtils.CreateUI<FallRespawnRole>(fallRespawnRole, parent);
        UIUtils.CreateUI<FallRespawnLocation>(fallRespawnLocation, parent);

        base.SetupUI(parent); //Spawn base values after so they appear at the bottom of the list
    }

    public override void OnDisplay(bool canControlPanel)
    {
        base.OnDisplay(canControlPanel);

        gameLength.OnDisplay(canControlPanel);
        //numberOfHunters.OnDisplay(canControlPanel);
        catchType.OnDisplay(canControlPanel);
        hidingTime.OnDisplay(canControlPanel);
        speedBoostTarget.OnDisplay(canControlPanel);
        speedMultiplier.OnDisplay(canControlPanel);
        fallRespawnRole.OnDisplay(canControlPanel);
        fallRespawnLocation.OnDisplay(canControlPanel);
    }
}

public class HideAndSeekMode : Gamemode<HideAndSeekRules, HideAndSeekData, HideAndSeekPanel>
{
    private bool isHunterVictory = false;
    private string activeHunterSceneName = "Lobby";
    private Player firstHunter;

    public HideAndSeekMode(int id, HideAndSeekData gamemodeData) : base(id, gamemodeData) {}

    private Player GetRandomPlayerAvoidingLastHunters()
    {
        List<Player> randomPlayers = Player.RegisteredPlayers.Values.Where(p => !GameManager.instance.lastMainHunterPlayers.Contains(p)).ToList();

        if (randomPlayers.Count <= 0)
        {
            randomPlayers = Player.RegisteredPlayers.Values.ToList();
        }

        return randomPlayers[Random.Range(0, randomPlayers.Count)];
    }

    private Player GetRandomPlayer()
    {
        Player[] randomPlayers = Player.RegisteredPlayers.Values.ToArray();

        return randomPlayers[Random.Range(0, randomPlayers.Length)];
    }

    public override async Task Setup(bool asRematch = false)
    {
        await base.Setup(asRematch);

        if (NetworkManager.IsServer)
        {
            firstHunter = GetRandomPlayer();
            GameManager.instance.lastMainHunterPlayers.Clear();

            foreach (Player player in Player.RegisteredPlayers.Values)
            {
                if (player != null)
                {
                    PlayerType _playerType = PlayerType.Default;
                    if (player.id == firstHunter.id)
                    {
                        _playerType = PlayerType.Hunter;
                        GameManager.instance.lastMainHunterPlayers.Add(player);
                    }
                    else
                    {
                        CustomisationManager.SetColourForPlayer(player, player.playerColours.GetColour(PlayerType.Default), PlayerType.Hider);
                        _playerType = PlayerType.Hider;
                    }
                    player.SetPlayerType(_playerType);
                }
            }
        }
    }

    public override void Start()
    {
        base.Start();

        if (NetworkManager.IsServer)
        {
            string hiderSceneName = LevelManager.activeSceneName;
            LevelManager hiderLevelManager = LevelManager.GetLevelManagerForScene(hiderSceneName);

            string hunterSceneName = LevelManager.lobbySceneName;
            LevelManager hunterLevelManager = LevelManager.GetLevelManagerForScene(hunterSceneName);

            foreach (Player player in Player.RegisteredPlayers.Values)
            {
                if (player.playerType == PlayerType.Hider)
                {
                    Transform teleportTarget = hiderLevelManager.GetNextSpawnpoint(GameManager.gameState != GameState.InProgress && player.isHost);
                    player.TeleportPlayer(teleportTarget, hiderSceneName);
                }

                if (player.playerType == PlayerType.Hunter && player.playerScene != hunterSceneName)
                {
                    Transform teleportTarget = hunterLevelManager.GetNextSpawnpoint(GameManager.gameState != GameState.InProgress && player.isHost);
                    player.TeleportPlayer(teleportTarget, hunterSceneName);
                }
            }

            Timer.Create((int)rules.hidingTime.value)
                .OnTick((timeRemaining) =>
                {
                    this.timeRemaining = timeRemaining;

                    ServerSend.UpdateGamemodeRuntimeValues(this);

                    gameplayPanel.UpdateValues();
                })
                .OnComplete(OnHidingTimerComplete).Start();
        }
    }

    private void OnHidingTimerComplete()
    {
        activeHunterSceneName = LevelManager.activeSceneName;

        Transform hunterSpawnpoint = LevelManager.GetLevelManagerForScene(activeHunterSceneName).GetNextSpawnpoint(true);

        foreach (Player player in Player.RegisteredPlayers.Values)
        {
            if (player.playerType == PlayerType.Hunter)
            {
                player.TeleportPlayer(hunterSpawnpoint, activeHunterSceneName);
            }
        }

        BeginGameTimer((int)rules.gameLength.value);
    }

    public override void Finish()
    {
        base.Finish();
        
        CalculateWinners();

        //UIManager.instance.SetSpecialMessage($"Game Over, {(isHunterVictory ? "Hunters" : "Hiders")} Win!");

        if (NetworkManager.IsServer)
        {
            activeHunterSceneName = "Lobby";
        }
    }

    public override void AddToEndMessage(ref Message message)
    {
        message.AddBool(isHunterVictory);
    }
    public override void ReadFromEndMessage(Message message)
    {
        bool isHunterVictory = message.GetBool();
    }

    public override bool CheckForGameOver()
    {
        if (GameManager.gameState != GameState.InProgress) return false;

        int hunterCount = 0;
        int hiderCount = 0;

        foreach (Player player in Player.RegisteredPlayers.Values)
        {
            if (player.playerType == PlayerType.Hider)
            {
                hiderCount++;
            }
            if (player.playerType == PlayerType.Hunter)
            {
                hunterCount++;
            }
        }

        return hiderCount == 0 || hunterCount == 0;
    }

    private void CalculateWinners()
    {
        int hunterCount = 0;
        int hiderCount = 0;

        foreach (Player player in Player.RegisteredPlayers.Values)
        {
            if (player.playerType == PlayerType.Hider)
            {
                hiderCount++;
            }
            if (player.playerType == PlayerType.Hunter)
            {
                hunterCount++;
            }
        }

        isHunterVictory = true;
        if (hunterCount == 0)
        {
            isHunterVictory = false;
        }

        winnerMessage = isHunterVictory ? "Seekers Win!" : "Hiders Win!";

        ServerSend.UpdateGamemodeRuntimeValues(this);
        gameplayPanel.UpdateValues();
    }

    public override void OnPlayerCollision(Player player, Player other)
    {
        if (!other.isBodyActive) return;

        if (player.playerType == PlayerType.Hunter && other.playerType != PlayerType.Hunter && GameManager.gameState == GameState.InProgress)
        {
            if (player.activePlayerCollisionIds.Contains(other.id)) return;
            player.activePlayerCollisionIds.Add(other.id);

            if (rules.catchType.value == CatchMethod.OnFlop && !player.playerMotor.CanKnockOutOther(other)) return;
                
            other.playerMotor.OnCollisionWithOther(3f);

            OnSeekerHitHider(player, other);
        }
        else
        {
            if (!player.playerMotor.CanKnockOutOther(other)) return;
            
            other.playerMotor.OnCollisionWithOther(other.playerType == PlayerType.Hunter ? 2f : 3f);
        }
    }

    private void OnSeekerHitHider(Player seeker, Player hider)
    {
        seeker.gamemodeValues.Change(PlayerGamemodeValueType.Catches, 1);
        ServerSend.UpdateGamemodeRuntimeValues(this);
        gameplayPanel.UpdateValues();

        Timer.Create(1)
            .OnComplete(() =>
            {
                hider.SetPlayerType(PlayerType.Hunter, showPoofEffect: true);
            }).Start();
        
        Timer.Create(3)
            .OnComplete(() =>
            {
                if (CheckForGameOver())
                {
                    Finish();
                }
            }).Start();
    }

    public override void OnPlayerFallFromMap(Player player)
    {
        if (GameManager.gameState != GameState.InProgress)
        {
            base.OnPlayerFallFromMap(player);
            return;
        }

        string activeSceneName = LevelManager.activeSceneName;
        if (player.playerType == PlayerType.Hunter)
        {
            activeSceneName = activeHunterSceneName;
        }
        else
        {
            if (rules.fallRespawnRole.value == FallRespawnRole.Hunter)
            {
                player.SetPlayerType(PlayerType.Hunter);
            }
        }

        Transform teleportTarget = LevelManager.GetLevelManagerForScene(activeSceneName)
            .GetNextSpawnpoint(rules.fallRespawnLocation.value == FallRespawnLocation.Centre);
        player.TeleportPlayer(teleportTarget, activeSceneName);

        Timer.Create(2)
            .OnComplete(() =>
            {
                if (CheckForGameOver())
                {
                    Finish();
                }
            }).Start();
    }

    protected override void OnPlayerTypeChanged(Player player)
    {
        float speedMultiplier = 1;

        if (player.playerType == PlayerType.Hunter)
        {
            int hunterCount = 0;
            foreach (Player registeredPlayer in Player.RegisteredPlayers.Values)
            {
                if (registeredPlayer.playerType == PlayerType.Hunter)
                {
                    hunterCount++;
                }
            }

            bool isFirstHunter = hunterCount == 1;

            switch (rules.speedBoostTarget.value)
            {
                case SpeedBoostTarget.FirstHunter:
                    if (isFirstHunter)
                    {
                        speedMultiplier = rules.speedMultiplier.value;
                    }
                    break;
                case SpeedBoostTarget.AllHunters:
                    speedMultiplier = rules.speedMultiplier.value;
                    break;
            }
        }
    }

    public override void AddRuntimeMessageValues(ref Message message)
    {
        message.AddInt(timeRemaining);

        var hunters = Player.RegisteredPlayers.Values.Where(player =>
            player.playerType == PlayerType.Hunter || (player.playerType != PlayerType.Hider && player.lastPlayerType == PlayerType.Hunter));

        var hiders = Player.RegisteredPlayers.Values.Where(player =>
            player.playerType == PlayerType.Hider || (player.playerType != PlayerType.Hunter && player.lastPlayerType == PlayerType.Hider));

        message.AddInt(hunters.Count());
        foreach (Player player in hunters)
        {
            message.AddUShort(player.id);

            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Score));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Catches));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Countdown));
        }

        message.AddInt(hiders.Count());
        foreach (Player player in hiders)
        {
            message.AddUShort(player.id);

            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Score));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Proximity));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Seen));
        }

        base.AddRuntimeMessageValues(ref message);
    }

    public override void ReadRuntimeMessageValues(Message message)
    {
        timeRemaining = message.GetInt();

        int hunterCount = message.GetInt();
        for (int i = 0; i < hunterCount; i++)
        {
            ushort playerId = message.GetUShort();
            float score = message.GetFloat();
            float catches = message.GetFloat();
            float countdown = message.GetFloat();

            if (Player.RegisteredPlayers.TryGetValue(playerId, out Player player))
            {
                player.gamemodeValues.Set(PlayerGamemodeValueType.Score, score);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Catches, catches);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Countdown, countdown);
            }
        }

        int hiderCount = message.GetInt();
        for (int i = 0; i < hiderCount; i++)
        {
            ushort playerId = message.GetUShort();
            float score = message.GetFloat();
            float proximity = message.GetFloat();
            float seen = message.GetFloat();

            if (Player.RegisteredPlayers.TryGetValue(playerId, out Player player))
            {
                player.gamemodeValues.Set(PlayerGamemodeValueType.Score, score);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Proximity, proximity);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Seen, seen);
            }
        }

        base.ReadRuntimeMessageValues(message);
    }

    public override void CalculatePlayerScore(Player player)
    {
        var playerValues = player.gamemodeValues;

        float score = 0;

        var playerType = player.playerType;
        if (playerType == PlayerType.Spectator)
        {
            playerType = player.lastPlayerType;
        }

        if (playerType == PlayerType.Hunter)
        {
            score += playerValues.Get(PlayerGamemodeValueType.Catches) * 100
            + playerValues.Get(PlayerGamemodeValueType.Countdown) * 10;
        }

        if (playerType == PlayerType.Hider)
        {
            score += playerValues.Get(PlayerGamemodeValueType.Proximity) * 15
            + playerValues.Get(PlayerGamemodeValueType.Seen) * 25;
        }

        playerValues.Set(PlayerGamemodeValueType.Score, score);
    }

    protected override void OnCountdownToPlayComplete()
    {
        base.OnCountdownToPlayComplete();

        BeginGameTimer((int)rules.gameLength.value);
    }

    protected override void OnGameTimerTick(int countdown)
    {
        base.OnGameTimerTick(countdown);

        var hunters = Player.RegisteredPlayers.Values.Where(player =>
            player.playerType == PlayerType.Hunter || (player.playerType != PlayerType.Hider && player.lastPlayerType == PlayerType.Hunter));

        var hiders = Player.RegisteredPlayers.Values.Where(player =>
            player.playerType == PlayerType.Hider || (player.playerType != PlayerType.Hunter && player.lastPlayerType == PlayerType.Hider));

        foreach (var hider in hiders)
        {
            foreach (var hunter in hunters)
            {
                float distance = Vector3.Distance(hider.playerMotor.root.position, hunter.playerMotor.root.position);

                if (distance <= data.proximityMax)
                {
                    hider.gamemodeValues.Change(PlayerGamemodeValueType.Proximity, 1);
                    break;
                }
            }

            if (hider.playerMotor.inputSpeed == 0)
            {
                foreach (var hunter in hunters)
                {
                    //float yDirection = (hider.playerMotor.root.position - hunter.playerMotor.root.position).normalized.y;
                    Vector3 direction = hunter.playerMotor.root.transform.forward;
                    //direction.y = yDirection;

                    if (Physics.Raycast(hunter.playerMotor.root.position, direction.normalized, out RaycastHit hit, data.seenDistanceMax))
                    {
                        Player playerHit = hit.collider.GetComponent<BodyCollisionDetection>()?.player;

                        if (playerHit == hider)
                        {
                            hider.gamemodeValues.Change(PlayerGamemodeValueType.Seen, 1);
                            break;
                        }
                    }
                }
            }
        }

        if (firstHunter != null)
        {
            firstHunter.gamemodeValues.Set(PlayerGamemodeValueType.Countdown, countdown);
        }
    }
}

