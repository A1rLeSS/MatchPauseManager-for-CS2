using CounterStrikeSharp.API.Modules.Utils;

namespace MatchPauseManager;

/// <summary>Message ids (ported from the SourceMod plugin's text table).</summary>
public enum Txt
{
    TacCalled, TacPending, TacTimeouts, TacActive, TacWarn, TacEnded, TacDeclined, TacServerEnded, TacCancelled,
    HudTacPending, HudTacActive, HudTacEnded, HudTacDeclined,

    TechCalledTeam, TechCalledRef, TechDamage, TechAfterRound, TechRewindHint, TechCancelHint,
    TechPaused, TechNoDamage, TechVoidRestoring, TechInvalid, TechNoBackup, TechRewindOff, TechRefOnly, TechUnpauseHint,
    TechEnded, TechCancelled, TechServerEnded, TechGateOffVoid, ResumeNoRestore,
    HudTechWillFinishRef, HudTechWillFinishTeam, HudTechRef, HudTechTeam, HudTechCancelled, HudTechEnded,
    StatusValid, StatusInvalid, StatusRestored, StatusNotRestored, StatusRestoring,

    RewStart, RewOk, RewOkHint, RewUnverified, RewNoBackup, RewFailHint, RewStaysInvalid,
    RewNothing, RewBusy, RewValid, RewDone, RewUnavailable, RewRefOnly, RewNoPause,

    Overtime, SayWarning, Live, HudLive,

    GuardTacPlayerOnly, GuardTacTeamOnly, GuardTacUsed, GuardTacNone, GuardPauseActive,
    GuardTechTeamOnly, GuardTechPreparing, GuardNoPause, GuardRestartBusy, GuardOnlyRefEnd,
    GuardOnlyCallingTeam, GuardTacNotStarted, GuardPendingTech, GuardWarmup,
}

/// <summary>
/// Broadcast texts, ported from the CS:GO plugin (V1.2.6).
/// Three packs: 0 = international, 1 = Chinese, 2 = international + rule reminders.
/// Tokens: {g} green, {d} default, {r} red. Format arguments use {0}, {1}, ...
/// </summary>
public static class Texts
{
    public static int Style { get; set; }

