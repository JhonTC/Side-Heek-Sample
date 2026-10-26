using TMPro;
using UnityEngine;

public abstract class GamemodePanel : MonoBehaviour
{
    public FadingText mainText;
    public FadingText subText;

    private Gamemode gamemode;

    public virtual void Init(Gamemode gamemode)
    {
        this.gamemode = gamemode;
    }

    public virtual void UpdateValues()
    {
        if (gamemode == null) return;

        if (gamemode.mainMessage == mainText.GetText()) return;
        if (gamemode.mainMessage == string.Empty) return;
        if (gamemode.messageDuration <= 0f) return;

        mainText.SetText(gamemode.mainMessage, gamemode.messageDuration);

        if (gamemode.subMessage == subText.GetText()) return;
        if (gamemode.subMessage == string.Empty) return;

        subText.SetText(gamemode.subMessage, gamemode.messageDuration);
    }

    public virtual void ShowScoreboard(bool value, bool autoSwitchActionMap) { }
    public virtual void ToggleScoreboard(bool autoSwitchActionMap) { }

    public void OnRematch()
    {
        Player.LocalPlayer?.RequestPlayerReady(!Player.LocalPlayer.isReady);
    }

    public void OnDisconnect()
    {
        UIManager.instance.OnDisconnectButtonPressed();
    }

    public void OnQuit()
    {
        UIManager.instance.DisplayPanel(UIPanelType.Quit);
    }
}

public abstract class GamemodePanel<SB> : GamemodePanel where SB : GamemodeScoreboard
{
    [SerializeField] protected SB scoreboard;

    public override void ShowScoreboard(bool value, bool autoSwitchActionMap) 
    {
        scoreboard.SetActive(value);

        if (autoSwitchActionMap)
        {
            InputHandler.instance.SwitchActionMap(value ? ActionMapType.UI : ActionMapType.Player);
        }
    }

    public override void ToggleScoreboard(bool autoSwitchActionMap) 
    { 
        ShowScoreboard(!scoreboard.IsActive, autoSwitchActionMap); 
    }
}
