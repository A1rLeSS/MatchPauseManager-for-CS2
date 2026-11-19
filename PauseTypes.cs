namespace MatchPauseManager;

/// <summary>Pause lifecycle states, mirroring the SourceMod plugin.</summary>
public enum PauseState
{
    None = 0,

    /// <summary>A tactical timeout was requested, waiting for CS2 to actually pause.</summary>
    TacticalPending,

    TacticalActive,

    /// <summary>A technical pause was requested on a round that is still being played.</summary>
    TechnicalPending,

    TechnicalActive,
}

/// <summary>Team identity (roster), not side.</summary>
public enum MatchTeam
{
    A = 0,
    B = 1,
}

/// <summary>Who called a pause.</summary>
public enum PauseCaller
{
    Referee = -1,
    TeamA = 0,
    TeamB = 1,
}

/// <summary>Snapshot of one player, taken at round start.</summary>
public sealed class PlayerSnapshot
{
    public ulong SteamId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Team { get; set; }

    public int Money { get; set; }

    public int Health { get; set; }

    public int Armor { get; set; }

    public bool Helmet { get; set; }

    public bool Defuser { get; set; }

    public int Mvps { get; set; }

    public int Score { get; set; }

    public List<string> Weapons { get; set; } = new();
}

/// <summary>Snapshot of the whole match state at round start.</summary>
public sealed class RoundSnapshot
{
    public int RoundsPlayed { get; set; }

    public int ScoreT { get; set; }

    public int ScoreCt { get; set; }

    public Dictionary<ulong, PlayerSnapshot> Players { get; set; } = new();

    public bool IsValid { get; set; }

    public DateTime TakenAt { get; set; } = DateTime.Now;
}
