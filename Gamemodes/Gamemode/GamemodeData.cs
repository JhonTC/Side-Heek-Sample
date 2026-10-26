using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using static Player;

public enum CatchMethod 
{ OnFlop, OnTouch }

public enum SpeedBoostTarget
{ FirstHunter, AllHunters, None }

public enum FallRespawnLocation
{ Centre, Random }

public enum FallRespawnRole
{ Hider, Hunter, Spectator }

public enum GameEndReason
{ Time, Score }

public abstract class GamemodeData : ScriptableObject
{
    public string friendlyName;
    public string sceneName;
    public string[] taglines;
    [TextArea] public string description;
    public VideoClip videoClip;

    [Range(0, 5)] public int preStartCountdown = 1;
    public string countdownZeroMessage = "";
    [Range(1, 10)] public int startDelay = 1;
    [Range(1, 120)] public int endDelay = 1;
    public bool hidePlayerNames;
    public float flopDuration;
    public float knockoutDuration = 3;
    public float playerCameraZoom;
    public GamemodePanel panelPrefab;
    public bool displayPlayerTypeView;

    public AudioClip countdownProgressAudioClip;
    public AudioClip countdownEndAudioClip;

    public abstract Gamemode GetGamemode(int id);
}

public abstract class GamemodeData<R> : GamemodeData where R : Gamerules
{
    public R defaultGamerules;
}

[Serializable]
public class Gamerules
{
    public Rule<bool> allowPickups = new Rule<bool>("Pickups Enabled", true);
    public Rule<bool> continuousFlop = new Rule<bool>("Continuous Flop", true);

    public virtual Gamerules Duplicate() { return null; }

    public virtual void AddMessageValues(ref Message message)
    {
        message.AddBool(allowPickups.value);
        message.AddBool(continuousFlop.value);
    }

    public virtual void ReadMessageValues(Message message)
    {
        allowPickups.SetValue(message.GetBool());
        continuousFlop.SetValue(message.GetBool());
    }

    public virtual void CopyFrom(Gamerules other)
    {
        allowPickups.SetValue(other.allowPickups.value);
        continuousFlop.SetValue(other.continuousFlop.value);
    }

    public virtual void SetupUI(Transform parent)
    {
        //UIUtils.CreateUI(allowPickups, parent);
        //UIUtils.CreateUI(continuousFlop, parent);
    }

    public virtual void OnDisplay(bool canControlPanel)
    {
        //allowPickups.OnDisplay(canControlPanel);
        //continuousFlop.OnDisplay(canControlPanel);
    }
}

public class ScoreData {}

[Serializable]
public abstract class Gamemode
{
    public int id;

    public string mainMessage = string.Empty;
    public string subMessage = string.Empty;
    public float messageDuration = 0;
    public string winnerMessage = string.Empty;

    public static Action OnSetup;
    public static Action OnStart;
    public static Action OnFinish;
    public static Action OnEnd;
    public static Action<bool> Transition;

    public Gamemode(int id)
    {
        this.id = id;
    }

    public virtual void FixedUpdate() { }

    public abstract GamemodeData GetData();
    public abstract Gamerules GetRules();

    public abstract Task Setup(bool asRematch = false);
    public abstract void Start();
    public abstract void Finish();
    public abstract Task End(bool asRematch = false);
    public abstract Task Replay();
    public abstract Task ForceEnd();

    public virtual bool CheckForGameOver() { return false; }

    public virtual void AddToEndMessage(ref Message message) { }
    public virtual void ReadFromEndMessage(Message message) { }

    public virtual void OnSceneLoaded() { }

    public abstract Task OnPlayerJoined(Player player);

    public virtual void OnPlayerLeft(Player player) { }
    public virtual void OnPlayerFallFromMap(Player player) { }
    public virtual void OnPlayerCollision(Player player, Player other) { }
    protected virtual void OnPlayerTypeChanged(Player player) { }

