using TMPro;
using UnityEngine;

public class BlendInScoreItem : GamemodeScoreItem
{
    // Hunter values
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text catchesText;
    [SerializeField] private TMP_Text remainingText;

    // Hider values
    [SerializeField] private TMP_Text proximityText;
    [SerializeField] private TMP_Text flopsText;
    [SerializeField] private TMP_Text jumpsText;

    public void UpdateValues(Player player, PlayerType playerType)
    {
        UpdateValues(player, player.playerColours.GetColour(playerType));

        if (playerType == PlayerType.Hunter)
        {
            livesText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Lives).ToString();
            catchesText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Catches).ToString();
            remainingText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Countdown).ToString() + "s";
        }

        if (playerType == PlayerType.Hider)
        {
            proximityText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Proximity).ToString() + "s";
            flopsText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Flops).ToString();
            jumpsText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Jumps).ToString();
        }

        DisplayValuesForType(playerType);
    }

    private void DisplayValuesForType(PlayerType playerType)
    {
        bool isHunter = playerType == PlayerType.Hunter;
        bool isHider = playerType == PlayerType.Hider;

        livesText.gameObject.SetActive(isHunter);
        catchesText.gameObject.SetActive(isHunter);
        remainingText.gameObject.SetActive(isHunter);

        proximityText.gameObject.SetActive(isHider);
        flopsText.gameObject.SetActive(isHider);
        jumpsText.gameObject.SetActive(isHider);
    }
}
