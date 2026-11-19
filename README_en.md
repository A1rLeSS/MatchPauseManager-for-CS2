**Languages:**
[English](README_en.md) | [中文](README.md)

# MatchPauseManager-for-CS2

MatchPauseManager-for-CS2 manages tactical and technical pauses on CS2 competitive servers. It handles pause limits, technical-pause round handling, and referee-controlled round rewinds.

The plugin is built for CounterStrikeSharp and Metamod:Source. Since SourceMod does not support CS2, this is a rewrite of the CS:GO plugin, not a direct port.

> **Note**: SourceMod does not support CS2. This is a **rewrite** on top of
> **Metamod:Source 2.0 + CounterStrikeSharp (C# / .NET 10)**, not a recompilation of the `.sp` file.

## Features

### Tactical pause
- `!pause` / `!tac` / `!p`
- 3 timeouts per team, 30 seconds each, warning 5 seconds before the end
- One timeout per team per round; refreshed to 1 per team on every overtime segment
- Countdown on the centre HUD, automatic resume
- Only the calling team or the referee can end it early

### Technical pause
- `!tech` from a player = team pause; `mp_pause_match` from the server console / rcon = **referee**
- **Damage gate**: when the round has **no damage yet**, `CCSGameRules.TerminateRound()` voids it immediately, the freeze time that follows is paused and the **pre-round state is restored automatically** (no referee confirmation). When damage was **already done**, the round is played out first, paused in the next freeze time and the **referee decides** whether to restore it
- Tactical and technical pauses are refused during warmup, and a technical pause that is unpaused from outside the plugin releases its state instead of staying stuck
- Referee and team pauses use different wording; a referee pause can only be resumed by the referee
- A pending request can be cancelled with `!unpause` / `mp_unpause_match`
- The pause is verified every second after it is issued

### Round rewind
- Snapshot at every round start: score, round counter, per-player money, health, armour, helmet,
  defuser, weapons, MVP/score
- Rounds voided without damage are **restored automatically**; rounds that already had damage are
  restored when the **referee confirms** (`mp_rewind_match` from console / rcon, or `!rewind` in game)
- Restores score, round counter, money, armour/health/equipment and makes a best effort to clear the
  invalidated round from the per-round player statistics
- Success and failure are both reported in chat and logged

### Text packs
`textStyle` selects one of three packs modelled on real broadcast wording (chat and HUD):

| Value | Style |
| --- | --- |
| `0` | International (English, default) |
| `1` | Chinese tournament |
| `2` | International + rule reminders |

## Install

1. Install **Metamod:Source 2.0** and **CounterStrikeSharp** (built against `1.0.380` / .NET 10).
2. Copy the `MatchPauseManager` folder to
   `game/csgo/addons/counterstrikesharp/plugins/MatchPauseManager/`.
3. Restart the server or run `css_plugins reload`.
4. `config.json` is created on first load.

## Commands

| Command | Who | Purpose |
| --- | --- | --- |
| `!pause` / `!tac` / `!p` | players | tactical timeout |
| `!tech` | players | technical pause |
| `!unpause` | calling team / referee | end or cancel a pause |
| `!rewind` | referee (`@css/root`) | restore the invalidated round |
| `mp_pause_match` | server console / rcon | referee technical pause |
| `mp_unpause_match` | server console / rcon | referee resume |
| `mp_rewind_match` | server console / rcon | referee restore |
| `css_pause` / `css_tech` / `css_unpause` / `css_rewind` | console or admins | console forms |

## Configuration

The plugin creates `config.json` in its plugin directory on first load. Edit the file and reload the plugin or restart the server for changes to take effect.

| Setting | Default | Description |
| --- | ---: | --- |
| `tacticalTime` | `30` | Tactical pause duration in seconds (1–300). |
| `tacticalCount` | `3` | Tactical pauses available to each team during regulation. |
| `overtimeCount` | `1` | Tactical pauses available to each team per overtime segment. |
| `technicalDamageGate` | `true` | If `true`, round handling depends on whether damage has occurred (no damage: void and restore automatically; damage: play it out, referee confirms the restore). If `false`, legacy behaviour: always void and restart the round without any restore. |
| `rewindEnabled` | `true` | Allows rewinding an invalidated round. When set to `false`, the "no damage" case no longer voids the round: it is paused in place and kept. |
| `rewindPlayerStats` | `true` | Attempts to clear per-round player stats when rewinding. |
| `textStyle` | `0` | Message pack: `0` international English, `1` Chinese, `2` international English with rule reminders. |
| `warnChatDuringTechnical` | `true` | Warns players who chat during a technical pause. |

## Differences from the CS:GO version

1. **Pause timing**: CS2's `mp_pause_match` only takes effect at the next freeze time, so a round in
   progress cannot be frozen in place. The plugin therefore voids the round with
   `CCSGameRules.TerminateRound()` to create that freeze time, which makes "no damage -> void +
   restore + pause" behave like the CS:GO version. Rounds that already had damage still have to be
   played out.
2. **Rewind**: CS2 has no `mp_backup_restore_load_file`, so the plugin snapshots the state and writes
   it back through the schema members.
3. **Player K/D/A**: CS2 keeps the totals client side; only the per-round stats vector is writable, so
   the invalidated round is cleared there on a best effort basis.
4. Side mapping and team names follow the V1.2.4 approach (`m_totalRoundsPlayed` derived).

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/net10.0/MatchPauseManager.dll`.

## License

[PolyForm Noncommercial License 1.0.0](LICENSE.md)