    private static readonly Dictionary<Txt, string> En = new()
    {
        [Txt.TacCalled] = "{g}TACTICAL TIMEOUT{d} — {0}",
        [Txt.TacPending] = "{d}PAUSE PENDING — WAITING FOR THE SERVER",
        [Txt.TacTimeouts] = "{d}TIME {g}{0}{d} SEC | TIMEOUTS LEFT {g}{1}",
        [Txt.TacActive] = "{g}TACTICAL TIMEOUT{d} — {g}{0} SECONDS",
        [Txt.TacWarn] = "{d}TACTICAL TIMEOUT ENDS IN {g}{0}{d} SEC",
        [Txt.TacEnded] = "{g}TACTICAL TIMEOUT ENDED{d} — RESUME",
        [Txt.TacDeclined] = "{r}TACTICAL TIMEOUT DECLINED{d} — THE SERVER DID NOT ENTER PAUSE",
        [Txt.TacServerEnded] = "{r}TACTICAL TIMEOUT ENDED BY THE SERVER",
        [Txt.TacCancelled] = "{r}TACTICAL TIMEOUT CANCELLED{d} — ROUND CONTINUES",
        [Txt.HudTacPending] = "TACTICAL TIMEOUT\n{0}\nPAUSE PENDING",
        [Txt.HudTacActive] = "TACTICAL TIMEOUT\n{0}\n{1} SECONDS REMAINING\nTIMEOUTS LEFT {2}",
        [Txt.HudTacEnded] = "TACTICAL TIMEOUT ENDED\nRESUME",
        [Txt.HudTacDeclined] = "TACTICAL TIMEOUT DECLINED\nTIMEOUT NOT USED",

        [Txt.TechCalledTeam] = "{g}TECHNICAL PAUSE{d} — {0}",
        [Txt.TechCalledRef] = "{g}TECHNICAL PAUSE{d} — REFEREE",
        [Txt.TechDamage] = "{g}ROUND ALREADY IN PROGRESS{d} — DAMAGE DONE, THE ROUND IS PLAYED OUT FIRST",
        [Txt.TechAfterRound] = "{d}TECHNICAL PAUSE STARTS AFTER THIS ROUND — THE ROUND WILL BE RESTORED",
        [Txt.TechRewindHint] = "{g}ADMIN: mp_rewind_match{d} TO RESTORE",
        [Txt.TechCancelHint] = "{d}!unpause / mp_unpause_match TO CANCEL",
        [Txt.TechPaused] = "{g}MATCH PAUSED",
        [Txt.TechNoDamage] = "{g}NO DAMAGE DONE{d} — THE ROUND IS KEPT, NOTHING TO RESTORE",
        [Txt.TechVoidRestoring] = "{g}NO DAMAGE DONE{d} — ROUND VOIDED, RESTORING ROUND START",
        [Txt.TechInvalid] = "{r}ROUND INVALID{d} — ADMIN: {g}mp_rewind_match{d} TO RESTORE",
        [Txt.TechNoBackup] = "{r}ROUND INVALID{d} — NO SNAPSHOT AVAILABLE TO RESTORE",
        [Txt.TechRewindOff] = "{d}ROUND RESTORE DISABLED (rewindEnabled = false)",
        [Txt.TechRefOnly] = "{d}ONLY THE REFEREE CAN RESUME",
        [Txt.TechUnpauseHint] = "{d}{0} MAY TYPE {g}!unpause",
        [Txt.TechEnded] = "{g}TECHNICAL PAUSE ENDED{d} — GET READY",
        [Txt.TechCancelled] = "{r}TECHNICAL PAUSE CANCELLED{d} — ROUND CONTINUES",
        [Txt.TechServerEnded] = "{r}TECHNICAL PAUSE ENDED BY THE SERVER",
        [Txt.TechGateOffVoid] = "{r}ROUND VOIDED{d} — ROUND RESTORE DISABLED (technicalDamageGate = false)",
        [Txt.ResumeNoRestore] = "{r}RESUMING WITHOUT A RESTORE{d} — THE INVALID ROUND KEEPS ITS RESULT",
        [Txt.HudTechWillFinishRef] = "TECHNICAL PAUSE REQUESTED\nTHE REFEREE\nROUND IS PLAYED OUT FIRST\nTHE ROUND WILL BE RESTORED",
        [Txt.HudTechWillFinishTeam] = "TECHNICAL PAUSE REQUESTED\n{0}\nROUND IS PLAYED OUT FIRST\nTHE ROUND WILL BE RESTORED",
        [Txt.HudTechRef] = "TECHNICAL PAUSE\nTHE REFEREE\n{0}\nONLY THE REFEREE CAN RESUME",
        [Txt.HudTechTeam] = "TECHNICAL PAUSE\n{0}\n{1}\nCALLING TEAM: TYPE !unpause",
        [Txt.HudTechCancelled] = "TECHNICAL PAUSE CANCELLED\nPLAY CONTINUES",
        [Txt.HudTechEnded] = "TECHNICAL PAUSE ENDED\nGET READY",
        [Txt.StatusValid] = "ROUND IS VALID — RESUME ON UNPAUSE",
        [Txt.StatusInvalid] = "ROUND INVALID — ADMIN: mp_rewind_match",
        [Txt.StatusRestored] = "ROUND RESTORED — READY",
        [Txt.StatusNotRestored] = "ROUND INVALID — NOT RESTORED",
        [Txt.StatusRestoring] = "ROUND VOIDED — RESTORING ROUND START",

        [Txt.RewStart] = "{d}ROUND RESTORE IN PROGRESS",
        [Txt.RewOk] = "{g}ROUND RESTORED{d} — SCORE {g}{0} : {1}",
        [Txt.RewOkHint] = "{g}ADMIN: mp_unpause_match{d} TO RESUME",
        [Txt.RewUnverified] = "{r}ROUND RESTORE UNVERIFIED{d} — SCORE {0}:{1} | ROUND {2} (SNAPSHOT {3})",
        [Txt.RewNoBackup] = "{r}ROUND RESTORE FAILED{d} — NO SNAPSHOT FOR THIS ROUND",
        [Txt.RewFailHint] = "{d}CHECK THE PLUGIN LOG AND rewindEnabled",
        [Txt.RewStaysInvalid] = "{d}THE ROUND STAYS INVALID",
        [Txt.RewNothing] = "THERE IS NO PAUSED ROUND TO RESTORE.",
        [Txt.RewBusy] = "A ROUND RESTORE IS ALREADY IN PROGRESS.",
        [Txt.RewValid] = "THIS ROUND IS STILL VALID. NOTHING TO RESTORE.",
        [Txt.RewDone] = "THIS ROUND HAS ALREADY BEEN RESTORED.",
        [Txt.RewUnavailable] = "NO ROUND RESTORE IS AVAILABLE FOR THIS PAUSE.",
        [Txt.RewRefOnly] = "{r}ONLY THE REFEREE CAN RESTORE A ROUND",
        [Txt.RewNoPause] = "{d}NO PAUSE IS ACTIVE",

        [Txt.Overtime] = "{g}OVERTIME{d} — TACTICAL TIMEOUTS REFRESHED: {g}{0}{d} PER TEAM",
        [Txt.SayWarning] = "{r}WARNING:{d} NO COMMUNICATION DURING A TECHNICAL PAUSE",
        [Txt.Live] = "{g}LIVE! LIVE! LIVE!",
        [Txt.HudLive] = "LIVE\nPLAY RESUMING",

        [Txt.GuardTacPlayerOnly] = "{d}TACTICAL TIMEOUT MUST BE CALLED BY A PLAYER",
        [Txt.GuardTacTeamOnly] = "{d}JOIN T OR CT TO CALL A TIMEOUT",
        [Txt.GuardTacUsed] = "{d}TIMEOUT ALREADY USED THIS ROUND",
        [Txt.GuardTacNone] = "{d}NO TACTICAL TIMEOUTS LEFT",
        [Txt.GuardPauseActive] = "{d}A PAUSE IS ALREADY ACTIVE",
        [Txt.GuardTechTeamOnly] = "{d}JOIN T OR CT TO CALL A TECHNICAL PAUSE",
        [Txt.GuardTechPreparing] = "{d}A TECHNICAL PAUSE IS BEING PREPARED",
        [Txt.GuardNoPause] = "{d}NO PAUSE IS ACTIVE",
        [Txt.GuardRestartBusy] = "{d}THE ROUND RESTART IS STILL IN PROGRESS",
        [Txt.GuardOnlyRefEnd] = "{d}ONLY THE REFEREE CAN END THIS PAUSE",
        [Txt.GuardOnlyCallingTeam] = "{d}ONLY THE CALLING TEAM MAY END THIS TIMEOUT",
        [Txt.GuardTacNotStarted] = "{d}THE PAUSE HAS NOT STARTED YET",
        [Txt.GuardPendingTech] = "{d}A TECHNICAL PAUSE IS ALREADY BEING PREPARED",
        [Txt.GuardWarmup] = "{r}WARMUP IS ACTIVE{d} — RUN mp_warmup_end TO START THE MATCH",
    };

