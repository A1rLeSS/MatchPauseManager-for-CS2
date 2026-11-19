using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace MatchPauseManager;

/// <summary>
/// Round snapshot / restore for the technical pause rewind.
///
/// CS2 does not expose CS:GO's mp_backup_restore_load_file workflow, so the state
/// is captured by the plugin and written back through the schema members.
/// </summary>
public static class RoundSnapshotter
{
    public static RoundSnapshot Capture(CCSGameRules rules)
    {
        var snapshot = new RoundSnapshot
        {
            RoundsPlayed = rules.TotalRoundsPlayed,
            ScoreT = GetTeamScore(CsTeam.Terrorist),
            ScoreCt = GetTeamScore(CsTeam.CounterTerrorist),
            IsValid = true,
        };

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            {
                continue;
            }

            var pawn = player.PlayerPawn?.Value;
            if (pawn == null)
            {
                continue;
            }

            var snap = new PlayerSnapshot
            {
                SteamId = player.SteamID,
                Slot = player.Slot,
                Name = player.PlayerName ?? string.Empty,
                Team = (int)player.TeamNum,
                Money = player.InGameMoneyServices?.Account ?? 0,
                Health = (int)player.PawnHealth,
                Armor = player.PawnArmor,
                Helmet = player.PawnHasHelmet,
                Defuser = player.PawnHasDefuser,
                Mvps = player.MVPs,
                Score = player.Score,
            };

            var weapons = pawn.WeaponServices?.MyWeapons;
            if (weapons != null)
            {
                foreach (var handle in weapons)
                {
                    var weapon = handle?.Value;
                    if (weapon == null || !weapon.IsValid)
                    {
                        continue;
                    }

                    var name = weapon.DesignerName;
                    if (!string.IsNullOrEmpty(name))
                    {
                        snap.Weapons.Add(name);
                    }
                }
            }

            snapshot.Players[snap.Slot] = snap;
        }

        return snapshot;
    }

    /// <summary>Restores the snapshot. Returns false when the score could not be restored.</summary>
    public static bool Restore(RoundSnapshot snapshot, bool restorePlayerStats, ILogger logger, out string report)
    {
        var sb = new StringBuilder();
        var rules = Utilities.GetGameRules();
        var ok = true;

        // Scores.
        ok &= SetTeamScore(CsTeam.Terrorist, snapshot.ScoreT);
        ok &= SetTeamScore(CsTeam.CounterTerrorist, snapshot.ScoreCt);
        sb.Append($"score={snapshot.ScoreT}:{snapshot.ScoreCt}");

        // Round counter (the invalidated round must not consume a slot).
        if (rules != null && snapshot.RoundsPlayed >= 0 && rules.TotalRoundsPlayed > snapshot.RoundsPlayed)
        {
            rules.TotalRoundsPlayed = snapshot.RoundsPlayed;
            rules.ITotalRoundsPlayed = snapshot.RoundsPlayed;
            sb.Append($" rounds={snapshot.RoundsPlayed}");
        }

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            {
                continue;
            }

            /*
             * Keyed by slot: clients sharing a SteamID (emulated servers)
             * must not overwrite each other's snapshot. The SteamID is still
             * verified so a reconnect into the same slot is not restored.
             */

            if (!snapshot.Players.TryGetValue(player.Slot, out var snap))
            {
                sb.Append($" [missing {player.PlayerName}]");
                continue;
            }

            if (snap.SteamId != 0 && player.SteamID != 0 && snap.SteamId != player.SteamID)
            {
                sb.Append($" [slot {player.Slot} taken by another client]");
                continue;
            }

            if (player.InGameMoneyServices != null)
            {
                player.InGameMoneyServices.Account = snap.Money;
            }

            player.MVPs = snap.Mvps;
            player.Score = snap.Score;

            /*
             * Equipment: at a freeze time the players normally hold a pistol
             * and a knife, but the snapshot is restored anyway so the round
             * really starts from the same loadout.
             *
             * The bomb is never removed or re-given: it belongs to the game
             * and dropping it would leave the T side without a C4.
             */

            RemoveWeaponsExceptBomb(player);

            foreach (var weapon in snap.Weapons)
            {
                if (weapon == "weapon_c4")
                {
                    continue;
                }

                player.GiveNamedItem(weapon);
            }

            var pawn = player.PlayerPawn?.Value;
            if (pawn != null)
            {
                if (pawn.LifeState == 0)
                {
                    pawn.Health = snap.Health;
                    pawn.ArmorValue = snap.Armor;

                    var items = pawn.ItemServices?.As<CCSPlayer_ItemServices>();
                    if (items != null)
                    {
                        items.HasHelmet = snap.Helmet;
                        items.HasDefuser = snap.Defuser;
                    }
                }
            }

            if (restorePlayerStats)
            {
                TryClearLastRoundStats(player);
            }
        }

        report = sb.ToString();
        logger.LogInformation("[MPM] rewind restore: {Report}", report);
        return ok;
    }

    /// <summary>
    /// Removes every weapon except the C4, which is owned by the game.
    /// </summary>
    private static void RemoveWeaponsExceptBomb(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        var weapons = pawn?.WeaponServices?.MyWeapons;
        if (weapons == null)
        {
            return;
        }

        var names = new List<string>();
        foreach (var handle in weapons)
        {
            var weapon = handle?.Value;
            if (weapon == null || !weapon.IsValid)
            {
                continue;
            }

            var name = weapon.DesignerName;
            if (!string.IsNullOrEmpty(name) && name != "weapon_c4")
            {
                names.Add(name!);
            }
        }

        foreach (var name in names)
        {
            Utilities.RemoveItemByDesignerName(player, name);
        }
    }

    /// <summary>
    /// Best effort: CS2 keeps K/D/A totals on the client side, but the per-round
    /// stats vector is writable, so the invalidated round is zeroed out there.
    /// </summary>
    private static void TryClearLastRoundStats(CCSPlayerController player)
    {
        try
        {
            var tracking = player.ActionTrackingServices;
            var perRound = tracking?.PerRoundStats;
            if (perRound == null || perRound.Count == 0)
            {
                return;
            }

            var last = perRound[perRound.Count - 1];
            if (last == null)
            {
                return;
            }

            last.Kills = 0;
            last.Deaths = 0;
            last.Assists = 0;
        }
        catch
        {
            // not fatal: score / economy / round counter are the important parts
        }
    }

    public static int GetTeamScore(CsTeam side)
    {
        var team = FindTeam(side);
        return team?.Score ?? 0;
    }

    private static bool SetTeamScore(CsTeam side, int value)
    {
        var team = FindTeam(side);
        if (team == null)
        {
            return false;
        }

        team.Score = value;
        return true;
    }

    public static CCSTeam? FindTeam(CsTeam side)
    {
        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
        {
            if (team != null && team.IsValid && team.TeamNum == (byte)side)
            {
                return team;
            }
        }

        return null;
    }
}
