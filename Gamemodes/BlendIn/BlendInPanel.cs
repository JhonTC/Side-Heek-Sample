using TMPro;
using UnityEngine;

public class BlendInPanel : GamemodePanel<BlendInScoreboard>
{
    public TMP_Text timeText;

    private BlendInMode gamemode;

    public override void Init(Gamemode gamemode)
    {
        base.Init(gamemode);

        this.gamemode = (BlendInMode)gamemode;
    }

    public override void UpdateValues()
    {
        base.UpdateValues();

        if (gamemode == null) return;

        UpdateTime(gamemode.timeRemaining);

        scoreboard.UpdateScores();
        scoreboard.SetWinnerText(gamemode.winnerMessage);
    }

    private void UpdateTime(int time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = time - (minutes * 60);
        string secondsPrefix = seconds < 10 ? "0" : "";

        timeText.text = $"{minutes}:{secondsPrefix}{seconds}";
    }
}