    private static readonly Dictionary<Txt, string> Cn = new()
    {
        [Txt.TacCalled] = "{g}战术暂停{d} — {0}",
        [Txt.TacPending] = "{d}等待服务器进入暂停状态",
        [Txt.TacTimeouts] = "{d}时长 {g}{0}{d} 秒 | 剩余次数 {g}{1}",
        [Txt.TacActive] = "{g}战术暂停{d} — {g}{0} 秒",
        [Txt.TacWarn] = "{d}战术暂停剩余 {g}{0}{d} 秒",
        [Txt.TacEnded] = "{g}战术暂停结束{d} — 比赛继续",
        [Txt.TacDeclined] = "{r}战术暂停未生效{d} — 服务器未进入暂停状态",
        [Txt.TacServerEnded] = "{r}战术暂停被服务器结束",
        [Txt.TacCancelled] = "{r}战术暂停已取消{d} — 回合继续",
        [Txt.HudTacPending] = "战术暂停\n{0}\n等待暂停生效",
        [Txt.HudTacActive] = "战术暂停\n{0}\n剩余 {1} 秒\n剩余次数 {2}",
        [Txt.HudTacEnded] = "战术暂停结束\n比赛继续",
        [Txt.HudTacDeclined] = "战术暂停未生效\n次数未消耗",

        [Txt.TechCalledTeam] = "{g}技术暂停{d} — {0}",
        [Txt.TechCalledRef] = "{g}技术暂停{d} — 裁判",
        [Txt.TechDamage] = "{g}本回合已产生伤害{d} — 先打完整回合",
        [Txt.TechAfterRound] = "{d}回合一结束即技术暂停，随后回溯该回合",
        [Txt.TechRewindHint] = "{g}裁判：mp_rewind_match{d} 回溯",
        [Txt.TechCancelHint] = "{d}输入 !unpause / mp_unpause_match 取消请求",
        [Txt.TechPaused] = "{g}比赛已暂停",
        [Txt.TechNoDamage] = "{g}本回合尚无伤害{d} — 回合保留，无需回溯",
        [Txt.TechVoidRestoring] = "{g}本回合尚无伤害{d} — 已作废，正在回溯回合开始状态",
        [Txt.TechInvalid] = "{r}本回合无效{d} — 裁判输入 {g}mp_rewind_match{d} 回溯",
        [Txt.TechNoBackup] = "{r}本回合无效{d} — 没有可用的回合快照",
        [Txt.TechRewindOff] = "{d}回溯功能已关闭（rewindEnabled = false）",
        [Txt.TechRefOnly] = "{d}只有裁判可以恢复比赛",
        [Txt.TechUnpauseHint] = "{d}{0} 可输入 {g}!unpause",
        [Txt.TechEnded] = "{g}技术暂停结束{d} — 准备",
        [Txt.TechCancelled] = "{r}技术暂停已取消{d} — 回合继续",
        [Txt.TechServerEnded] = "{r}技术暂停被服务器结束",
        [Txt.TechGateOffVoid] = "{r}本回合已作废{d} — 回溯已关闭（technicalDamageGate = false）",
        [Txt.ResumeNoRestore] = "{r}未回溯直接继续{d} — 无效回合结果保留",
        [Txt.HudTechWillFinishRef] = "技术暂停请求\n发起方：裁判\n本回合先打完\n随后回溯该回合",
        [Txt.HudTechWillFinishTeam] = "技术暂停请求\n{0}\n本回合先打完\n随后回溯该回合",
        [Txt.HudTechRef] = "技术暂停\n发起方：裁判\n{0}\n只有裁判可以恢复比赛",
        [Txt.HudTechTeam] = "技术暂停\n{0}\n{1}\n发起方可输入 !unpause",
        [Txt.HudTechCancelled] = "技术暂停已取消\n比赛继续",
        [Txt.HudTechEnded] = "技术暂停结束\n准备",
        [Txt.StatusValid] = "本回合有效 — 解除暂停后继续",
        [Txt.StatusInvalid] = "本回合无效 — 裁判执行 mp_rewind_match",
        [Txt.StatusRestored] = "回合已回溯 — 可以继续",
        [Txt.StatusNotRestored] = "本回合无效 — 未回溯",
        [Txt.StatusRestoring] = "本回合已作废 — 正在回溯回合开始状态",

        [Txt.RewStart] = "{d}正在回溯回合",
        [Txt.RewOk] = "{g}回合已回溯{d} — 比分 {g}{0} : {1}",
        [Txt.RewOkHint] = "{g}裁判：执行 mp_unpause_match{d} 继续比赛",
        [Txt.RewUnverified] = "{r}回溯未通过校验{d} — 比分 {0}:{1} | 回合 {2}（快照 {3}）",
        [Txt.RewNoBackup] = "{r}回溯失败{d} — 本回合没有可用快照",
        [Txt.RewFailHint] = "{d}请检查插件日志与 rewindEnabled",
        [Txt.RewStaysInvalid] = "{d}本回合仍为无效",
        [Txt.RewNothing] = "当前没有可回溯的暂停回合。",
        [Txt.RewBusy] = "正在回溯，请稍候。",
        [Txt.RewValid] = "本回合仍然有效，无需回溯。",
        [Txt.RewDone] = "本回合已经回溯过了。",
        [Txt.RewUnavailable] = "本次暂停没有可用的回溯。",
        [Txt.RewRefOnly] = "{r}只有裁判可以回溯回合",
        [Txt.RewNoPause] = "{d}当前没有暂停",

        [Txt.Overtime] = "{g}加时赛{d} — 战术暂停刷新为每队 {g}{0}{d} 次",
        [Txt.SayWarning] = "{r}警告：{d}技术暂停期间禁止交流",
        [Txt.Live] = "{g}比赛继续 — LIVE! LIVE! LIVE!",
        [Txt.HudLive] = "LIVE\n比赛继续",

        [Txt.GuardTacPlayerOnly] = "{d}战术暂停必须由玩家发起",
        [Txt.GuardTacTeamOnly] = "{d}需加入 T 或 CT 才能发起战术暂停",
        [Txt.GuardTacUsed] = "{d}本回合的暂停次数已用完",
        [Txt.GuardTacNone] = "{d}战术暂停次数已用完",
        [Txt.GuardPauseActive] = "{d}已有暂停在进行中",
        [Txt.GuardTechTeamOnly] = "{d}需加入 T 或 CT 才能发起技术暂停",
        [Txt.GuardTechPreparing] = "{d}技术暂停正在准备中",
        [Txt.GuardNoPause] = "{d}当前没有暂停",
        [Txt.GuardRestartBusy] = "{d}回合重开仍在进行中",
        [Txt.GuardOnlyRefEnd] = "{d}只有裁判可以结束本次技术暂停",
        [Txt.GuardOnlyCallingTeam] = "{d}只有发起方队伍可以结束本次暂停",
        [Txt.GuardTacNotStarted] = "{d}暂停尚未开始",
        [Txt.GuardPendingTech] = "{d}技术暂停正在准备中",
        [Txt.GuardWarmup] = "{r}当前处于热身阶段{d} — 执行 mp_warmup_end 开始比赛",
    };

