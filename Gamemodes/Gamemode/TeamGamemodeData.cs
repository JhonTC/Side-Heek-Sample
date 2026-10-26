using PlayEveryWare.Common.Extensions;
using Riptide;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public abstract class TeamGamemodeData<R> : GamemodeData<R> where R : Gamerules
{
    public TeamData[] teamData;
}

[System.Serializable]
public class TeamGamerules : Gamerules 
{
    public bool allowCustomTeamCount = false;

    public LimitedRule<float> numberOfTeams = new LimitedRule<float>("Number Of Teams", 2, 2, 10);

    public override void AddMessageValues(ref Message message)
    {
        base.AddMessageValues(ref message);

        message.AddFloat(numberOfTeams.value);
    }

    public override void ReadMessageValues(Message message)
    {
        base.ReadMessageValues(message);

        numberOfTeams.SetValue(message.GetFloat());
    }

    public override void CopyFrom(Gamerules other)
    {
        TeamGamerules copyFrom = other as TeamGamerules;
        if (copyFrom == null) return;

        base.CopyFrom(copyFrom);

        numberOfTeams.SetValue(copyFrom.numberOfTeams.value);
    }

    public override void SetupUI(Transform parent)
    {
        if (allowCustomTeamCount)
        {
            UIUtils.CreateUI(numberOfTeams, parent);
        }

        base.SetupUI(parent); //Spawn base values after so they appear at the bottom of the list
    }

    public override void OnDisplay(bool canControlPanel)
    {
        base.OnDisplay(canControlPanel);

        if (allowCustomTeamCount)
        {
            numberOfTeams.OnDisplay(canControlPanel);
        }
    }
}

public class TeamScoreData : ScoreData
{
    public int defendingTeamId;
    public int scoringTeamId;
    public Player scoringPlayer;
    public Player assistingPlayer;
}

/*[System.Serializable]
public abstract class TeamGamemode<R, D> : TeamGamemode<R, D, GamemodePanel> where R : TeamGamerules where D : GamemodeData<R>
{
    public TeamGamemode(int id, D gamemodeData) : base(id, gamemodeData) { }
}*/

[System.Serializable]
public abstract class TeamGamemode<R, D, P> : Gamemode<R, D, P> where R : TeamGamerules where D : TeamGamemodeData<R> where P : GamemodePanel
{
    public Dictionary<int, Team> teams = new Dictionary<int, Team>();

    public Dictionary<string, int> disconnectedPlayerTeams;

    public TeamGamemode(int id, D gamemodeData) : base(id, gamemodeData) { }

    public override async Task Setup(bool asRematch = false)
    {
        await base.Setup(asRematch);

        int numberOfTeams = (int)rules.numberOfTeams.value;
        int registeredPlayerCount = Player.RegisteredPlayerCount;
        if (registeredPlayerCount < rules.numberOfTeams.value)
        {
            numberOfTeams = registeredPlayerCount;
        }

        for (int i = 0; i < rules.numberOfTeams.value; i++)
        {
            teams.Add(i, new Team(i, data.teamData[i]));
        }

        if (NetworkManager.IsServer)
        {
            List<Team> tempTeams = teams.Values.ToList();
            List<Player> randomPlayerList = Player.RegisteredPlayers.Values.Where((player) => player.playerType != PlayerType.Spectator).ToList();
            randomPlayerList.Shuffle();

            int playerCounter = 0;
            foreach (Player player in randomPlayerList)
            {
                int teamIndex = playerCounter % teams.Count;
                tempTeams[teamIndex].AddMember(player);

                player.teamId = teamIndex;
                player.SetPlayerType(PlayerType.Team);

                playerCounter++;
            }
        }
    }

    public override void Start()
    {
        base.Start();

        if (NetworkManager.IsServer)
        {
            string sceneName = LevelManager.activeSceneName;
            LevelManager levelManager = LevelManager.GetLevelManagerForScene(sceneName);

            foreach (Team team in teams.Values)
            {
                if (team.members.Count > 0)
                {
                    Transform spawnpoint = levelManager.GetSpawnpoint(team.id, false);

                    foreach (var teamPlayer in team.members.Values)
                    {
                        teamPlayer.player.TeleportPlayer(spawnpoint, sceneName);
                    }
                }
            }
        }
    }

    public override async Task End(bool asRematch = false)
    {
        await base.End(asRematch);

        ClearTeams();
    }

    public override async Task ForceEnd()
    {
        await base.ForceEnd();

        ClearTeams();
    }

    public void ClearTeams()
    {
        foreach (Team team in teams.Values)
        {
            foreach (var teamPlayer in team.members.Values)
            {
                teamPlayer.player.teamId = 0;
            }
        }

        teams.Clear();
    }

