using Riptide;
using System.Net.Mail;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "FootballData", menuName = "Scriptable Objects/Gamemodes/FootballData")]
public class FootballData : TeamGamemodeData<FootballRules>
{
    public Ball ballPrefab;
    public float celebrationForce = 500f;
    public int celebrationDuration = 5;

    public override Gamemode GetGamemode(int id)
    {
        return new FootballMode(id, this);
    }
}

[System.Serializable]
public struct TeamData
{
    public string teamName;
    public Color colourDark;
    public Color colourLight;
}

[System.Serializable]
public class FootballRules : TeamGamerules
{
    public LimitedRule<float> gameLength = new LimitedRule<float>("Game Length", 180, 60, 600, "s");
    public EnumRule<GameEndReason> gameEndReason = new EnumRule<GameEndReason>("Game End Reason", GameEndReason.Time);
    public LimitedRule<float> maxScore = new LimitedRule<float>("Max Score", 5, 1, 20, " wins");
    public Rule<bool> friendlyFire = new Rule<bool>("Friendly Fire", false);
    public LimitedRule<float> ballSize = new LimitedRule<float>("Ball Diameter", 5, 1, 7);

    public override Gamerules Duplicate()
    {
        var gamerules = new FootballRules();
        gamerules.CopyFrom(this);
        return gamerules;
    }

    public override void AddMessageValues(ref Message message)
    {
        base.AddMessageValues(ref message);

        message.AddFloat(gameLength.value);
        message.AddFloat(maxScore.value);
        message.AddInt(gameEndReason.enumIndex);
        message.AddBool(friendlyFire.value);
        message.AddFloat(ballSize.value);
    }

    public override void ReadMessageValues(Message message)
    {
        base.ReadMessageValues(message);

        gameLength.SetValue(message.GetFloat());
        maxScore.SetValue(message.GetFloat());
        gameEndReason.SetValue((GameEndReason)message.GetInt());
        friendlyFire.SetValue(message.GetBool());
        ballSize.SetValue(message.GetFloat());
    }

    public override void CopyFrom(Gamerules other)
    {
        FootballRules copyFrom = other as FootballRules;
        if (copyFrom == null) return;

        base.CopyFrom(other);

        gameLength.SetValue(copyFrom.gameLength.value);
        maxScore.SetValue(copyFrom.maxScore.value);
        gameEndReason.SetValue(copyFrom.gameEndReason.value);
        friendlyFire.SetValue(copyFrom.friendlyFire.value);
        ballSize.SetValue(copyFrom.ballSize.value);
    }

    public override void SetupUI(Transform parent)
    {
        UIUtils.CreateUI(gameLength, parent);
        UIUtils.CreateUI(maxScore, parent);
        UIUtils.CreateUI<GameEndReason>(gameEndReason, parent);
        UIUtils.CreateUI(friendlyFire, parent);
        UIUtils.CreateUI(ballSize, parent);

        base.SetupUI(parent); //Spawn base values after so they appear at the bottom of the list
    }

    public override void OnDisplay(bool canControlPanel)
    {
        base.OnDisplay(canControlPanel);

        gameLength.OnDisplay(canControlPanel);
        maxScore.OnDisplay(canControlPanel);
        gameEndReason.OnDisplay(canControlPanel);
        friendlyFire.OnDisplay(canControlPanel);
        ballSize.OnDisplay(canControlPanel);
    }
}

public class FootballScoreData : TeamScoreData
{
    public float ballSpeed;
}


public class FootballMode : TeamGamemode<FootballRules, FootballData, FootballPanel>
{
    private Ball activeBall;
    private bool isRegisteringGoal = false;
    private bool isCelebratingGoal = false;
    private Vector3 registeringGoalPosition;