    public virtual void CreatePanels() { }
    public virtual void UpdatePanels() { }
    public virtual void DestroyPanels() { }
    public virtual void ShowScoreboard(bool value, bool autoSwitchActionMap) { }
    public virtual void ToggleScoreboard(bool autoSwitchActionMap) { }

    public virtual void AddRuntimeMessageValues(ref Message message)
    {
        message.AddString(mainMessage);
        message.AddString(subMessage);
        message.AddFloat(messageDuration);
        message.AddString(winnerMessage);
    }
    public virtual void ReadRuntimeMessageValues(Message message)
    {
        mainMessage = message.GetString();
        subMessage = message.GetString();
        messageDuration = message.GetFloat();
        winnerMessage = message.GetString();
    }

    public virtual void Score(ScoreData scoreData) { }

    public abstract void CalculatePlayerScore(Player player);
}

[Serializable]
public abstract class Gamemode<R, D> : Gamemode where R : Gamerules where D : GamemodeData<R>
{
    public D data;
    public R rules;

    private Timer endScreenTimer;

    public Gamemode(int id, D gamemodeData) : base(id)
    {
        this.data = gamemodeData;
        rules = (R)gamemodeData.defaultGamerules.Duplicate();
    }

    public override GamemodeData GetData()
    {
        return data;
    }
    public override Gamerules GetRules()
    {
        return rules;
    }

    public override async Task Replay()
    {
        GameManager.gameState = GameState.Replaying;

        Transition?.Invoke(true);

        if (NetworkManager.IsServer)
        {
            ServerSend.SetGameState(GameManager.gameState);

            endScreenTimer.Stop();
            Timer.Create(1)
                .OnComplete(async () =>
                {
                    await End(true);
                    await Setup(true);
                })
                .Start();
        }
    }

    public override async Task OnPlayerJoined(Player player)
    {
        if (NetworkManager.IsServer)
        {
            if (GameManager.gameState == GameState.Starting || GameManager.gameState == GameState.InProgress)
            {
                //player.SetUsernameVisibility(player.playerType == PlayerType.Spectator ? false : !data.hidePlayerNames);

                if (LevelManager.activeSceneName != LevelManager.lobbySceneName)
                {
                    ServerSend.LoadScene(LevelManager.activeSceneName, player.id);
                }

                ServerSend.SetGameState(GameState.Starting, player.id);
            }

            if (GameManager.gameState == GameState.InProgress)
            {
                ServerSend.SetGameState(GameState.InProgress, player.id);
            }
        }
    }

    public override async Task Setup(bool asRematch = false)
    {
        GameManager.gameState = GameState.Starting;
        OnSetup?.Invoke();

        if (!asRematch)
        {
            Transition?.Invoke(true);

            GameManager.instance.FadeMusic(true);

            if (NetworkManager.IsServer)
            {
                Player.OnPlayerTypeChanged += OnPlayerTypeChanged;

                await GameManager.instance.LoadSceneAsync(data.sceneName, LoadSceneMode.Additive);
            }
        }

        if (NetworkManager.IsServer)
        {
            foreach (Player player in Player.RegisteredPlayers.Values)
            {
                if (NetworkManager.IsServer)
                {
                    player.SetPlayerReady(false);
                }
            }

            Timer.Create(data.startDelay)
                .OnComplete(Start)
                .Start();

            ServerSend.SetGameState(GameManager.gameState, asRematch);
        }
    }

    public override void Start()
    {
        if (rules.allowPickups.value)
        {
            PickupSpawner.SetAllActive(true);
        }

        GameManager.gameState = GameState.InProgress;
        OnStart?.Invoke();

        GameManager.instance.SetPlayerCameraData(data.playerCameraZoom);

        Transition?.Invoke(false);

        foreach (Player player in Player.RegisteredPlayers.Values)
        {
            player.SetUsernameVisibility(!data.hidePlayerNames, false);
        }

        if (NetworkManager.IsServer)
        {
            ServerSend.SetGameState(GameManager.gameState);
        }
    }

