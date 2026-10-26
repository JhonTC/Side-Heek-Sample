using UnityEngine;
using UnityEngine.UI;

public class BasicGamemodePanel : GamemodePanel
{
    public Button exitButton;

    public override void Init(Gamemode gamemode)
    {
        base.Init(gamemode);

        exitButton.interactable = NetworkManager.IsHost;

        exitButton.onClick.AddListener(() =>
        {
            GameManager.instance.gamemode.Finish();
        });
    }
}
