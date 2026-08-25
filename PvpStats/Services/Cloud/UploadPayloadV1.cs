using System;
using System.Collections.Generic;

namespace PvpStats.Services.Cloud;

internal sealed class UploadEnvelopeV1 {
    public int SchemaVersion { get; init; } = 1;
    public DateTime ExportedAt { get; init; }
    public required UploadClientV1 Client { get; init; }
    public required List<FrontlineMatchV1> Matches { get; init; }
    public List<CrystallineConflictMatchV1>? CrystallineConflictMatches { get; init; }
    public List<RivalWingsMatchV1>? RivalWingsMatches { get; init; }
    public List<IdentityObservationV1>? IdentityObservations { get; set; }
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

internal sealed class CrystallineConflictMatchV1 {
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
    public required string MatchType { get; init; }
    public required string Winner { get; init; }
    public bool Overtime { get; init; }
    public string? DataCenter { get; init; }
    public required string GameVersion { get; init; }
    public required string PluginVersion { get; init; }
    public UploadAliasV1? LocalPlayer { get; init; }
    public required List<CrystallineConflictParticipantV1> Players { get; init; }
    public required List<CrystallineConflictTeamV1> Teams { get; init; }
    public CrystallineConflictTimelineV1? Timeline { get; init; }
}

internal sealed class CrystallineConflictParticipantV1 {
    public required UploadAliasV1 Alias { get; init; }
    public string? AccountId { get; init; }
    public string? ContentId { get; init; }
    public required string Job { get; init; }
    public uint ClassJobId { get; init; }
    public required string Team { get; init; }
    public required CrystallineConflictScoreboardV1 Scoreboard { get; init; }
}

internal sealed class CrystallineConflictScoreboardV1 {
    public long Kills { get; init; }
    public long Deaths { get; init; }
    public long Assists { get; init; }
    public long DamageDealt { get; init; }
    public long DamageTaken { get; init; }
    public long HpRestored { get; init; }
    public long TimeOnCrystalMillis { get; init; }
}

internal sealed class CrystallineConflictTeamV1 { public required string Team { get; init; } public int Placement { get; init; } public long Progress { get; init; } }
internal sealed class CrystallineConflictTimelineV1 { public List<CrystallineConflictProgressEventV1>? CrystalProgress { get; init; } public List<CrystallineConflictKnockoutEventV1>? Knockouts { get; init; } public List<CrystallineConflictLimitBreakEventV1>? LimitBreaks { get; init; } }
internal sealed class CrystallineConflictProgressEventV1 { public DateTime Timestamp { get; init; } public string? Team { get; init; } public long Points { get; init; } }
internal sealed class CrystallineConflictKnockoutEventV1 { public DateTime Timestamp { get; init; } public required UploadAliasV1 Victim { get; init; } public UploadAliasV1? Killer { get; init; } }
internal sealed class CrystallineConflictLimitBreakEventV1 { public DateTime Timestamp { get; init; } public required UploadAliasV1 Actor { get; init; } public uint ActionId { get; init; } public required string Phase { get; init; } }

internal sealed class RivalWingsMatchV1 {
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
    public required string Winner { get; init; }
    public string? DataCenter { get; init; }
    public required string GameVersion { get; init; }
    public required string PluginVersion { get; init; }
    public int PlayerCount { get; init; }
    public UploadAliasV1? LocalPlayer { get; init; }
    public required List<RivalWingsParticipantV1> Players { get; init; }
    public required List<RivalWingsTeamV1> Teams { get; init; }
    public RivalWingsTimelineV1? Timeline { get; init; }
}
internal sealed class RivalWingsParticipantV1 { public required UploadAliasV1 Alias { get; init; } public string? AccountId { get; init; } public string? ContentId { get; init; } public required string Job { get; init; } public uint ClassJobId { get; init; } public required string Team { get; init; } public int Alliance { get; init; } public required RivalWingsScoreboardV1 Scoreboard { get; init; } public Dictionary<string,long>? MechTimeMillis { get; init; } }
internal sealed class RivalWingsScoreboardV1 { public long Kills { get; init; } public long Deaths { get; init; } public long Assists { get; init; } public long DamageDealt { get; init; } public long DamageToOther { get; init; } public long DamageTaken { get; init; } public long HpRestored { get; init; } public long Ceruleum { get; init; } public long Special1 { get; init; } }
internal sealed class RivalWingsTeamV1 { public required string Team { get; init; } public int Placement { get; init; } public required Dictionary<string,long> StructureHp { get; init; } public Dictionary<string,long>? MechTimeMillis { get; init; } public Dictionary<string,long>? Supplies { get; init; } public long Mercenaries { get; init; } }
internal sealed class RivalWingsTimelineV1 { public List<RivalWingsStructureEventV1>? StructureHealth { get; init; } public List<RivalWingsMechEventV1>? MechCounts { get; init; } public List<RivalWingsSoaringEventV1>? Soaring { get; init; } public List<RivalWingsClaimEventV1>? Claims { get; init; } }
internal sealed class RivalWingsStructureEventV1 { public DateTime Timestamp { get; init; } public required string Team { get; init; } public required string Structure { get; init; } public long Health { get; init; } }
internal sealed class RivalWingsMechEventV1 { public DateTime Timestamp { get; init; } public required string Team { get; init; } public required string Mech { get; init; } public long Count { get; init; } }
internal sealed class RivalWingsSoaringEventV1 { public DateTime Timestamp { get; init; } public required string Team { get; init; } public int Alliance { get; init; } public long Stacks { get; init; } }
internal sealed class RivalWingsClaimEventV1 { public DateTime Timestamp { get; init; } public required string Team { get; init; } public required string Kind { get; init; } public string? Resource { get; init; } }