    public override void Finish()
    {
        GameManager.gameState = GameState.Ending;
        OnFinish?.Invoke();

        if (NetworkManager.IsServer)
        {
            endScreenTimer = Timer.Create(data.endDelay)
                .OnComplete(async () =>
                {
                    await End();
                })
                .Start();

            ServerSend.SetGameState(GameManager.gameState);

            PickupSpawner.SetAllActive(false);
        }
    }

    public override async Task End(bool asRematch = false)
    {
        GameManager.gameState = GameState.WaitingToStart;
        OnEnd?.Invoke();

        winnerMessage = string.Empty;

        if (!asRematch)
        {
            GameManager.instance.FadeMusic(false);
            GameManager.instance.SetPlayerCameraData(GameManager.instance.defaultPlayerCameraZoom);

            if (NetworkManager.IsServer)
            {
                foreach (var player in Player.RegisteredPlayers.Values)
                {
                    player.SetPlayerType(PlayerType.Default);

                    string playerSceneName = LevelManager.lobbySceneName;
                    Transform teleportTarget = LevelManager.GetLevelManagerForScene(playerSceneName)
                        .GetNextSpawnpoint(GameManager.gameState != GameState.InProgress && player.isHost);
                    player.TeleportPlayer(teleportTarget, playerSceneName);
                }

                endScreenTimer.Stop();

                Player.OnPlayerTypeChanged -= OnPlayerTypeChanged;

                var sceneName = data.sceneName;

                await GameManager.instance.UnloadSceneAsync(sceneName);
            }
        }

        if (NetworkManager.IsServer)
        {
            NetworkIdentityService.instance.ClearAllNetworkIdentitiesOfType(NetworkIdentityType.Pickup);
            NetworkIdentityService.instance.ClearAllNetworkIdentitiesOfType(NetworkIdentityType.Item);

            PickupHandler.ResetPickupLog();

            ServerSend.SetGameState(GameManager.gameState, asRematch);
        }
    }

    public override async Task ForceEnd()
    {
        GameManager.gameState = GameState.WaitingToStart;
        OnEnd?.Invoke();

        GameManager.instance.FadeMusic(false);

        if (NetworkManager.IsServer)
        {
            Timer.StopAll();

            Player.OnPlayerTypeChanged -= OnPlayerTypeChanged;

            NetworkIdentityService.instance.ClearAllNetworkIdentitiesOfType(NetworkIdentityType.Pickup);
            NetworkIdentityService.instance.ClearAllNetworkIdentitiesOfType(NetworkIdentityType.Item);

            PickupHandler.ResetPickupLog();

            ServerSend.SetGameState(GameManager.gameState, false);
        }

        if (!NetworkManager.IsApplicationQuit)
        {
            await GameManager.instance.UnloadSceneAsync(data.sceneName);
        }
    }

    public override void OnPlayerFallFromMap(Player player)
    {
        string playerSceneName = player.playerScene;
        Transform teleportTarget = LevelManager.GetLevelManagerForScene(playerSceneName).GetNextSpawnpoint(true);
        player.TeleportPlayer(teleportTarget, playerSceneName);
    }
}

[Serializable]
public abstract class Gamemode<R, D, GP> : Gamemode<R, D> where R : Gamerules where D : GamemodeData<R> where GP : GamemodePanel
{
    public GP gameplayPanel;

    protected Timer gameTimer;
    public int timeRemaining;

    public Gamemode(int id, D gamemodeData) : base(id, gamemodeData) {}

    public override async Task OnPlayerJoined(Player player)
    {
        await base.OnPlayerJoined(player);

        if (NetworkManager.IsServer)
        {
            if (GameManager.gameState == GameState.Starting || GameManager.gameState == GameState.InProgress)
            {
                ServerSend.CreateGamemodePanels(player.id);
            }
        }
    }

    public override async Task Replay()
    {
        await base.Replay();

        ShowScoreboard(false, true);
    }

    public override void Start()
    {
        base.Start();

        if (NetworkManager.IsServer)
        {
            CreatePanels();

            CountdownToPlay();
        }
    }

