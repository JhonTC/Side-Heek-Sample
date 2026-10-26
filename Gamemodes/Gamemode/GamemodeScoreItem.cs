using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamemodeScoreItem : MonoBehaviour
{
    [SerializeField] protected Image outline;
    protected Color outlineDefaultColour;

    [SerializeField] protected Image background;
    [SerializeField] protected TMP_Text nameText;

    [SerializeField] protected TMP_Text scoreText;

    protected void Awake()
    {
        outlineDefaultColour = outline.color;
    }

    protected void UpdateValues(Player player, Color backgroundColour)
    {
        background.color = LightenColour(backgroundColour);

        string username = player.username;
        if (player.isLocal)
        {
            username += " (you)";
        }

        nameText.text = username;
        scoreText.text = player.gamemodeValues.GetInt(PlayerGamemodeValueType.Score).ToString();
    }

    private Color LightenColour(Color colour)
    {
        Color.RGBToHSV(colour, out float h, out float s, out float v);

        return Color.HSVToRGB(h, Mathf.Clamp01(s * 0.6f), Mathf.Clamp01(v * 1.4f));
    }

    public void OnPlayerReady(bool isReady)
    {
        outline.color = isReady ? GameManager.instance.readyColour : outlineDefaultColour;
    }
}
