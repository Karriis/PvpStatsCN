using Lumina.Excel.Sheets;
using PvpStats.Types.Match;
using PvpStats.Types.Match.Timeline;
using PvpStats.Types.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PvpStats.Services.Cloud;

internal sealed class FrontlineUploadMapper {
    private readonly Plugin _plugin;

    internal FrontlineUploadMapper(Plugin plugin) {
        _plugin = plugin;
    }

    internal UploadEnvelopeV1 Map(FrontlineMatch match, FrontlineMatchTimeline? timeline = null) {
        if(!match.IsCompleted || match.MatchStartTime == null || match.MatchEndTime == null || match.Arena == null) {
            throw new InvalidOperationException("Only completed Frontline matches can be uploaded.");
        }

        var worlds = _plugin.DataManager.GetExcelSheet<World>()
            .GroupBy(world => world.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().RowId, StringComparer.OrdinalIgnoreCase);
        var pluginVersion = match.PluginVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var gameVersion = RequireText(match.GameVersion, "game version");
        var players = match.Players.Select(player => MapPlayer(match, player, worlds)).ToList();
        var observedAt = AsUtc(match.MatchEndTime.Value);
        var identityObservations = match.Players
            .Where(player => player.ContentId is > 0)
            .Select(player => {
                var linkedAliases = _plugin.PlayerLinksService.GetAllLinkedAliases(player.Name)
                    .Where(alias => !alias.Equals(player.Name))
                    .Distinct()
                    .Select(alias => MapAlias(alias, worlds))
                    .ToList();
                return new IdentityObservationV1 {
                    AccountId = FormatOptionalId(player.AccountId),
                    ContentId = player.ContentId!.Value.ToString(),
                    CurrentAlias = MapAlias(player.Name, worlds),
                    LinkedAliases = linkedAliases.Count == 0 ? null : linkedAliases,
                    Source = linkedAliases.Count == 0 ? "pvp_result" : "usedname",
                    ObservedAt = observedAt,
                };
            })
            .ToList();

        return new UploadEnvelopeV1 {
            ExportedAt = DateTime.UtcNow,
            Client = new UploadClientV1 {
                PluginVersion = pluginVersion,
                GameVersion = gameVersion,
                BuildHash = CloudUploadProtocol.GetClientBuildHash(),
                DataCenter = NullIfWhiteSpace(match.DataCenter),
            },
            Matches = [new FrontlineMatchV1 {
                SourceMatchId = match.Id.ToString(),
                SourceVersion = match.Version,
                IsCompleted = true,
                ValidationFlags = (ulong)match.Flags,
                DutyStartTime = AsUtc(match.DutyStartTime),
                MatchStartTime = AsUtc(match.MatchStartTime.Value),
                MatchEndTime = AsUtc(match.MatchEndTime.Value),
                DutyId = match.DutyId,
                TerritoryId = match.TerritoryId,
                Arena = MapArena(match.Arena.Value),
                DataCenter = NullIfWhiteSpace(match.DataCenter),
                GameVersion = gameVersion,
                PluginVersion = pluginVersion,
                // The result packet can contain empty rows for players who
                // disconnected before the scoreboard was produced.
                PlayerCount = players.Count,
                LocalPlayer = match.LocalPlayer == null ? null : MapAlias(match.LocalPlayer, worlds),
                Players = players,
                Teams = match.Teams.Select(team => new FrontlineTeamV1 {
                    Team = MapTeam(team.Key),
                    Placement = FrontlineUploadConventions.ToApiPlacement(team.Value.Placement),
                    TotalPoints = team.Value.TotalPoints,
                    KillPoints = team.Value.KillPoints,
                    DeathPointLosses = team.Value.DeathPointLosses,
                    OccupationPoints = team.Value.OccupationPoints,
                    TargetablePoints = team.Value.TargetablePoints,
                    DronePoints = team.Value.DronePoints,
                }).ToList(),
                Timeline = MapTimeline(timeline, match.MatchStartTime.Value, match.MatchEndTime.Value),
            }],
            IdentityObservations = identityObservations.Count == 0 ? null : identityObservations,
        };
    }

