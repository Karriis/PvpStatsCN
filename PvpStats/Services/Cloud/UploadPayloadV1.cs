using System;
using System.Collections.Generic;

namespace PvpStats.Services.Cloud;

internal sealed class UploadEnvelopeV1 {
    public int SchemaVersion { get; init; } = 1;
    public DateTime ExportedAt { get; init; }
    public required UploadClientV1 Client { get; init; }
    public required List<FrontlineMatchV1> Matches { get; init; }
    public List<IdentityObservationV1>? IdentityObservations { get; init; }
}

internal sealed class UploadClientV1 {
    public required string PluginVersion { get; init; }
    public required string GameVersion { get; init; }
    public required string BuildHash { get; init; }
    public string? DataCenter { get; init; }
}

internal sealed class UploadAliasV1 {
    public required string Name { get; init; }
    public required string HomeWorld { get; init; }
    public uint HomeWorldId { get; init; }
}

internal sealed class IdentityObservationV1 {
    public string? AccountId { get; init; }
    public required string ContentId { get; init; }
    public required UploadAliasV1 CurrentAlias { get; init; }
    public List<UploadAliasV1>? LinkedAliases { get; init; }
    public required string Source { get; init; }
    public DateTime ObservedAt { get; init; }
}

internal sealed class FrontlineMatchV1 {
    public required string SourceMatchId { get; init; }
    public int SourceVersion { get; init; }
    public bool IsCompleted { get; init; }
    public ulong ValidationFlags { get; init; }
    public DateTime DutyStartTime { get; init; }
    public DateTime MatchStartTime { get; init; }
    public DateTime MatchEndTime { get; init; }
    public uint DutyId { get; init; }
    public uint TerritoryId { get; init; }
    public required string Arena { get; init; }
    public string? DataCenter { get; init; }
    public required string GameVersion { get; init; }
    public required string PluginVersion { get; init; }
    public int PlayerCount { get; init; }
    public UploadAliasV1? LocalPlayer { get; init; }
    public required List<FrontlineParticipantV1> Players { get; init; }
    public required List<FrontlineTeamV1> Teams { get; init; }
    public FrontlineTimelineV1? Timeline { get; init; }
}

internal sealed class FrontlineParticipantV1 {
    public required UploadAliasV1 Alias { get; init; }
    public string? AccountId { get; init; }
    public string? ContentId { get; init; }
    public required string Job { get; init; }
    public uint ClassJobId { get; init; }
    public required string Team { get; init; }
    public int Alliance { get; init; }
    public required FrontlineScoreboardV1 Scoreboard { get; init; }
    public int? MaxBattleHigh { get; init; }
}

internal sealed class FrontlineScoreboardV1 {
    public long Kills { get; init; }
    public long Deaths { get; init; }
    public long Assists { get; init; }
    public long DamageDealt { get; init; }
    public long DamageToOther { get; init; }
    public long DamageTaken { get; init; }
    public long HpRestored { get; init; }
    public long Occupations { get; init; }
    public long Special1 { get; init; }
    public long ClaimTimeMillis { get; init; }
}

internal sealed class FrontlineTeamV1 {
    public required string Team { get; init; }
    public int Placement { get; init; }
    public long TotalPoints { get; init; }
    public long KillPoints { get; init; }
    public long DeathPointLosses { get; init; }
    public long OccupationPoints { get; init; }
    public long TargetablePoints { get; init; }
    public long DronePoints { get; init; }
}

internal sealed class FrontlineTimelineV1 {
    public List<TeamPointsEventV1>? TeamPoints { get; init; }
    public List<BattleHighEventV1>? SelfBattleHigh { get; init; }
}

internal sealed class TeamPointsEventV1 {
    public DateTime Timestamp { get; init; }
    public required string Team { get; init; }
    public long Points { get; init; }
}

internal sealed class BattleHighEventV1 {
    public DateTime Timestamp { get; init; }
    public int Level { get; init; }
}
