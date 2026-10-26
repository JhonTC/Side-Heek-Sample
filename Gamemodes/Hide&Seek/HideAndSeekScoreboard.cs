using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HideAndSeekScoreboard : GamemodeScoreboard<HideAndSeekScoreItem>
{
    [SerializeField] private Transform hunterScoreItemParent;
    [SerializeField] private Transform hiderScoreItemParent;

    public void UpdateScores()
    {
        List<ushort> keysToRemove = existingItems.Keys.ToList();

        var hunters = Player.RegisteredPlayers.Values
            .Where(player => player.playerType == PlayerType.Hunter || (player.playerType != PlayerType.Hider && player.lastPlayerType == PlayerType.Hunter))
            .OrderBy(player => player.gamemodeValues.Get(PlayerGamemodeValueType.Score)).Reverse();

        var hiders = Player.RegisteredPlayers.Values
            .Where(player => player.playerType == PlayerType.Hider || (player.playerType != PlayerType.Hunter && player.lastPlayerType == PlayerType.Hider))
            .OrderBy(player => player.gamemodeValues.Get(PlayerGamemodeValueType.Score)).Reverse();

        foreach (Player player in hunters)
        {
            ushort playerId = player.id;

            if (!existingItems.ContainsKey(playerId))
            {
                var newItem = Instantiate(scoreItemPrefab, hunterScoreItemParent);
                existingItems.Add(playerId, newItem);
            }

            PlayerType playerType = player.playerType;
            if (playerType == PlayerType.Spectator)
            {
                playerType = player.lastPlayerType;
            }

            var item = existingItems[playerId];
            if (item.transform.parent != hunterScoreItemParent)
            {
                item.transform.SetParent(hunterScoreItemParent);
            }
            item.transform.SetAsLastSibling();
            item.UpdateValues(player, playerType);

            if (keysToRemove.Contains(playerId))
            {
                keysToRemove.Remove(playerId);
            }
        }

        foreach (Player player in hiders)
        {
            ushort playerId = player.id;

            if (!existingItems.ContainsKey(playerId))
            {
                var newItem = Instantiate(scoreItemPrefab, hiderScoreItemParent);
                existingItems.Add(playerId, newItem);
            }

            PlayerType playerType = player.playerType;
            if (playerType == PlayerType.Spectator)
            {
                playerType = player.lastPlayerType;
            }

            var item = existingItems[playerId];
            if (item.transform.parent != hiderScoreItemParent)
            {
                item.transform.SetParent(hiderScoreItemParent);
            }
            item.transform.SetAsLastSibling();
            item.UpdateValues(player, playerType);

            if (keysToRemove.Contains(playerId))
            {
                keysToRemove.Remove(playerId);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (existingItems.ContainsKey(key))
            {
                Destroy(existingItems[key].gameObject);

                existingItems.Remove(key);
            }
        }
    }
}