    private static FrontlineTimelineV1? MapTimeline(FrontlineMatchTimeline? timeline, DateTime matchStartTime, DateTime matchEndTime) {
        if(timeline == null) {
            return null;
        }

        var minimum = AsUtc(matchStartTime).AddMinutes(-1);
        var maximum = AsUtc(matchEndTime).AddMinutes(1);
        var teamPoints = timeline.TeamPoints?
            .SelectMany(team => team.Value.Select(point => new TeamPointsEventV1 {
                Timestamp = AsUtc(point.Timestamp),
                Team = MapTeam(team.Key),
                Points = point.Points,
            }))
            .Where(point => point.Timestamp >= minimum && point.Timestamp <= maximum)
            .OrderBy(point => point.Timestamp)
            .ToList();
        var selfBattleHigh = timeline.SelfBattleHigh?
            .Select(point => new BattleHighEventV1 {
                Timestamp = AsUtc(point.Timestamp),
                Level = point.Count,
            })
            .Where(point => point.Timestamp >= minimum && point.Timestamp <= maximum)
            .OrderBy(point => point.Timestamp)
            .ToList();

        if((teamPoints?.Count ?? 0) == 0 && (selfBattleHigh?.Count ?? 0) == 0) {
            return null;
        }
        return new FrontlineTimelineV1 {
            TeamPoints = teamPoints?.Count > 0 ? teamPoints : null,
            SelfBattleHigh = selfBattleHigh?.Count > 0 ? selfBattleHigh : null,
        };
    }

    private static FrontlineParticipantV1 MapPlayer(FrontlineMatch match, FrontlinePlayer player, IReadOnlyDictionary<string, uint> worlds) {
        if(player.Job == null || player.ClassJobId is null or 0) {
            throw new InvalidOperationException($"Player {player.Name} is missing job data.");
        }
        if(!match.PlayerScoreboards.TryGetValue(player.Name, out var scoreboard)) {
            throw new InvalidOperationException($"Player {player.Name} is missing a scoreboard.");
        }

        int? maxBattleHigh = null;
        if(match.MaxBattleHigh?.TryGetValue(player.Name, out var observedBattleHigh) == true) {
            maxBattleHigh = observedBattleHigh;
        }

        return new FrontlineParticipantV1 {
            Alias = MapAlias(player.Name, worlds),
            AccountId = FormatOptionalId(player.AccountId),
            ContentId = FormatOptionalId(player.ContentId),
            Job = player.Job.Value.ToString(),
            ClassJobId = player.ClassJobId.Value,
            Team = MapTeam(player.Team),
            Alliance = player.Alliance,
            Scoreboard = new FrontlineScoreboardV1 {
                Kills = scoreboard.Kills,
                Deaths = scoreboard.Deaths,
                Assists = scoreboard.Assists,
                DamageDealt = scoreboard.DamageDealt,
                DamageToOther = scoreboard.DamageToOther,
                DamageTaken = scoreboard.DamageTaken,
                HpRestored = scoreboard.HPRestored,
                Occupations = scoreboard.Occupations,
                Special1 = scoreboard.Special1,
                ClaimTimeMillis = checked((long)scoreboard.ClaimTime.TotalMilliseconds),
            },
            MaxBattleHigh = maxBattleHigh,
        };
    }

    private static UploadAliasV1 MapAlias(PlayerAlias alias, IReadOnlyDictionary<string, uint> worlds) {
        if(!worlds.TryGetValue(alias.HomeWorld, out var worldId) || worldId == 0) {
            throw new InvalidOperationException($"Unable to resolve home world ID for {alias}.");
        }
        return new UploadAliasV1 {
            Name = RequireText(alias.Name, "player name"),
            HomeWorld = RequireText(alias.HomeWorld, "home world"),
            HomeWorldId = worldId,
        };
    }

    private static string MapArena(FrontlineMap arena) => arena switch {
        FrontlineMap.BorderlandRuins => "borderland_ruins",
        FrontlineMap.SealRock => "seal_rock",
        FrontlineMap.FieldsOfGlory => "fields_of_glory",
        FrontlineMap.OnsalHakair => "onsal_hakair",
        FrontlineMap.WorqorChirteh => "worqor_chirteh",
        _ => throw new InvalidOperationException($"Unsupported Frontline arena: {arena}"),
    };

    private static string MapTeam(FrontlineTeamName team) => team switch {
        FrontlineTeamName.Maelstrom => "maelstrom",
        FrontlineTeamName.Adders => "adders",
        FrontlineTeamName.Flames => "flames",
        _ => throw new InvalidOperationException($"Unsupported Frontline team: {team}"),
    };

    private static string? FormatOptionalId(ulong? value) => value is > 0 ? value.Value.ToString() : null;
    private static string RequireText(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"Missing {field}.");
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
