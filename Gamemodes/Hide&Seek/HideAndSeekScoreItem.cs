using TMPro;
using UnityEngine;

public class HideAndSeekScoreItem : GamemodeScoreItem
{
    // Hunter values
    [SerializeField] private TMP_Text catchesText;
    [SerializeField] private TMP_Text remainingText;

    // Hider values
    [SerializeField] private TMP_Text proximityText;
    [SerializeField] private TMP_Text visibleText;

    public void UpdateValues(Player player, PlayerType playerType)
    {
        UpdateValues(player, player.playerColours.GetColour(playerType));

        if (playerType == PlayerType.Hunter)
        {
            catchesText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Catches).ToString();
            remainingText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Countdown).ToString() + "s";
        }

        if (playerType == PlayerType.Hider)
        {
            proximityText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Proximity).ToString() + "s";
            visibleText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Seen).ToString() + "s";
        }

        DisplayValuesForType(playerType);
    }

    private void DisplayValuesForType(PlayerType playerType)
    {
        bool isHunter = playerType == PlayerType.Hunter;
        bool isHider = playerType == PlayerType.Hider;

        catchesText.gameObject.SetActive(isHunter);
        remainingText.gameObject.SetActive(isHunter);

        proximityText.gameObject.SetActive(isHider);
        visibleText.gameObject.SetActive(isHider);
    }
}
