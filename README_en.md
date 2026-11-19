**Read this in other languages:**
[English](README_en.md) | [中文](README.md)

# MatchPauseManager-for-CS2

Competitive **tactical / technical pause** manager for CS2, ported from
[MatchPauseManager-for-CSGO](https://github.com/A1rLeSS/MatchPauseManager-for-CSGO) (SourceMod / SourcePawn, V1.2.6).

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
- **Damage gate**: if the round already produced damage it is played out first, then paused in the
  next freeze time and marked invalid for a restore; without damage the round is kept
- Referee and team pauses use different wording; a referee pause can only be resumed by the referee
- A pending request can be cancelled with `!unpause` / `mp_unpause_match`
- The pause is verified every second after it is issued

### Round rewind
- Snapshot at every round start: score, round counter, per-player money, health, armour, helmet,
  defuser, weapons, MVP/score
- After an invalidated round is paused, the referee runs `mp_rewind_match` (console / rcon) or an
  admin runs `!rewind`
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

See the table in the Chinese README (`tacticalTime`, `tacticalCount`, `overtimeCount`,
`technicalDamageGate`, `rewindEnabled`, `rewindPlayerStats`, `textStyle`, `warnChatDuringTechnical`).

## Differences from the CS:GO version

1. **Pause timing**: CS2's `mp_pause_match` only takes effect at the next freeze time, so a round in
   progress cannot be frozen in place. The technical pause therefore finishes the round first.
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
