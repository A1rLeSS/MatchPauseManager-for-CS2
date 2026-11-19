using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace MatchPauseManager;

/// <summary>
/// CS2 port of MatchPauseManager.
///
/// CS2 semantics that shape this port:
///  - <c>mp_pause_match</c> pauses at the next freeze time, never mid round, so a
///    pending pause is a first class state here.
///  - the round counter and team scores are exposed as writable schema members, so
///    the rewind restores them directly instead of using CS:GO round backups.
/// </summary>
public sealed class MatchPauseManagerPlugin : BasePlugin
{
    public override string ModuleName => "Match Pause Manager";

    public override string ModuleVersion => "1.3.2";

    public override string ModuleAuthor => "A1rLeSS";

    public override string ModuleDescription =>
        "Competitive tactical and technical pause manager for CS2 (ported from MatchPauseManager-for-CSGO)";

    private const int RefereeTeam = -1;

    private PluginConfig _config = new();

    private Timer? _tickTimer;
    private bool _unloading;

    // ------------------------------------------------------------------ state

    private PauseState _state = PauseState.None;

    private int _callerTeam = RefereeTeam;

    private int _pausingTeam = -1;

    private int _tacTimeLeft;

    private int _pendingTicks;

    private int _stallTicks;

    /// <summary>Depth of plugin-issued commands currently executing.</summary>
    private int _internalDepth;

    /// <summary>Tick until which plugin-issued commands may still be in the queue.</summary>
    private int _internalUntilTick = -1;

    private readonly int[] _tacRemaining = new int[2];

    private readonly bool[] _tacUsedThisRound = new bool[2];

    private int _currentOtSegment = -1;

    private bool _matchStarted;

    // ------------------------------------------------------------- round data

    private bool _roundHasDamage;

    private bool _roundInvalidated;

    /// <summary>
    /// True when the round was voided before any damage was done, so the
    /// round start state is rolled back without a referee confirmation.
    /// </summary>
    private bool _autoRestore;

    /// <summary>Legacy behaviour (technicalDamageGate = false): void, never restore.</summary>
    private bool _legacyVoid;

    private bool _rewindArmed;

    private bool _rewindInProgress;

    private bool _roundRestored;

    private bool _snapshotLocked;

    private RoundSnapshot? _snapshot;

    // --------------------------------------------------------------- lifecycle

    public override void Load(bool hotReload)
    {
        _config = PluginConfig.Load(ModuleDirectory);
        Texts.Style = _config.TextStyle;
        ResetTacticalCounts();

        AddCommandListener("mp_pause_match", OnNativePauseMatch, HookMode.Pre);
        AddCommandListener("mp_unpause_match", OnNativeUnpauseMatch, HookMode.Pre);
        AddCommandListener("mp_rewind_match", OnNativeRewindMatch, HookMode.Pre);

        _tickTimer = AddTimer(1.0f, Tick, TimerFlags.REPEAT);

        Logger.LogInformation("[MPM] loaded: tactical={Time}s x{Count}, rewind={Rewind}, style={Style}",
            _config.TacticalTime, _config.TacticalCount, _config.RewindEnabled, _config.TextStyle);
    }

    public override void Unload(bool hotReload)
    {
        _unloading = true;
        _tickTimer?.Kill();
        _tickTimer = null;
    }

    // ------------------------------------------------------------ chat commands

    [GameEventHandler]
    public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var player = Utilities.GetPlayerFromUserid(@event.Userid);
        var text = (@event.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return HookResult.Continue;
        }

        if (text[0] is '!' or '/' or '.')
        {
            HandleChatCommand(player, text[1..].Trim());
            return HookResult.Continue;
        }

        if (_config.WarnChatDuringTechnical &&
            _state == PauseState.TechnicalActive &&
            player != null &&
            player.IsValid &&
            IsPlayingTeam(player))
        {
            player.PrintToChat(Texts.Chat(Txt.SayWarning));
        }

