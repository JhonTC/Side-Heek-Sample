using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GamemodeScoreboard : UIPanel
{
    [SerializeField] private GameObject endScreenButtons;
    [SerializeField] private TMP_Text rematchButtonText;

    [SerializeField] private GameObject endScreenWinnerParent;
    [SerializeField] private TMP_Text endScreenWinnerText;

    protected override void Start()
    {
        base.Start();

        endScreenWinnerParent.SetActive(false);

        DisablePanel();
    }

    public override void Subscribe()
    {
        base.Subscribe();

        Player.OnPlayerReadyChanged += OnPlayerReadyChanged;
    }

    public override void Unsubscribe()
    {
        base.Unsubscribe();

        Player.OnPlayerReadyChanged -= OnPlayerReadyChanged;
    }

    protected virtual void OnPlayerReadyChanged(Player player)
    {
        if (player.isLocal)
        {
            rematchButtonText.text = player.isReady ? "Cancel Vote" : "Vote Rematch";
        }
    }

    public override void EnablePanel()
    {
        if (endScreenButtons == null) return;

        endScreenButtons.SetActive(GameManager.gameState == GameState.Ending);

        base.EnablePanel();
    }

    public override void DisablePanel()
    {
        base.DisablePanel();
    }

    public void SetWinnerText(string text)
    {
        if (text != string.Empty)
        {
            endScreenWinnerText.text = text;
        }

        endScreenWinnerParent.SetActive(text != null);
    }

    public virtual void UpdateValues()
    {

    }
}

public class GamemodeScoreboard<T> : GamemodeScoreboard where T: GamemodeScoreItem
{
    [SerializeField] protected T scoreItemPrefab;

    protected Dictionary<ushort, T> existingItems = new Dictionary<ushort, T>();

    protected override void OnPlayerReadyChanged(Player player)
    {
        base.OnPlayerReadyChanged(player);

        if (!existingItems.ContainsKey(player.id)) return;

        existingItems[player.id].OnPlayerReady(player.isReady);
    }
}