    public override void OnPlayerLeft(Player player)
    {
        base.OnPlayerLeft(player);

        foreach (Team team in teams.Values)
        {
            if (team.members.ContainsKey(player.id))
            {
                team.RemoveMember(player);
                disconnectedPlayerTeams.Add(player.username, team.id); //todo ADD PLAYER JOINED DETECTION
            }
        }
    }

    public override void AddRuntimeMessageValues(ref Message message)
    {
        message.AddInt(teams.Count);

        foreach (Team team in teams.Values)
        {
            message.AddInt(team.id);
            message.AddInt(team.score);
            message.AddInt(team.members.Count);
                    
            foreach (TeamPlayer player in team.members.Values)
            {
                message.AddUShort(player.player.id);
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.Goals));
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.Assists));
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.Saves));
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.Shots));
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.FirstTouches));
                message.AddFloat(player.playerValues.Get(PlayerGamemodeValueType.Score));
            }
        }

        base.AddRuntimeMessageValues(ref message);
    }

    public override void ReadRuntimeMessageValues(Message message)
    {
        int teamCount = message.GetInt();

        for (int i = 0; i < teamCount; i++)
        {
            int teamId = message.GetInt();
            if (!teams.ContainsKey(teamId))
            {
                teams.Add(teamId, new Team(teamId, data.teamData[teamId]));
            }
            Team team = teams[teamId];

            team.SetScore(message.GetInt());
            int membersCount = message.GetInt();

            for (int j = 0; j < membersCount; j++)
            {
                ushort playerId = message.GetUShort();
                if (!team.members.ContainsKey(playerId))
                {
                    team.members.Add(playerId, new TeamPlayer(playerId));
                }
                TeamPlayer player = team.members[playerId];

                player.playerValues.Set(PlayerGamemodeValueType.Goals, message.GetFloat());
                player.playerValues.Set(PlayerGamemodeValueType.Assists, message.GetFloat());
                player.playerValues.Set(PlayerGamemodeValueType.Saves, message.GetFloat());
                player.playerValues.Set(PlayerGamemodeValueType.Shots, message.GetFloat());
                player.playerValues.Set(PlayerGamemodeValueType.FirstTouches, message.GetFloat());
                player.playerValues.Set(PlayerGamemodeValueType.Score, message.GetFloat());
            }
        }

        base.ReadRuntimeMessageValues(message);
    }

    public override void Score(ScoreData scoreData) 
    {
        base.Score(scoreData);

        TeamScoreData data = scoreData as TeamScoreData;
        if (data == null) return;

        if (!teams.ContainsKey(data.scoringTeamId)) return;

        teams[data.scoringTeamId].IncrementScore(data.scoringPlayer, data.assistingPlayer, 1);
    }

    public override void CalculatePlayerScore(Player player)
    {
        var playerValues = player.gamemodeValues;

        float score = playerValues.Get(PlayerGamemodeValueType.Goals) * 100
            + playerValues.Get(PlayerGamemodeValueType.Assists) * 50
            + playerValues.Get(PlayerGamemodeValueType.Saves) * 75
            + playerValues.Get(PlayerGamemodeValueType.Shots) * 25
            + playerValues.Get(PlayerGamemodeValueType.BallHits) * 1
            + playerValues.Get(PlayerGamemodeValueType.FirstTouches) * 10;

        playerValues.Set(PlayerGamemodeValueType.Score, score);
    }
}

[System.Serializable]
public class Team
{
    public int id;
    public Dictionary<ushort, TeamPlayer> members = new Dictionary<ushort, TeamPlayer>();
    public int score = 0;
    public Vector3 goalPosition;
    public TeamData data;

    public Team(int id, TeamData data)
    {
        this.id = id;
        this.data = data;
    }

    public void AddMember(Player player)
    {
        if (!members.ContainsKey(player.id))
        {
            members.Add(player.id, new TeamPlayer(player));
        }
    }

    public void RemoveMember(Player player)
    {
        if (members.ContainsKey(player.id))
        {
            members.Remove(player.id);
        }
    }

    public void IncrementScore(Player scoringPlayer, Player assistingPlayer, int delta)
    {
        score += delta;

        if (scoringPlayer != null && members.ContainsKey(scoringPlayer.id))
        {
            members[scoringPlayer.id].playerValues.Change(PlayerGamemodeValueType.Goals, 1);
        }

        if (assistingPlayer != null && members.ContainsKey(assistingPlayer.id))
        {
            members[assistingPlayer.id].playerValues.Change(PlayerGamemodeValueType.Assists, 1);
        }
    }

    public void SetScore(int value)
    {
        score = value;
    }
}

public class TeamPlayer
{
    public Player player;

    public Player.PlayerGamemodeValues playerValues => player.gamemodeValues;

    public TeamPlayer(ushort playerId)
    {
        if (Player.TryGetRegisteredPlayer(playerId, out player)) {}
    }

    public TeamPlayer(Player player)
    {
        this.player = player;
    }
}