        return HookResult.Continue;
    }

    private void HandleChatCommand(CCSPlayerController? player, string raw)
    {
        var name = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        switch (name)
        {
            case "pause":
            case "tac":
            case "p":
                TryStartTacticalPause(player);
                break;

            case "tech":
                TryStartTechnicalPause(player);
                break;

            case "unpause":
                TryUnpause(player, refereeOverride: false);
                break;

            case "rewind":
                TryRewind(player);
                break;
        }
    }

    // ------------------------------------------------------- console commands

    [ConsoleCommand("css_pause", "Call a tactical timeout")]
    [CommandHelper(0, "", CommandUsage.CLIENT_ONLY)]
    public void OnPauseCommand(CCSPlayerController? player, CommandInfo info)
    {
        TryStartTacticalPause(player);
    }

    [ConsoleCommand("css_tech", "Call a technical pause (server console = referee)")]
    public void OnTechCommand(CCSPlayerController? player, CommandInfo info)
    {
        TryStartTechnicalPause(player);
    }

    [ConsoleCommand("css_unpause", "Resume the current pause")]
    public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo info)
    {
        TryUnpause(player, refereeOverride: false);
    }

    [ConsoleCommand("css_rewind", "Restore the invalidated round (referee)")]
    public void OnRewindCommand(CCSPlayerController? player, CommandInfo info)
    {
        TryRewind(player);
    }

    // ------------------------------------------------------------ game events

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundHasDamage = false;
        _tacUsedThisRound[0] = false;
        _tacUsedThisRound[1] = false;

        var rules = Utilities.GetGameRules();
        if (rules == null)
        {
            return HookResult.Continue;
        }

        if (!_snapshotLocked)
        {
            _snapshot = RoundSnapshotter.Capture(rules);
        }

        /*
         * A technical pause is pending: make sure the freeze time that just
         * started is paused as well (CS2 applies mp_pause_match at a freeze).
         */

        if (_state is PauseState.TechnicalPending or PauseState.TacticalPending)
        {
            IssuePause();
        }

        HandleOvertimeCounts(rules);

        if (!_matchStarted)
        {
            _matchStarted = true;
            AddTimer(1.0f, () => PrintAll(Txt.Live));
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var rules = Utilities.GetGameRules();
        if (rules != null && rules.WarmupPeriod)
        {
            return HookResult.Continue;
        }

        _roundHasDamage = true;
        return HookResult.Continue;
    }

    // ---------------------------------------------------- native mp_ commands

    private HookResult OnNativePauseMatch(CCSPlayerController? player, CommandInfo info)
    {
        if (IsInternalCommand())
        {
            return HookResult.Continue;
        }

        if (_state != PauseState.None)
        {
            // already pausing / paused, swallow duplicates
            return HookResult.Handled;
        }

        // from the server console this is the referee technical pause
        TryStartTechnicalPause(null);
        return HookResult.Handled;
    }

    private HookResult OnNativeUnpauseMatch(CCSPlayerController? player, CommandInfo info)
    {
        if (IsInternalCommand())
        {
            return HookResult.Continue;
        }

        if (_state == PauseState.None)
        {
            return HookResult.Continue;
        }

        TryUnpause(player, refereeOverride: true);
        return HookResult.Handled;
    }

    private HookResult OnNativeRewindMatch(CCSPlayerController? player, CommandInfo info)
    {
        TryRewind(player ?? null, refereeOnly: true);
        return HookResult.Handled;
    }

    // --------------------------------------------------------- tactical pause

    private void TryStartTacticalPause(CCSPlayerController? player)
    {
        var rules = Utilities.GetGameRules();
        if (rules is { WarmupPeriod: true })
        {
            Reply(player, Txt.GuardWarmup);
            return;
        }

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
        {
            Reply(player, Txt.GuardTacPlayerOnly);
            return;
        }

        if (_state is PauseState.TechnicalPending or PauseState.TechnicalActive)
        {
            Reply(player, Txt.GuardPendingTech);
            return;
        }

        if (_state != PauseState.None)
        {
            Reply(player, Txt.GuardPauseActive);
            return;
        }

        var team = TeamOf(player);
        if (team < 0)
        {
            Reply(player, Txt.GuardTacTeamOnly);
            return;
        }

        if (_tacUsedThisRound[team])
        {
            Reply(player, Txt.GuardTacUsed);
            return;
        }

        if (_tacRemaining[team] <= 0)
        {
            Reply(player, Txt.GuardTacNone);
            return;
        }

        _tacRemaining[team]--;
        _tacUsedThisRound[team] = true;
        _pausingTeam = team;
        _callerTeam = team;
        _state = PauseState.TacticalPending;
        _pendingTicks = 0;
        _tacTimeLeft = 0;

        PrintAll(Txt.TacCalled, TeamName(team));
        PrintAll(Txt.TacPending);
        PrintAll(Txt.TacTimeouts, _config.TacticalTime, _tacRemaining[team]);
        ShowHud(new[] { Texts.Get(Txt.HudTacPending, TeamName(team)) });

        IssuePause();
    }

    private void HandleTacticalPending(CCSGameRules rules, bool paused)
    {
        if (paused)
        {
            _state = PauseState.TacticalActive;
            _stallTicks = 0;
            _tacTimeLeft = _config.TacticalTime;
            PrintAll(Txt.TacActive, _tacTimeLeft);
            ShowHud(new[] { Texts.Get(Txt.HudTacActive, TeamName(_pausingTeam), _tacTimeLeft, _tacRemaining[_pausingTeam]) });
            return;
        }

        _pendingTicks++;

        // keep the pending HUD alive while CS2 waits for the next freeze time
        ShowHud(new[] { Texts.Get(Txt.HudTacPending, TeamName(_pausingTeam)) });

        if (_pendingTicks > 180)
        {
            // three minutes without the game pausing: refund and give up
            _state = PauseState.None;
            RefundTactical(_pausingTeam);
            _pausingTeam = -1;
            _callerTeam = RefereeTeam;
            PrintAll(Txt.TacDeclined);
            ShowHud(new[] { Texts.Get(Txt.HudTacDeclined) });
        }
    }

    private void HandleTacticalActive(CCSGameRules rules, bool paused)
    {
        if (!paused)
        {
            _stallTicks++;
            if (_stallTicks > 5)
            {
                ReleaseTacticalPause(announce: true);
            }

            return;
        }

        _stallTicks = 0;
        ShowHud(new[] { Texts.Get(Txt.HudTacActive, TeamName(_pausingTeam), _tacTimeLeft, _tacRemaining[_pausingTeam]) });

        if (_tacTimeLeft is > 0 and <= 5)
        {
            PrintAll(Txt.TacWarn, _tacTimeLeft);
        }

        _tacTimeLeft--;
        if (_tacTimeLeft <= 0)
        {
            EndTacticalPause();
        }
    }

    private void EndTacticalPause()
    {
        if (_state != PauseState.TacticalActive)
        {
            return;
        }

        PrintAll(Txt.TacEnded);
        ShowHud(new[] { Texts.Get(Txt.HudTacEnded) });
        ReleaseTacticalPause(announce: false);
        IssueUnpause();
    }

    private void ReleaseTacticalPause(bool announce)
    {
        _state = PauseState.None;
        _pausingTeam = -1;
        _callerTeam = RefereeTeam;
        _tacTimeLeft = 0;
        _stallTicks = 0;
        _pendingTicks = 0;

        if (announce)
        {
            PrintAll(Txt.TacServerEnded);
            ShowHud(new[] { Texts.Get(Txt.HudTacEnded) });
        }
    }

    private void CancelTacticalPending(CCSPlayerController? player)
    {
        if (_state != PauseState.TacticalPending)
        {
            return;
        }

        RefundTactical(_pausingTeam);
        _state = PauseState.None;
        _pausingTeam = -1;
        _callerTeam = RefereeTeam;
        _pendingTicks = 0;
        IssueUnpause();
        PrintAll(Txt.TacCancelled);
        ShowHud(new[] { Texts.Get(Txt.HudTacEnded) });
    }

    private void RefundTactical(int team)
    {
        if (team is < 0 or > 1)
        {
            return;
        }

        _tacRemaining[team] = Math.Min(_tacRemaining[team] + 1, _config.TacticalCount + 20);
        _tacUsedThisRound[team] = false;
    }

    // --------------------------------------------------------- technical pause

    private void TryStartTechnicalPause(CCSPlayerController? player)
    {
        var rules = Utilities.GetGameRules();
        if (rules is { WarmupPeriod: true })
        {
            Reply(player, Txt.GuardWarmup);
            return;
        }

        if (_state is PauseState.TechnicalPending or PauseState.TechnicalActive)
        {
            return;
        }

        // tactical -> technical: refund and release the tactical pause first
        if (_state is PauseState.TacticalPending or PauseState.TacticalActive)
        {
            RefundTactical(_pausingTeam);
            _pausingTeam = -1;
            _state = PauseState.None;
            IssueUnpause();
        }

        var team = player == null ? -1 : TeamOf(player);
        if (player != null && team < 0)
        {
            Reply(player, Txt.GuardTechTeamOnly);
            return;
        }

        _callerTeam = team;

        _roundRestored = false;
        _rewindArmed = false;
        _autoRestore = false;
        _legacyVoid = false;
        _pendingTicks = 0;
        _state = PauseState.TechnicalPending;

        if (team < 0)
        {
            PrintAll(Txt.TechCalledRef);
        }
        else
        {
            PrintAll(Txt.TechCalledTeam, TeamName(team));
        }

        var hud = Texts.Get(team < 0 ? Txt.HudTechWillFinishRef : Txt.HudTechWillFinishTeam, TeamName(team));
        var terminate = Utilities.GetGameRules();

        if (!_config.TechnicalDamageGate)
        {
            /*
             * Legacy behaviour, mirroring sm_mp_technical_damage_gate 0 on the
             * CS:GO side: void the round at once and never restore it.
             */

            _legacyVoid = true;
            _roundInvalidated = true;
            _snapshotLocked = true;

            PrintAll(Txt.TechGateOffVoid);
            ShowHud(new[] { hud });

            terminate?.TerminateRound(0.1f, RoundEndReason.RoundDraw);
            IssuePause();
            return;
        }

        if (_roundHasDamage)
        {
            /*
             * Damage already done: the round is played out first and the
             * referee decides about the restore.
             */

            _roundInvalidated = true;
            _snapshotLocked = true;

            PrintAll(Txt.TechDamage);
            PrintAll(Txt.TechAfterRound);
            ShowHud(new[] { hud });

            // CS2 queues this until the next freeze time
            IssuePause();
            return;
        }

        if (!_config.RewindEnabled)
        {
            /*
             * No damage, but no rollback available: voiding the round would
             * throw it away with no way back, so it is kept and paused.
             */

            _roundInvalidated = false;
            _snapshotLocked = false;

            PrintAll(Txt.TechNoDamage);
            PrintAll(Txt.TechRewindOff);
            ShowHud(new[] { hud });

            IssuePause();
            return;
        }

        /*
         * No damage yet: void the round right now, pause the freeze time that
         * follows and roll the round start state back automatically.
         */

        _roundInvalidated = true;
        _autoRestore = true;
        _snapshotLocked = true;

        PrintAll(Txt.TechVoidRestoring);
        ShowHud(new[] { hud });

        terminate?.TerminateRound(0.1f, RoundEndReason.RoundDraw);

        IssuePause();
    }

    private void HandleTechnicalPending(CCSGameRules rules, bool paused)
    {
        if (!paused)
        {
            return;
        }

        _state = PauseState.TechnicalActive;
        PrintAll(Txt.TechPaused);

        if (_legacyVoid)
        {
            /* legacy mode never offers a restore */
        }
        else if (_roundInvalidated && _autoRestore)
        {
            /*
             * Voided before any damage: roll the round start state back
             * without asking the referee.
             */

            if (_config.RewindEnabled && _snapshot is { IsValid: true })
            {
                PerformRewind(auto: true);
            }
            else
            {
                _autoRestore = false;
                PrintAll(Txt.TechNoBackup);

                if (!_config.RewindEnabled)
                {
                    PrintAll(Txt.TechRewindOff);
                }
            }
        }
        else if (_roundInvalidated)
        {
            _rewindArmed = _config.RewindEnabled && _snapshot is { IsValid: true } &&
                           rules.TotalRoundsPlayed == _snapshot.RoundsPlayed + 1;

            if (!_config.RewindEnabled)
            {
                PrintAll(Txt.TechRewindOff);
            }

            PrintAll(_rewindArmed ? Txt.TechInvalid : Txt.TechNoBackup);

            if (_rewindArmed)
            {
                PrintAll(Txt.TechRewindHint);
            }
        }
        else
        {
            PrintAll(Txt.TechNoDamage);
        }

        PrintResumeHint();
        ShowHud(new[] { BuildTechnicalHud() });
    }

    private void HandleTechnicalActive(CCSGameRules rules, bool paused)
    {
        if (!paused)
        {
            /*
             * Unpaused outside the plugin (another plugin, a native command
             * the listener could not see, a round restart): release every
             * flag, otherwise the snapshot stays locked for the rest of the map.
             */

            AbortTechnicalPauseExternally();
            return;
        }

        ShowHud(new[] { BuildTechnicalHud() });
    }

    /// <summary>Releases a technical pause the plugin did not end itself.</summary>
    private void AbortTechnicalPauseExternally()
    {
        if (_state is not (PauseState.TechnicalActive or PauseState.TechnicalPending))
        {
            return;
        }

        ResetTechnicalState();

        PrintAll(Txt.TechServerEnded);
        ShowHud(new[] { Texts.Get(Txt.HudTechEnded) });
    }

    /// <summary>Clears every technical pause flag; callers handle the messaging.</summary>
    private void ResetTechnicalState()
    {
        _state = PauseState.None;
        _callerTeam = RefereeTeam;
        _roundInvalidated = false;
        _rewindArmed = false;
        _rewindInProgress = false;
        _roundRestored = false;
        _snapshotLocked = false;
        _autoRestore = false;
        _legacyVoid = false;
        _pendingTicks = 0;
    }

    private void EndTechnicalPause(bool cancelled)
    {
        if (_state is not (PauseState.TechnicalActive or PauseState.TechnicalPending))
        {
            return;
        }

        if (!cancelled && _roundInvalidated && !_roundRestored)
        {
            PrintAll(Txt.ResumeNoRestore);
        }

        ResetTechnicalState();

        if (cancelled)
        {
            PrintAll(Txt.TechCancelled);
            ShowHud(new[] { Texts.Get(Txt.HudTechCancelled) });
            IssueUnpause();
            return;
        }

        PrintAll(Txt.TechEnded);
        ShowHud(new[] { Texts.Get(Txt.HudTechEnded) });
        IssueUnpause();
        AddTimer(2.0f, () => PrintAll(Txt.Live));
    }

    private void CancelTechnicalPending()
    {
        if (_state != PauseState.TechnicalPending)
        {
            return;
        }

        EndTechnicalPause(cancelled: true);
    }

    // ------------------------------------------------------------------ unpause

    private void TryUnpause(CCSPlayerController? player, bool refereeOverride)
    {
        if (_state == PauseState.None)
        {
            if (player != null)
            {
                Reply(player, Txt.GuardNoPause);
            }

            return;
        }

        var referee = refereeOverride || player == null || IsReferee(player);
        var team = player == null ? -1 : TeamOf(player);

        switch (_state)
        {
            case PauseState.TacticalPending:
                if (referee || team == _pausingTeam)
                {
                    CancelTacticalPending(player);
                }
                else
                {
                    Reply(player, Txt.GuardOnlyCallingTeam);
                }

                break;

            case PauseState.TacticalActive:
                if (referee || team == _pausingTeam)
                {
                    EndTacticalPause();
                }
                else
                {
                    Reply(player, Txt.GuardOnlyCallingTeam);
                }

                break;

            case PauseState.TechnicalPending:
                if (referee || team == _callerTeam)
                {
                    CancelTechnicalPending();
                }
                else
                {
                    Reply(player, Txt.GuardPendingTech);
                }

                break;

            case PauseState.TechnicalActive:
                if (referee)
                {
                    EndTechnicalPause(cancelled: false);
                }
                else if (_callerTeam >= 0 && team == _callerTeam)
                {
                    EndTechnicalPause(cancelled: false);
                }
                else if (_callerTeam < 0)
                {
                    Reply(player, Txt.GuardOnlyRefEnd);
                }
                else
                {
                    Reply(player, Txt.GuardOnlyCallingTeam);
                }

                break;
        }
    }

    // ------------------------------------------------------------------- rewind

    private void TryRewind(CCSPlayerController? player, bool refereeOnly = false)
    {
        var referee = player == null || IsReferee(player);

        if (refereeOnly && !referee)
        {
            Reply(player, Txt.RewRefOnly);
            return;
        }

        if (!referee && !refereeOnly && player != null)
        {
            // in game the referee flag is required for a restore
            Reply(player, Txt.RewRefOnly);
            return;
        }

        if (_state != PauseState.TechnicalActive)
        {
            Reply(player, Txt.RewNoPause);
            return;
        }

        if (_rewindInProgress)
        {
            Reply(player, Txt.RewBusy);
            return;
        }

        if (_legacyVoid)
        {
            Reply(player, Txt.RewUnavailable);
            return;
        }

        if (!_roundInvalidated)
        {
            Reply(player, Txt.RewValid);
            return;
        }

        if (_roundRestored)
        {
            Reply(player, Txt.RewDone);
            return;
        }

        if (!_rewindArmed || _snapshot is not { IsValid: true })
        {
            PrintAll(Txt.RewNoBackup);
            PrintAll(Txt.RewFailHint);
            return;
        }

        PerformRewind(auto: false);
    }

    /// <summary>
    /// Writes the snapshot back. Used by the referee command and, without a
    /// confirmation, for a round that was voided before any damage was done.
    /// </summary>
    private void PerformRewind(bool auto)
    {
        if (_rewindInProgress || _snapshot is not { IsValid: true })
        {
            return;
        }

        _rewindInProgress = true;
        PrintAll(Txt.RewStart);

        var snapshot = _snapshot;
        AddTimer(0.2f, () =>
        {
            var scoreOk = RoundSnapshotter.Restore(snapshot, _config.RewindPlayerStats, Logger, out var report);
            var scoreT = RoundSnapshotter.GetTeamScore(CsTeam.Terrorist);
            var scoreCt = RoundSnapshotter.GetTeamScore(CsTeam.CounterTerrorist);
            var rounds = Utilities.GetGameRules()?.TotalRoundsPlayed ?? -1;
            var verified = scoreOk && scoreT == snapshot.ScoreT && scoreCt == snapshot.ScoreCt;

            _roundRestored = verified;
            _rewindInProgress = false;
            _autoRestore = false;

            if (verified)
            {
                _rewindArmed = false;
                _snapshotLocked = false;
                PrintAll(Txt.RewOk, scoreT, scoreCt);
                PrintAll(Txt.RewOkHint);
            }
            else
            {
                PrintAll(Txt.RewUnverified, scoreT, scoreCt, rounds, snapshot.RoundsPlayed);
                PrintAll(Txt.RewStaysInvalid);
            }

            Logger.LogInformation("[MPM] rewind verified={Verified} auto={Auto} detail={Report}", verified, auto, report);
            ShowHud(new[] { BuildTechnicalHud() });
        });
    }

    // --------------------------------------------------------------------- tick

    private void Tick()
    {
        if (_unloading)
        {
            return;
        }

        var rules = Utilities.GetGameRules();
        if (rules == null)
        {
            return;
        }

        var paused = IsPaused(rules);

        switch (_state)
        {
            case PauseState.TacticalPending:
                HandleTacticalPending(rules, paused);
                break;
            case PauseState.TacticalActive:
                HandleTacticalActive(rules, paused);
                break;
            case PauseState.TechnicalPending:
                HandleTechnicalPending(rules, paused);
                break;
            case PauseState.TechnicalActive:
                HandleTechnicalActive(rules, paused);
                break;
        }
    }

    private static bool IsPaused(CCSGameRules rules) => rules.MatchWaitingForResume || rules.GamePaused;

    // ------------------------------------------------------------ team mapping

    /// <summary>Side swaps played so far, derived from the round counter (as in V1.2.4).</summary>
    private static int CountSideSwaps(int roundsPlayed)
    {
        var maxRounds = Math.Max(2, CvarInt("mp_maxrounds", 30));
        var halfRounds = Math.Max(1, maxRounds / 2);
        var swaps = roundsPlayed >= halfRounds ? 1 : 0;

        if (CvarInt("mp_overtime_enable", 0) == 0)
        {
            return swaps;
        }

        var otRounds = Math.Max(2, CvarInt("mp_overtime_maxrounds", 6));
        var otPlayed = roundsPlayed - maxRounds;
        if (otPlayed <= 0)
        {
            return swaps;
        }

        var halfOt = otRounds / 2;
        if (halfOt <= 0)
        {
            return swaps;
        }

        if (otPlayed >= halfOt)
        {
            swaps += 1 + (otPlayed - halfOt) / otRounds;
        }

        return swaps;
    }

    private static int CurrentSideSwaps()
    {
        var rules = Utilities.GetGameRules();
        return rules == null ? 0 : CountSideSwaps(rules.TotalRoundsPlayed);
    }

    private static CsTeam SideOfTeam(int matchTeam)
    {
        var flipped = CurrentSideSwaps() % 2 == 1;
        var aIsCt = !flipped;
        if (matchTeam == 0)
        {
            return aIsCt ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        }

        return aIsCt ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
    }

    private static int TeamOf(CCSPlayerController player)
    {
        var side = player.Team;
        if (side is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            return -1;
        }

        var flipped = CurrentSideSwaps() % 2 == 1;
        var ctIsTeamA = !flipped;
        return side == CsTeam.CounterTerrorist
            ? ctIsTeamA ? 0 : 1
            : ctIsTeamA ? 1 : 0;
    }

    private static string TeamName(int matchTeam)
    {
        if (matchTeam is < 0 or > 1)
        {
            return "REFEREE";
        }

        var team = RoundSnapshotter.FindTeam(SideOfTeam(matchTeam));
        var name = team?.ClanTeamname;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = team?.Teamname;
        }

        if (string.IsNullOrWhiteSpace(name) || name is "CT" or "TERRORIST" or "TERRORIST ")
        {
            return matchTeam == 0 ? "Team A" : "Team B";
        }

        return name!;
    }

    // --------------------------------------------------------------- overtime

    private void HandleOvertimeCounts(CCSGameRules rules)
    {
        if (CvarInt("mp_overtime_enable", 0) == 0)
        {
            return;
        }

        var maxRounds = Math.Max(2, CvarInt("mp_maxrounds", 30));
        if (rules.TotalRoundsPlayed < maxRounds)
        {
            return;
        }

        /*
         * Only a draw can lead to overtime: a regulation win (for example
         * 16:14) must not refresh the tactical timeouts.
         */

        if (RoundSnapshotter.GetTeamScore(CsTeam.Terrorist) != RoundSnapshotter.GetTeamScore(CsTeam.CounterTerrorist))
        {
            return;
        }

        var otRounds = Math.Max(2, CvarInt("mp_overtime_maxrounds", 6));
        var segment = (rules.TotalRoundsPlayed - maxRounds) / otRounds;
        if (segment == _currentOtSegment)
        {
            return;
        }

        _currentOtSegment = segment;
        SetCounts(_config.OvertimeCount);
        PrintAll(Txt.Overtime, _config.OvertimeCount);
    }

    private void SetCounts(int count)
    {
        _tacRemaining[0] = count;
        _tacRemaining[1] = count;
    }

    private void ResetTacticalCounts()
    {
        SetCounts(_config.TacticalCount);
        _tacUsedThisRound[0] = false;
        _tacUsedThisRound[1] = false;
        _currentOtSegment = -1;
        _matchStarted = false;
    }

    // ----------------------------------------------------------------- helpers

    private static int CvarInt(string name, int fallback)
    {
        var intCvar = ConVar<int>.Find(name);
        if (intCvar != null)
        {
            return intCvar.Value;
        }

        // some CS2 cvars are exposed as floats
        var floatCvar = ConVar<float>.Find(name);
        return floatCvar != null ? (int)floatCvar.Value : fallback;
    }

    private static bool IsPlayingTeam(CCSPlayerController player)
        => player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist;

    private static bool IsReferee(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
        {
            return true;
        }

        try
        {
            return AdminManager.PlayerHasPermissions(player, new[] { "@css/root" })
                || AdminManager.PlayerHasCommandOverride(player, "css_rewind");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Marks the next few commands as originating from the plugin itself.</summary>
    private void IssuePause() => ExecuteInternal("mp_pause_match");

    private void IssueUnpause() => ExecuteInternal("mp_unpause_match");

    private void ExecuteInternal(string command)
    {
        _internalDepth++;
        _internalUntilTick = Server.TickCount + 2;

        try
        {
            Server.ExecuteCommand(command);
        }
        finally
        {
            _internalDepth--;
        }
    }

    private bool IsInternalCommand()
        => _internalDepth > 0 || Server.TickCount <= _internalUntilTick;

    private void PrintAll(Txt id, params object[] args)
        => Server.PrintToChatAll(Texts.Chat(id, args));

    private void Reply(CCSPlayerController? player, Txt id, params object[] args)
    {
        var text = Texts.Chat(id, args);
        if (player != null && player.IsValid)
        {
            player.PrintToChat(text);
        }
        else
        {
            Server.PrintToConsole(text);
        }
    }

    private void PrintResumeHint()
    {
        if (_callerTeam < 0)
        {
            PrintAll(Txt.TechRefOnly);
        }
        else
        {
            PrintAll(Txt.TechUnpauseHint, TeamName(_callerTeam));
        }
    }

    private string BuildTechnicalHud()
    {
        string status;

        if (!_roundInvalidated)
        {
            status = Texts.Get(Txt.StatusValid);
        }
        else if (_roundRestored)
        {
            status = Texts.Get(Txt.StatusRestored);
        }
        else if (_autoRestore || _rewindInProgress)
        {
            status = Texts.Get(Txt.StatusRestoring);
        }
        else if (_rewindArmed)
        {
            status = Texts.Get(Txt.StatusInvalid);
        }
        else
        {
            status = Texts.Get(Txt.StatusNotRestored);
        }

        return _callerTeam < 0
            ? Texts.Get(Txt.HudTechRef, status)
            : Texts.Get(Txt.HudTechTeam, TeamName(_callerTeam), status);
    }

    private void ShowHud(IEnumerable<string> lines)
    {
        var html = string.Join(
            "<br>",
            lines.SelectMany(line => line.Split('\n')).Select(EscapeHtml));

        foreach (var player in Utilities.GetPlayers())
        {
            if (player != null && player.IsValid && !player.IsHLTV)
            {
                player.PrintToCenterHtml(html);
            }
        }
    }

    private static string EscapeHtml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
