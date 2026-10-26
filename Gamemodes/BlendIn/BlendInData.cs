using Riptide;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "BlendInData", menuName = "Scriptable Objects/Gamemodes/BlendInData")]
public class BlendInData : GamemodeData<BlendInRules>
{
    public float proximityMax;

    public override Gamemode GetGamemode(int id)
    {
        return new BlendInMode(id, this);
    }
}

[System.Serializable]
public class BlendInRules : Gamerules
{
    public LimitedRule<float> gameLength = new LimitedRule<float>("Game Length", 180, 60, 600, "s");
    public LimitedRule<float> numberOfHunters = new LimitedRule<float>("Number of Hunters", 1, 1, 4); //todo: - requires setup to actually spawn more hunters
    public LimitedRule<float> prepTime = new LimitedRule<float>("Prep Time", 10, 10, 60, "s");
    public LimitedRule<float> hunterLives = new LimitedRule<float>("Hunter Lives", 3, 1, 10);
    public LimitedRule<float> speedMultiplier = new LimitedRule<float>("Hunter Speed Multiplier", 1.1f, 0.8f, 2f, "x");
    public LimitedRule<float> actionDelay = new LimitedRule<float>("Time Between Actions", 20, 8, 120, "s");

    public override Gamerules Duplicate()
    {
        var gamerules = new BlendInRules();
        gamerules.CopyFrom(this);
        return gamerules;
    }

    public override void AddMessageValues(ref Message message)
    {
        base.AddMessageValues(ref message);

        message.AddFloat(gameLength.value);
        message.AddFloat(numberOfHunters.value);
        message.AddFloat(prepTime.value);
        message.AddFloat(hunterLives.value);
        message.AddFloat(speedMultiplier.value);
        message.AddFloat(actionDelay.value);
    }

    public override void ReadMessageValues(Message message)
    {
        base.ReadMessageValues(message);

        gameLength.SetValue(message.GetFloat());
        numberOfHunters.SetValue(message.GetFloat());
        prepTime.SetValue(message.GetFloat());
        hunterLives.SetValue(message.GetFloat());
        speedMultiplier.SetValue(message.GetFloat());
        actionDelay.SetValue(message.GetFloat());
    }

    public override void CopyFrom(Gamerules other)
    {
        BlendInRules copyFrom = other as BlendInRules;
        if (copyFrom == null) return;

        base.CopyFrom(copyFrom);

        gameLength.SetValue(copyFrom.gameLength.value);
        numberOfHunters.SetValue(copyFrom.numberOfHunters.value);
        prepTime.SetValue(copyFrom.prepTime.value);
        hunterLives.SetValue(copyFrom.hunterLives.value);
        speedMultiplier.SetValue(copyFrom.speedMultiplier.value);
        actionDelay.SetValue(copyFrom.actionDelay.value);
    }

    public override void SetupUI(Transform parent)
    {
        UIUtils.CreateUI(gameLength, parent);
        //UIUtils.CreateUI(numberOfHunters, parent);
        UIUtils.CreateUI(prepTime, parent);
        UIUtils.CreateUI(hunterLives, parent);
        UIUtils.CreateUI(speedMultiplier, parent, false);
        UIUtils.CreateUI(actionDelay, parent, false);

        base.SetupUI(parent); //Spawn base values after so they appear at the bottom of the list
    }

    public override void OnDisplay(bool canControlPanel)
    {
        base.OnDisplay(canControlPanel);

        gameLength.OnDisplay(canControlPanel);
        //numberOfHunters.OnDisplay(canControlPanel);
        prepTime.OnDisplay(canControlPanel);
        hunterLives.OnDisplay(canControlPanel);
        speedMultiplier.OnDisplay(canControlPanel);
        actionDelay.OnDisplay(canControlPanel);
    }
}

public class BlendInMode : Gamemode<BlendInRules, BlendInData, BlendInPanel>
{
    private bool isHunterVictory = false;
    private string activeHunterSceneName = "Lobby";
    private string spectatorSceneName = "Lobby";

