using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FootballScoreboard : GamemodeScoreboard<FootballScoreItem>
{
    public void UpdateScores(Team[] teams, TeamData[] teamColours)
    {
        List<ushort> keysToRemove = existingItems.Keys.ToList();
        for (int i = 0; i < teams.Length; i++)
        {
            Team team = teams[i];
            Color teamColour = teamColours[i].colourDark;

            var scoreOrdererdPlayers = team.members.Values.OrderBy(teamPlayer => teamPlayer.playerValues.Get(PlayerGamemodeValueType.Score)).Reverse();

            foreach (TeamPlayer player in scoreOrdererdPlayers)
            {
                ushort playerId = player.player.id;

                if (!existingItems.ContainsKey(playerId))
                {
                    var item = Instantiate(scoreItemPrefab, panelContent.transform);
                    existingItems.Add(playerId, item);
                }
                
                existingItems[playerId].UpdateValues(player, teamColour);
                existingItems[playerId].transform.SetAsLastSibling();

                if (keysToRemove.Contains(playerId))
                {
                    keysToRemove.Remove(playerId);
                }
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