    public FootballMode(int id, FootballData gamemodeData) : base(id, gamemodeData) { }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (isCelebratingGoal && NetworkManager.IsServer)
        {
            foreach (Team team in teams.Values)
            {
                foreach (TeamPlayer teamPlayer in team.members.Values)
                {
                    var playerRoot = teamPlayer.player.playerMotor.GetMovementController().rootBody;

                    var direction = (registeringGoalPosition - playerRoot.position).normalized;

                    var force = direction * data.celebrationForce;

                    playerRoot.AddForce(force);
                }
            }
        }
    }

    public override async Task Setup(bool asRematch = false)
    {
        await base.Setup(asRematch);

        if (NetworkManager.IsServer)
        {
            foreach (Team team in teams.Values)
            {
                int teamIndex = team.id % data.teamData.Length;
                Color teamColour = data.teamData[teamIndex].colourDark;

                foreach (TeamPlayer teamPlayer in team.members.Values)
                {
                    CustomisationManager.RequestColourForPlayer(teamPlayer.player.id, teamColour, PlayerType.Team, true);
                }
            }

            var spawnpoint = LevelManager.GetLevelManagerForScene(LevelManager.activeSceneName).GetNextSpawnpoint(true); //null as level not loaded yet
            var spawnPosition = spawnpoint.position + Vector3.up * 5f;

            activeBall = NetworkIdentityService.instance.SpawnAndRegisterNetworkIdentity(data.ballPrefab, spawnpoint.position, spawnpoint.rotation, null, onSpawnedCallback: (ball) =>
            {
                ball.SetBallSize(rules.ballSize.value);
            });
        }
    }

    public override void Start()
    {
        base.Start();

        if (NetworkManager.IsServer)
        {
            timeRemaining = (int)rules.gameLength.value;
            gameplayPanel.UpdateValues();
            ServerSend.UpdateGamemodeRuntimeValues(this);
        }
    }

    public override void Finish()
    {
        base.Finish();

        CalculateWinners();

        //UIManager.instance.SetSpecialMessage($"Game Over!");

        if (NetworkManager.IsServer)
        {
            if (activeBall != null)
            {
                Object.Destroy(activeBall.gameObject);
            }
        }
    }

    public override async Task ForceEnd()
    {
        await base.ForceEnd();

        if (NetworkManager.IsServer)
        {
            if (activeBall != null)
            {
                Object.Destroy(activeBall.gameObject);
            }
        }
    }

    public override void AddToEndMessage(ref Message message)
    {
        //message.AddBool(isHunterVictory);
    }
    public override void ReadFromEndMessage(Message message)
    {
        //bool isHunterVictory = message.GetBool();
    }

    public override bool CheckForGameOver()
    {
        if (GameManager.gameState == GameState.InProgress)
        {
            if (rules.gameEndReason.value == GameEndReason.Score)
            {
                foreach (Team team in teams.Values)
                {
                    if (team.score >= rules.maxScore.value)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void CalculateWinners()
    {
        int highestScore = -1;
        int highestKey = -1;
        bool isDraw = false;
        foreach (Team team in teams.Values)
        {
            if (team.score > highestScore)
            {
                highestKey = team.id;
                highestScore = team.score;

                isDraw = false;
            }
            else if (team.score == highestScore)
            {
                isDraw = true;
            }
        }

        string message = $"Team {highestKey} Wins!";
        if (isDraw)
        {
            message = "Draw!";
        }

        winnerMessage = message;

        ServerSend.UpdateGamemodeRuntimeValues(this);
        gameplayPanel.UpdateValues();
    }

    public override void OnPlayerCollision(Player player, Player other)
    {
        if (!other.isBodyActive) return;
        if (!player.playerMotor.CanKnockOutOther(other)) return;

        if (player.teamId == other.teamId && !rules.friendlyFire.value) return;

        other.playerMotor.OnCollisionWithOther(data.knockoutDuration);
    }

    public override void Score(ScoreData scoreData)
    {
        FootballScoreData data = scoreData as FootballScoreData;
        if (data == null) return;

        if (!teams.ContainsKey(data.defendingTeamId)) return;
        if (isRegisteringGoal) return;

        registeringGoalPosition = teams[data.defendingTeamId].goalPosition;
        isRegisteringGoal = true;
        isCelebratingGoal = true;

        gameTimer.Pause();

        activeBall.gameObject.SetActive(false);

        base.Score(scoreData);

        messageDuration = 4f;
        mainMessage = data.scoringPlayer != null ? $"{data.scoringPlayer.username} scored!" : $"Team {data.scoringTeamId} scored!";

        string ballSpeedString = data.ballSpeed.ToString("0.00");
        subMessage = $"{ballSpeedString} m/s";

        ServerSend.UpdateGamemodeRuntimeValues(this);

        gameplayPanel.UpdateValues();

        BeginGoalCelebration();
    }

    public override void AddRuntimeMessageValues(ref Message message)
    {
        message.AddInt(timeRemaining);

        base.AddRuntimeMessageValues(ref message);
    }

    public override void ReadRuntimeMessageValues(Message message)
    {
        timeRemaining = message.GetInt();

        base.ReadRuntimeMessageValues(message);
    }

    private void BeginGoalCelebration()
    {
        Timer.Create(data.celebrationDuration)
            .OnComplete(() =>
            {
                ResetStates();

                if (CheckForGameOver())
                {
                    Finish();
                    return;
                }

                ResetPositions();

                if (GameManager.gameState == GameState.InProgress)
                {
                    CountdownToPlay();
                }
            })
            .Start();
    }

    private void ResetPositions()
    {
        LevelManager levelManager = LevelManager.GetLevelManagerForScene(LevelManager.activeSceneName);
        foreach (Team team in teams.Values)
        {
            Transform playerSpawnpoint = levelManager.GetSpawnpoint(team.id, false);

            foreach (TeamPlayer teamPlayer in team.members.Values)
            {
                teamPlayer.player.TeleportPlayer(playerSpawnpoint, teamPlayer.player.playerScene);
            }
        }

        if (GameManager.gameState == GameState.InProgress)
        {
            var ballSpawnpoint = LevelManager.GetLevelManagerForScene(LevelManager.activeSceneName).GetNextSpawnpoint(true);
            var spawnPosition = ballSpawnpoint.position + Vector3.up * 5f;

            activeBall.gameObject.SetActive(true);
            activeBall.root.Teleport(spawnPosition, ballSpawnpoint.rotation);
            activeBall.firstTouchActive = true;
        }
    }

    private void ResetStates()
    {
        isCelebratingGoal = false;
    }

    protected override void OnCountdownToPlayComplete()
    {
        base.OnCountdownToPlayComplete();

        isRegisteringGoal = false;

        BeginGameTimer((int)rules.gameLength.value);
    }
}