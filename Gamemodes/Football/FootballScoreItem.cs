using TMPro;
using UnityEngine;

public class FootballScoreItem : GamemodeScoreItem
{
    [SerializeField] private TMP_Text goalsText;
    [SerializeField] private TMP_Text assistsText;
    [SerializeField] private TMP_Text savesText;
    [SerializeField] private TMP_Text shotsText;

    public void UpdateValues(TeamPlayer teamPlayer, Color teamColour)
    {
        UpdateValues(teamPlayer.player, teamColour);

        scoreText.text = teamPlayer.playerValues.GetInt(PlayerGamemodeValueType.Score).ToString();
        goalsText.text = teamPlayer.playerValues.GetInt(PlayerGamemodeValueType.Goals).ToString();
        assistsText.text = teamPlayer.playerValues.GetInt(PlayerGamemodeValueType.Assists).ToString();
        savesText.text = teamPlayer.playerValues.GetInt(PlayerGamemodeValueType.Saves).ToString();
        shotsText.text = teamPlayer.playerValues.GetInt(PlayerGamemodeValueType.Shots).ToString();
    }
}