    /// <summary>Style 2: international pack plus the rule reminders tournaments display.</summary>
    private static readonly Dictionary<Txt, string> Rules = new()
    {
        [Txt.TechPaused] = "{g}MATCH PAUSED{d} — NO COMMUNICATION",
        [Txt.TechInvalid] = "{r}ROUND INVALID{d} — ADMIN: {g}mp_rewind_match{d} TO RESTORE | NO COMMUNICATION",
        [Txt.StatusInvalid] = "ROUND INVALID — ADMIN RESTORE | NO COMMUNICATION",
        [Txt.TechNoDamage] = "{g}NO DAMAGE DONE{d} — ROUND CONTINUES. NO COMMUNICATION DURING THE PAUSE.",
        [Txt.TechVoidRestoring] = "{g}NO DAMAGE DONE{d} — ROUND VOIDED AND RESTORED. NO COMMUNICATION DURING THE PAUSE.",
        [Txt.TechAfterRound] = "{d}TECHNICAL PAUSE AFTER THIS ROUND — NO COMMUNICATION UNTIL THEN",
        [Txt.TacCalled] = "{g}TACTICAL TIMEOUT{d} — {0} | NO COMMUNICATION",
        [Txt.TacActive] = "{g}TACTICAL TIMEOUT{d} — {g}{0} SECONDS{d} | NO COMMUNICATION",
        [Txt.HudTechRef] = "TECHNICAL PAUSE\nTHE REFEREE\n{0}\nONLY THE REFEREE CAN RESUME | NO COMMUNICATION",
        [Txt.HudTechTeam] = "TECHNICAL PAUSE\n{0}\n{1}\nCALLING TEAM: TYPE !unpause | NO COMMUNICATION",
        [Txt.SayWarning] = "{r}WARNING:{d} NO COMMUNICATION DURING A TECHNICAL PAUSE. YOU MAY BE PENALISED.",
    };

    public static string Get(Txt id, params object[] args)
    {
        var source = Style switch
        {
            1 => Cn,
            2 => Rules,
            _ => En,
        };

        if (!source.TryGetValue(id, out var text))
        {
            // style 1 falls back to English, style 2 (overrides only) falls back too
            text = En.TryGetValue(id, out var fallback) ? fallback : id.ToString();
        }

        text = text
            .Replace("{g}", ChatColors.Green.ToString())
            .Replace("{d}", ChatColors.Default.ToString())
            .Replace("{r}", ChatColors.Red.ToString());

        return args.Length == 0 ? text : string.Format(text, args);
    }

    /// <summary>Single chat line prefixed with the [CM] tag.</summary>
    public static string Chat(Txt id, params object[] args)
        => $"{ChatColors.Grey}[CM]{ChatColors.Default} {Get(id, args)}";
}