    private List<ushort> npcs = new List<ushort>();

    private Player firstHunter;

    private Timer recursiveActionTimer = null;

    public BlendInMode(int id, BlendInData gamemodeData) : base(id, gamemodeData) {}

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
                        _playerType = PlayerType.Hider;
                    }

                    player.SetPlayerType(_playerType, _playerType == PlayerType.Hunter);
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

                    CustomisationManager.SetColourForPlayer(player, CustomisationManager.GetRandomColor(), player.playerType);
                }

                if (player.playerType == PlayerType.Hunter)
                {
                    if (player.playerScene != hunterSceneName)
                    {
                        Transform teleportTarget = hunterLevelManager.GetNextSpawnpoint(GameManager.gameState != GameState.InProgress && player.isHost);
                        player.TeleportPlayer(teleportTarget, hunterSceneName);
                    }

                    player.gamemodeValues.Set(PlayerGamemodeValueType.Lives, rules.hunterLives.value);
                    ServerSend.UpdateGamemodeRuntimeValues(this);
                    gameplayPanel.UpdateValues();
                }
            }

            Timer.Create((int)rules.prepTime.value)
                .OnTick((timeRemaining) =>
                {
                    this.timeRemaining = timeRemaining;

                    ServerSend.UpdateGamemodeRuntimeValues(this);

                    gameplayPanel.UpdateValues();
                })
                .OnComplete(OnPrepTimerComplete).Start();


            for (int i = 0; i < 10; i++)
            {
                ushort npcId = NetworkIdentityService.instance.GetNextAvaliableId();
                Player.CreatePlayer(npcId, "", PlayerType.NPC);

                npcs.Add(npcId);
            }
        }
    }

    private void OnPrepTimerComplete()
    {
        activeHunterSceneName = LevelManager.activeSceneName;

        foreach (Player player in Player.RegisteredPlayers.Values)
        {
            if (player.playerType == PlayerType.Hunter)
            {
                Transform teleportTarget = LevelManager.GetLevelManagerForScene(activeHunterSceneName).GetNextSpawnpoint(true);
                player.TeleportPlayer(teleportTarget, activeHunterSceneName);
            }
        }

        BeginGameTimer((int)rules.gameLength.value);
        PerformRecursiveAction();
    }

    private void PerformRecursiveAction()
    {
        float actionDelay = rules.actionDelay.value;
        if (timeRemaining - actionDelay < actionDelay + 10f) return; //breaks recursive function

        int randomAction = Random.Range(0, 3); //yields values of 0 - 1

        recursiveActionTimer = Timer.Create(actionDelay)
            .OnTick((countdown) =>
            {
                bool updateMessage = false;
                if (countdown == 4)
                {
                    messageDuration = 1f;
                    mainMessage = "Action in...";
                    updateMessage = true;
                }
                if (countdown > 0 && countdown < 4)
                {
                    messageDuration = 1f;
                    mainMessage = countdown.ToString();
                    updateMessage = true;
                }

                if (updateMessage)
                {
                    ServerSend.UpdateGamemodeRuntimeValues(this);
                    gameplayPanel.UpdateValues();
                }
            })
            .OnComplete(() =>
            {
                messageDuration = 1.5f;

                switch (randomAction)
                {
                    case 0:
                        // Flop
                        mainMessage = "Flop!";
                        break;
                    case 1:
                        // Jump
                        mainMessage = "Jump!";
                        break;
                    case 2:
                        // Stand still
                        mainMessage = "Stop!";
                        break;
                }

                ServerSend.UpdateGamemodeRuntimeValues(this);
                gameplayPanel.UpdateValues();

                Timer.Create(0.4f)
                    .OnComplete(() =>
                    {
                        IEnumerable<Player> npcs = Player.RegisteredPlayers.Values.Where(x => x.playerType == PlayerType.NPC);

                        switch (randomAction)
                        {
                            case 0:
                                // Flop
                                foreach (Player npc in npcs)
                                {
                                    npc.playerMotor.ForceFlop();
                                }
                                break;
                            case 1:
                                // Jump
                                foreach (Player npc in npcs)
                                {
                                    npc.playerMotor.ForceJump();
                                }
                                break;
                            case 2:
                                // Stand still
                                foreach (Player npc in npcs)
                                {
                                    npc.playerMotor.ForceStop(2f);
                                }
                                break;
                        }

                        PerformRecursiveAction();
                    }).Start();
            }).Start();
    }

    public override void Finish()
    {
        base.Finish();

        if (recursiveActionTimer != null)
        {
            recursiveActionTimer.Stop();
        }

        CalculateWinners();

        //UIManager.instance.SetSpecialMessage($"Game Over, {(isHunterVictory ? "Hunters" : "Hiders")} Win!");

        if (NetworkManager.IsServer)
        {
            for (int i = npcs.Count - 1; i >= 0; i--)
            {
                if (Player.TryGetRegisteredPlayer(npcs[i], out Player npc))
                {
                    npc.DespawnBody();
                    Object.Destroy(npc.gameObject);
                }
            }

            activeHunterSceneName = "Lobby";
        }
    }

    public override async Task End(bool asRematch = false)
    {
        await base.End(asRematch);

        foreach (var player in Player.RegisteredPlayers.Values)
        {
            CustomisationManager.SetColourForPlayer(player, player.playerColours.GetColour(PlayerType.Default), PlayerType.Hider);
            player.overrideStanding = false;
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
        if (hiderCount > 0)
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
        if (!player.playerMotor.CanKnockOutOther(other)) return;

        if (player.playerType == PlayerType.Hunter && other.playerType != PlayerType.Hunter && GameManager.gameState == GameState.InProgress)
        {
            if (player.activePlayerCollisionIds.Contains(other.id)) return;
            player.activePlayerCollisionIds.Add(other.id);

            player.playerMotor.flopPlayerCollisionIds.Add(other.id);

            other.playerMotor.OnCollisionWithOther(3f);

            if (other.playerType == PlayerType.NPC)
            {
                OnSeekerHitNPC(player);

                return;
            }

            if (other.playerType != PlayerType.Hider) return;

            OnSeekerHitHider(player, other);
        }
        else
        {
            other.playerMotor.OnCollisionWithOther(other.playerType == PlayerType.Hunter ? 2f : 3f);
        }
    }

    private void OnSeekerHitNPC(Player seeker)
    {
        seeker.gamemodeValues.Change(PlayerGamemodeValueType.Lives, -1);
        ServerSend.UpdateGamemodeRuntimeValues(this);
        gameplayPanel.UpdateValues();

        if (seeker.gamemodeValues.GetInt(PlayerGamemodeValueType.Lives) <= 0)
        {
            Timer.Create(2)
                .OnComplete(() =>
                {
                    CustomisationManager.SetColourForPlayer(seeker, seeker.playerColours.GetColour(seeker.playerType), PlayerType.Spectator);
                    seeker.SetPlayerType(PlayerType.Spectator, despawnSpectatorBody: false);
                    seeker.overrideStanding = true;

                    if (CheckForGameOver())
                    {
                        Finish();
                    }

                    //Transform playerTeleportTarget = LevelManager.GetLevelManagerForScene(spectatorSceneName).GetNextSpawnpoint(true);
                    //seeker.TeleportPlayer(playerTeleportTarget, spectatorSceneName, false);
                }).Start();
        }
    }

    private void OnSeekerHitHider(Player seeker, Player hider)
    {
        seeker.gamemodeValues.Change(PlayerGamemodeValueType.Catches, 1);
        ServerSend.UpdateGamemodeRuntimeValues(this);
        gameplayPanel.UpdateValues();

        Timer.Create(2)
            .OnComplete(() =>
            {
                CustomisationManager.SetColourForPlayer(hider, hider.playerColours.GetColour(hider.playerType), PlayerType.Spectator);
                hider.SetPlayerType(PlayerType.Spectator, despawnSpectatorBody: false);
                hider.overrideStanding = true;

                if (CheckForGameOver())
                {
                    Finish();
                }

                //Transform otherTeleportTarget = LevelManager.GetLevelManagerForScene(spectatorSceneName).GetNextSpawnpoint(true);
                //hider.TeleportPlayer(otherTeleportTarget, spectatorSceneName, false);
            }).Start();
    }

    protected override void OnPlayerTypeChanged(Player player)
    {
        float speedMultiplier = 1;

        if (player.playerType == PlayerType.Hunter)
        {
            speedMultiplier = rules.speedMultiplier.value;
        }
    }

    public override void OnPlayerFallFromMap(Player player)
    {
        if (GameManager.gameState != GameState.InProgress)
        {
            base.OnPlayerFallFromMap(player);
            return;
        }

        string activeSceneName = LevelManager.activeSceneName;
        if (player.playerType == PlayerType.Hunter || player.playerType == PlayerType.NPC)
        {
            activeSceneName = activeHunterSceneName;

            Transform teleportTarget = LevelManager.GetLevelManagerForScene(activeSceneName).GetNextSpawnpoint(true);
            player.TeleportPlayer(teleportTarget, activeSceneName);
        }
        else if (player.playerType == PlayerType.Hider)
        {
            player.SetPlayerType(PlayerType.Spectator);
            player.overrideStanding = true;

            Transform teleportTarget = LevelManager.GetLevelManagerForScene(spectatorSceneName).GetNextSpawnpoint(true);
            player.TeleportPlayer(teleportTarget, spectatorSceneName, false);
        }

        Timer.Create(2)
            .OnComplete(() =>
            {
                if (CheckForGameOver())
                {
                    Finish();
                }
            }).Start();
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
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Lives));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Catches));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Countdown));
        }

        message.AddInt(hiders.Count());
        foreach (Player player in hiders)
        {
            message.AddUShort(player.id);

            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Score));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Proximity));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Flops));
            message.AddFloat(player.gamemodeValues.Get(PlayerGamemodeValueType.Jumps));
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
            float lives = message.GetFloat();
            float catches = message.GetFloat();
            float countDown = message.GetFloat();

            if (Player.RegisteredPlayers.TryGetValue(playerId, out Player player))
            {
                player.gamemodeValues.Set(PlayerGamemodeValueType.Score, score);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Lives, lives);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Catches, catches);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Countdown, countDown);
            }
        }

        int hiderCount = message.GetInt();
        for (int i = 0; i < hiderCount; i++)
        {
            ushort playerId = message.GetUShort();
            float score = message.GetFloat();
            float proximity = message.GetFloat();
            float flops = message.GetFloat();
            float jumps = message.GetFloat();

            if (Player.RegisteredPlayers.TryGetValue(playerId, out Player player))
            {
                player.gamemodeValues.Set(PlayerGamemodeValueType.Score, score);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Proximity, proximity);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Flops, flops);
                player.gamemodeValues.Set(PlayerGamemodeValueType.Jumps, jumps);
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
            float lives = playerValues.Get(PlayerGamemodeValueType.Lives);

            score += lives * 40
            + playerValues.Get(PlayerGamemodeValueType.Catches) * 100;

            if (lives > 0 && GameManager.gameState == GameState.Ending)
            {
                score += playerValues.Get(PlayerGamemodeValueType.Countdown) * 10;
            }
        }

        if (playerType == PlayerType.Hider)
        {
            score += playerValues.Get(PlayerGamemodeValueType.Proximity) * 15
            + playerValues.Get(PlayerGamemodeValueType.Flops) * 50
            + playerValues.Get(PlayerGamemodeValueType.Jumps) * 25;
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

        var hunters = Player.RegisteredPlayers.Values.Where(player => player.playerType == PlayerType.Hunter);
        var hiders = Player.RegisteredPlayers.Values.Where(player => player.playerType == PlayerType.Hider);

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
        }

        if (firstHunter != null)
        {
            firstHunter.gamemodeValues.Set(PlayerGamemodeValueType.Countdown, countdown);
        }
    }
}