    public override void Finish()
    {
        base.Finish();

        mainMessage = string.Empty;
        subMessage = string.Empty;

        ShowScoreboard(true, true);

        if (NetworkManager.IsServer)
        {
            if (gameTimer != null)
            {
                gameTimer.Stop();
                gameTimer = null;
            }
        }
    }

    public override async Task End(bool asRematch = false)
    {
        await base.End(asRematch);

        ShowScoreboard(false, true);

        if (NetworkManager.IsServer)
        {
            DestroyPanels();
        }

        foreach (var player in Player.RegisteredPlayers.Values)
        {
            player.gamemodeValues.Clear();
        }
    }

    public override async Task ForceEnd()
    {
        await base.ForceEnd();

        ShowScoreboard(false, true);

        DestroyPanels();

        foreach (var player in Player.RegisteredPlayers.Values)
        {
            player.gamemodeValues.Clear();
        }
    }

    public override void CreatePanels()
    {
        if (data.panelPrefab != null) 
        {
            gameplayPanel = (GP)MonoBehaviour.Instantiate(data.panelPrefab, GameManager.instance.gamemodePanelParent);
            gameplayPanel?.Init(this);
        }

        if (NetworkManager.IsServer)
        {
            ServerSend.CreateGamemodePanels();
        }
    }

    public override void UpdatePanels() 
    {
        if (gameplayPanel == null) return;

        gameplayPanel.UpdateValues();
    }

    public override void DestroyPanels()
    {
        if (gameplayPanel != null)
        {
            MonoBehaviour.Destroy(gameplayPanel.gameObject);
        }

        if (NetworkManager.IsServer)
        {
            ServerSend.DestroyGamemodePanels();
        }
    }

    public override void ShowScoreboard(bool value, bool autoSwitchActionMap)
    {
        if (gameplayPanel == null) return;

        gameplayPanel.ShowScoreboard(value, autoSwitchActionMap);
    }

    public override void ToggleScoreboard(bool autoSwitchActionMap)
    {
        if (gameplayPanel == null) return;

        gameplayPanel.ToggleScoreboard(autoSwitchActionMap);
    }

    public override void ReadRuntimeMessageValues(Message message)
    {
        base.ReadRuntimeMessageValues(message);

        UpdatePanels();
    }

    protected void CountdownToPlay()
    {
        if (data.preStartCountdown <= 0) return;

        InputHandler.RotationOnly = true;

        int countdownDuration = data.preStartCountdown;
        if (countdownDuration <= 0)
        {
            OnCountdownToPlayComplete();
            return;
        }

        Timer.Create(countdownDuration)
            .OnTick((countdown) =>
            {
                if (countdown > 0 && countdown < 4)
                {
                    messageDuration = 1f;
                    mainMessage = countdown.ToString();
                }

                ServerSend.UpdateGamemodeRuntimeValues(this);

                gameplayPanel.UpdateValues();
            })
            .OnComplete(() => {

                messageDuration = 2f;
                mainMessage = data.countdownZeroMessage;

                OnCountdownToPlayComplete();
            })
            .Start();
    }

    protected virtual void OnCountdownToPlayComplete()
    {
        InputHandler.RotationOnly = false;

        ServerSend.UpdateGamemodeRuntimeValues(this);

        gameplayPanel.UpdateValues();
    }

    protected void BeginGameTimer(int duration)
    {
        if (gameTimer != null)
        {
            gameTimer.Resume();
        }
        else
        {
            timeRemaining = duration;

            gameTimer = Timer.Create(duration)
                .OnTick(OnGameTimerTick)
                .OnComplete(Finish).Start();
        }
    }

    protected virtual void OnGameTimerTick(int countdown)
    {
        timeRemaining = countdown;

        if (countdown > 0 && countdown < 4)
        {
            messageDuration = 1f;
            mainMessage = countdown.ToString();
        }

        if (countdown == 30 || countdown == 60)
        {
            messageDuration = 2f;
            mainMessage = $"{countdown}s remaining";
        }

        ServerSend.UpdateGamemodeRuntimeValues(this);

        gameplayPanel.UpdateValues();
    }
}