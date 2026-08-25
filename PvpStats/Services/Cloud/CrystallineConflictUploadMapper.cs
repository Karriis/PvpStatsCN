using Lumina.Excel.Sheets;
using PvpStats.Types.Match;
using PvpStats.Types.Match.Timeline;
using PvpStats.Types.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PvpStats.Services.Cloud;

internal sealed class CrystallineConflictUploadMapper {
    private readonly Plugin _plugin;
    internal CrystallineConflictUploadMapper(Plugin plugin) => _plugin = plugin;

    internal UploadEnvelopeV1 Map(CrystallineConflictMatch match, CrystallineConflictMatchTimeline? timeline = null) {
        if(!match.IsCompleted || match.MatchStartTime == null || match.MatchEndTime == null || match.Arena == null || match.MatchWinner == null || match.PostMatch == null) throw new InvalidOperationException("Only completed Crystalline Conflict matches can be uploaded.");
        var worlds = Worlds();
        var players = match.Players.Select(player => MapPlayer(match, player, worlds)).ToList();
        var observedAt = Utc(match.MatchEndTime.Value);
        var observations = match.Players.Where(player => player.ContentId is > 0).Select(player => MapObservation(player, worlds, observedAt)).ToList();
        var pluginVersion = match.PluginVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var gameVersion = Required(match.GameVersion, "game version");
        return new UploadEnvelopeV1 {
            ExportedAt = DateTime.UtcNow,
            Client = new UploadClientV1 { PluginVersion = pluginVersion, GameVersion = gameVersion, BuildHash = CloudUploadProtocol.GetClientBuildHash(), DataCenter = Optional(match.DataCenter) },
            Matches = [],
            CrystallineConflictMatches = [new CrystallineConflictMatchV1 {
                SourceMatchId = match.Id.ToString(), SourceVersion = match.Version, IsCompleted = true, ValidationFlags = (ulong)match.Flags,
                DutyStartTime = Utc(match.DutyStartTime), MatchStartTime = Utc(match.MatchStartTime.Value), MatchEndTime = Utc(match.MatchEndTime.Value), DutyId = match.DutyId, TerritoryId = match.TerritoryId,
                Arena = Arena(match.Arena.Value), MatchType = match.MatchType.ToString().ToLowerInvariant(), Winner = Team(match.MatchWinner.Value), Overtime = match.IsOvertime,
                DataCenter = Optional(match.DataCenter), GameVersion = gameVersion, PluginVersion = pluginVersion, LocalPlayer = match.LocalPlayer == null ? null : Alias(match.LocalPlayer, worlds),
                Players = players, Teams = match.Teams.Select(team => new CrystallineConflictTeamV1 { Team = Team(team.Key), Placement = team.Key == match.MatchWinner ? 1 : 2, Progress = checked((long)Math.Round(team.Value.Progress)) }).ToList(),
                Timeline = Timeline(timeline, match.MatchStartTime.Value, match.MatchEndTime.Value, worlds),
            }],
            IdentityObservations = observations.Count == 0 ? null : observations,
        };
    }

    private CrystallineConflictParticipantV1 MapPlayer(CrystallineConflictMatch match, CrystallineConflictPlayer player, IReadOnlyDictionary<string,uint> worlds) {
        if(player.Job == null || player.ClassJobId is null or 0 || player.Team == null) throw new InvalidOperationException($"Player {player.Alias} is missing job or team data.");
        var row = match.PostMatch!.Teams[player.Team.Value].PlayerStats.FirstOrDefault(value => value.Player != null && value.Player.Equals(player.Alias)) ?? throw new InvalidOperationException($"Player {player.Alias} is missing a scoreboard.");
        return new CrystallineConflictParticipantV1 { Alias = Alias(player.Alias, worlds), AccountId = Id(player.AccountId), ContentId = Id(player.ContentId), Job = player.Job.Value.ToString(), ClassJobId = player.ClassJobId.Value, Team = Team(player.Team.Value), Scoreboard = new CrystallineConflictScoreboardV1 { Kills=row.Kills,Deaths=row.Deaths,Assists=row.Assists,DamageDealt=row.DamageDealt,DamageTaken=row.DamageTaken,HpRestored=row.HPRestored,TimeOnCrystalMillis=checked((long)row.TimeOnCrystal.TotalMilliseconds) } };
    }

