using System.Linq;
using TMPro;
using UnityEngine;

public class FootballPanel : GamemodePanel<FootballScoreboard>
{
    public TMP_Text timeText;
    public TeamPanel[] teamPanels;

    private FootballMode gamemode;

    public override void Init(Gamemode gamemode)
    {
        base.Init(gamemode);

        this.gamemode = (FootballMode)gamemode;
    }

    public override void UpdateValues()
    {
        base.UpdateValues();

        if (gamemode == null) return;

        UpdateTime(gamemode.timeRemaining);

        foreach (var team in gamemode.teams.Values)
        {
            UpdateTeamScore(team.id, team.score);
        }

        scoreboard.UpdateScores(gamemode.teams.Values.ToArray(), gamemode.data.teamData);
        scoreboard.SetWinnerText(gamemode.winnerMessage);
    }

    private void UpdateTime(int time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = time - (minutes * 60);
        string secondsPrefix = seconds < 10 ? "0" : "";

        timeText.text = $"{minutes}:{secondsPrefix}{seconds}";
    }

    private void UpdateTeamScore(int team, int score)
    {
        if (team < 0 || team >= teamPanels.Length) return;

        teamPanels[team].scoreText.text = score.ToString();
    }

    public override void ShowScoreboard(bool value, bool autoSwitchActionMap)
    {
        if (value)
        {
            scoreboard.UpdateScores(gamemode.teams.Values.ToArray(), gamemode.data.teamData);
        }

        base.ShowScoreboard(value, autoSwitchActionMap);
    }

    [System.Serializable]
    public class TeamPanel
    {
        public TMP_Text nameText;
        public TMP_Text scoreText;

        public void UpdateScore(int score)
        {
            scoreText.text = score.ToString();
        }
    }
}