    private IdentityObservationV1 MapObservation(CrystallineConflictPlayer player,IReadOnlyDictionary<string,uint> worlds,DateTime observedAt) { var linked=_plugin.PlayerLinksService.GetAllLinkedAliases(player.Alias).Where(value=>!value.Equals(player.Alias)).Distinct().Select(value=>Alias(value,worlds)).ToList();return new IdentityObservationV1{AccountId=Id(player.AccountId),ContentId=player.ContentId!.Value.ToString(),CurrentAlias=Alias(player.Alias,worlds),LinkedAliases=linked.Count==0?null:linked,Source=linked.Count==0?"pvp_result":"usedname",ObservedAt=observedAt}; }
    private static CrystallineConflictTimelineV1? Timeline(CrystallineConflictMatchTimeline? source,DateTime start,DateTime end,IReadOnlyDictionary<string,uint> worlds) { if(source==null)return null;var min=Utc(start).AddMinutes(-1);var max=Utc(end).AddMinutes(1);bool In(DateTime value)=>Utc(value)>=min&&Utc(value)<=max;var progress=(source.CrystalPosition??[]).Where(e=>In(e.Timestamp)).Select(e=>new CrystallineConflictProgressEventV1{Timestamp=Utc(e.Timestamp),Points=e.Points}).Concat((source.TeamProgress??[]).SelectMany(team=>team.Value.Where(e=>In(e.Timestamp)).Select(e=>new CrystallineConflictProgressEventV1{Timestamp=Utc(e.Timestamp),Team=Team(team.Key),Points=e.Points}))).OrderBy(e=>e.Timestamp).ToList();var kills=(source.Kills??[]).Where(e=>In(e.Timestamp)).Select(e=>new CrystallineConflictKnockoutEventV1{Timestamp=Utc(e.Timestamp),Victim=Alias(e.Victim,worlds),Killer=e.CreditedKiller==null?null:Alias(e.CreditedKiller,worlds)}).ToList();var lbs=(source.LimitBreakCasts??[]).Where(e=>In(e.Timestamp)).Select(e=>new CrystallineConflictLimitBreakEventV1{Timestamp=Utc(e.Timestamp),Actor=Alias(e.Actor,worlds),ActionId=e.ActionId,Phase="cast"}).Concat((source.LimitBreakEffects??[]).Where(e=>In(e.Timestamp)).Select(e=>new CrystallineConflictLimitBreakEventV1{Timestamp=Utc(e.Timestamp),Actor=Alias(e.Actor,worlds),ActionId=e.ActionId,Phase="effect"})).OrderBy(e=>e.Timestamp).ToList();if(progress.Count==0&&kills.Count==0&&lbs.Count==0)return null;return new(){CrystalProgress=progress.Count==0?null:progress,Knockouts=kills.Count==0?null:kills,LimitBreaks=lbs.Count==0?null:lbs}; }
    private Dictionary<string,uint> Worlds()=>_plugin.DataManager.GetExcelSheet<World>().GroupBy(w=>w.Name.ToString(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().RowId,StringComparer.OrdinalIgnoreCase);
    private static UploadAliasV1 Alias(PlayerAlias value,IReadOnlyDictionary<string,uint> worlds){if(!worlds.TryGetValue(value.HomeWorld,out var id)||id==0)throw new InvalidOperationException($"Unable to resolve home world ID for {value}.");return new(){Name=Required(value.Name,"player name"),HomeWorld=Required(value.HomeWorld,"home world"),HomeWorldId=id};}
    private static string Team(CrystallineConflictTeamName value)=>value switch{CrystallineConflictTeamName.Astra=>"astra",CrystallineConflictTeamName.Umbra=>"umbra",_=>throw new InvalidOperationException("Unsupported CC team")};
    private static string Arena(CrystallineConflictMap value)=>value switch{CrystallineConflictMap.Palaistra=>"palaistra",CrystallineConflictMap.VolcanicHeart=>"volcanic_heart",CrystallineConflictMap.CloudNine=>"cloud_nine",CrystallineConflictMap.ClockworkCastleTown=>"clockwork_castletown",CrystallineConflictMap.RedSands=>"red_sands",CrystallineConflictMap.BaysideBattleground=>"bayside_battleground",CrystallineConflictMap.ArcheiaHarmonias=>"archeia_harmonias",_=>throw new InvalidOperationException("Unsupported CC arena")};
    private static string? Id(ulong? value)=>value is >0?value.Value.ToString():null;private static string Required(string? value,string field)=>!string.IsNullOrWhiteSpace(value)?value:throw new InvalidOperationException($"Missing {field}.");private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value;private static DateTime Utc(DateTime value)=>value.Kind==DateTimeKind.Utc?value:value.ToUniversalTime();
}
