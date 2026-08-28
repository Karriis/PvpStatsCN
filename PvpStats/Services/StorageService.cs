using LiteDB;
using PvpStats.Types.Match;
using PvpStats.Types.Match.Timeline;
using PvpStats.Types.Player;
using PvpStats.Services.Cloud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PvpStats.Services;
internal class StorageService {
    private const string CCTable = "ccmatch";
    private const string FLTable = "flmatch";
    private const string RWTable = "rwmatch";
    private const string CCTimelineTable = "cctimeline";
    private const string FLTimelineTable = "fltimeline";
    private const string RWTimelineTable = "rwtimeline";
    private const string AutoPlayerLinksTable = "playerlinks_auto";
    private const string ManualPlayerLinksTable = "playerlinks_manual";
    private const string CloudUploadsTable = "cloud_uploads";
    private const string CloudCharacterApprovalsTable = "cloud_character_approvals";
    private const string PlayerIdentityTable = "playeridentity";
    private const string PlayerAliasObservationTable = "playeraliasobservation";
    private const string IdentitySyncStateTable = "identitysyncstate";

    private Plugin _plugin;
    private SemaphoreSlim _dbLock = new SemaphoreSlim(1, 1);
    private LiteDatabase Database { get; init; }

    internal StorageService(Plugin plugin, string path) {
        _plugin = plugin;
        Database = new LiteDatabase(path);

        //if(Database.UserVersion <= 0) {
        //    //foreach(var x in GetCCMatches().Find("Teams")) {
        //    //    foreach(var y in x.Teams) {
        //    //        foreach(var z in y.Value.Players) {
        //    //        }
        //    //    }
        //    //}

        //    var x = Database.GetCollection(CCTable);
        //    foreach(var doc in x.FindAll()) {
        //        foreach(var team in doc["Teams"].AsDocument) {
        //        }
        //    }

        //    //Database.UserVersion = 1;
        //}

        //set mapper properties
        BsonMapper.Global.EmptyStringToNull = false;
        BsonMapper.Global.RegisterType<DateTime>(
            serialize: dt => new BsonValue(dt.ToUniversalTime()),
            deserialize: v => v.AsDateTime.ToUniversalTime()
        );

        //BsonMapper.Global.RegisterType(
        //    serialize: key => key.FullName,
        //    deserialize: bson => (PlayerAlias)bson.AsString
        //);

        //create indices
        var ccMatchCollection = GetCCMatches();
        ccMatchCollection.EnsureIndex(m => m.IsCompleted);
        ccMatchCollection.EnsureIndex(m => m.IsDeleted);
        ccMatchCollection.EnsureIndex(m => m.DutyStartTime);
        ccMatchCollection.EnsureIndex(m => m.MatchType);
        ccMatchCollection.EnsureIndex(m => m.Arena);
        ccMatchCollection.EnsureIndex(m => m.IsBookmarked);

        var flMatchCollection = GetFLMatches();
        flMatchCollection.EnsureIndex(m => m.DutyStartTime);

        var rwMatchCollection = GetRWMatches();
        rwMatchCollection.EnsureIndex(m => m.DutyStartTime);

        GetPlayerIdentities().EnsureIndex(identity => identity.LastObservedAt);
        GetPlayerAliasObservations().EnsureIndex(observation => observation.ContentId);
        GetIdentitySyncStates().EnsureIndex(state => state.Status);
        GetCloudCharacterApprovals().EnsureIndex(character => character.Status);
    }

    public void Dispose() {
        Database.Dispose();
    }
    internal ILiteCollection<CrystallineConflictMatch> GetCCMatches() {
        return Database.GetCollection<CrystallineConflictMatch>(CCTable);
    }

    internal async Task AddCCMatch(CrystallineConflictMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetCCMatches().Insert(match));
    }

    internal async Task AddCCMatches(IEnumerable<CrystallineConflictMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetCCMatches().Insert(matches.Where(m => m.Id != null)));
    }

    internal async Task UpdateCCMatch(CrystallineConflictMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetCCMatches().Update(match));
    }

    internal async Task UpdateCCMatches(IEnumerable<CrystallineConflictMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetCCMatches().Update(matches.Where(m => m.Id != null)));
    }

    internal ILiteCollection<FrontlineMatch> GetFLMatches() {
        return Database.GetCollection<FrontlineMatch>(FLTable);
    }

    internal async Task AddFLMatch(FrontlineMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetFLMatches().Insert(match));
    }

    internal async Task AddFLMatches(IEnumerable<FrontlineMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetFLMatches().Insert(matches.Where(m => m.Id != null)));
    }

    internal async Task UpdateFLMatch(FrontlineMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetFLMatches().Update(match));
    }

    internal async Task UpdateFLMatches(IEnumerable<FrontlineMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetFLMatches().Update(matches.Where(m => m.Id != null)));
    }

    internal ILiteCollection<RivalWingsMatch> GetRWMatches() {
        return Database.GetCollection<RivalWingsMatch>(RWTable);
    }

    internal async Task AddRWMatch(RivalWingsMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetRWMatches().Insert(match));
    }

    internal async Task AddRWMatches(IEnumerable<RivalWingsMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetRWMatches().Insert(matches.Where(m => m.Id != null)));
    }

    internal async Task UpdateRWMatch(RivalWingsMatch match) {
        LogUpdate(match.Id.ToString());
        await WriteToDatabase(() => GetRWMatches().Update(match));
    }

    internal ILiteCollection<CrystallineConflictMatchTimeline> GetCCTimelines() {
        return Database.GetCollection<CrystallineConflictMatchTimeline>(CCTimelineTable);
    }

    internal async Task AddCCTimeline(CrystallineConflictMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetCCTimelines().Insert(timeline));
    }

    internal async Task UpdateCCTimeline(CrystallineConflictMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetCCTimelines().Update(timeline));
    }

    internal ILiteCollection<FrontlineMatchTimeline> GetFLTimelines() {
        return Database.GetCollection<FrontlineMatchTimeline>(FLTimelineTable);
    }

    internal async Task AddFLTimeline(FrontlineMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetFLTimelines().Insert(timeline));
    }

    internal async Task UpdateFLTimeline(FrontlineMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetFLTimelines().Update(timeline));
    }

    internal ILiteCollection<RivalWingsMatchTimeline> GetRWTimelines() {
        return Database.GetCollection<RivalWingsMatchTimeline>(RWTimelineTable);
    }

    internal async Task AddRWTimeline(RivalWingsMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetRWTimelines().Insert(timeline));
    }

    internal async Task UpdateRWTimeline(RivalWingsMatchTimeline timeline) {
        LogUpdate(timeline.Id.ToString());
        await WriteToDatabase(() => GetRWTimelines().Update(timeline));
    }

    internal async Task UpdateRWMatches(IEnumerable<RivalWingsMatch> matches) {
        LogUpdate(null, matches.Count());
        await WriteToDatabase(() => GetRWMatches().Update(matches.Where(m => m.Id != null)));
    }

    internal ILiteCollection<PlayerAliasLink> GetAutoLinks() {
        return Database.GetCollection<PlayerAliasLink>(AutoPlayerLinksTable);
    }

    internal async Task SetAutoLinks(IEnumerable<PlayerAliasLink> links) {
        LogUpdate(null, links.Count());
        _plugin.Storage.GetAutoLinks().DeleteAll();
        await WriteToDatabase(() => GetAutoLinks().Insert(links.Where(x => x.Id != null)));
    }

    internal ILiteCollection<PlayerAliasLink> GetManualLinks() {
        return Database.GetCollection<PlayerAliasLink>(ManualPlayerLinksTable);
    }

    internal async Task SetManualLinks(IEnumerable<PlayerAliasLink> links) {
        LogUpdate(null, links.Count());
        //kind of hacky
        GetManualLinks().DeleteAll();
        await WriteToDatabase(() => GetManualLinks().Insert(links.Where(x => x.Id != null)));
    }

    internal ILiteCollection<CloudUploadRecord> GetCloudUploads() {
        return Database.GetCollection<CloudUploadRecord>(CloudUploadsTable);
    }

    internal ILiteCollection<CloudCharacterApprovalRecord> GetCloudCharacterApprovals() {
        return Database.GetCollection<CloudCharacterApprovalRecord>(CloudCharacterApprovalsTable);
    }

    internal async Task UpsertCloudUpload(CloudUploadRecord record) {
        await WriteToDatabase(() => GetCloudUploads().Upsert(record));
    }

    internal async Task<CloudCharacterApprovalRecord> ObserveCloudCharacter(string installationId, string key, string name, string world, string? contentId) {
        CloudCharacterApprovalRecord? result = null;
        await WriteToDatabase(() => {
            var collection = GetCloudCharacterApprovals();
            var now = DateTime.UtcNow;
            result = collection.FindOne(character =>
                character.InstallationId == installationId &&
                (character.Id == key || (contentId != null && character.ContentId == contentId)));
            if(result == null) {
                var hasPrimary = collection.Exists(character => character.InstallationId == installationId && character.IsPrimary);
                result = new CloudCharacterApprovalRecord {
                    Id = key,
                    InstallationId = installationId,
                    Name = name,
                    World = world,
                    ContentId = contentId,
                    IsPrimary = !hasPrimary,
                    Status = CloudCharacterApprovalStatus.Pending,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                };
            } else {
                result.Name = name;
                result.World = world;
                result.ContentId ??= contentId;
                result.LastSeenAt = now;
            }
            collection.Upsert(result);
            return result;
        });
        return result!;
    }

    internal async Task<bool> ApproveCloudCharacter(string key) {
        var approved = false;
        await WriteToDatabase(() => {
            var collection = GetCloudCharacterApprovals();
            var character = collection.FindById(key);
            if(character == null) return false;
            character.Status = CloudCharacterApprovalStatus.Approved;
            character.ApprovedAt = DateTime.UtcNow;
            character.LastSeenAt = DateTime.UtcNow;
            approved = collection.Update(character);
            return approved;
        });
        return approved;
    }

    internal ILiteCollection<PlayerIdentityRecord> GetPlayerIdentities() => Database.GetCollection<PlayerIdentityRecord>(PlayerIdentityTable);
    internal ILiteCollection<PlayerAliasObservationRecord> GetPlayerAliasObservations() => Database.GetCollection<PlayerAliasObservationRecord>(PlayerAliasObservationTable);
    internal ILiteCollection<IdentitySyncStateRecord> GetIdentitySyncStates() => Database.GetCollection<IdentitySyncStateRecord>(IdentitySyncStateTable);

    internal async Task<List<string>> ObserveIdentities(IEnumerable<IdentityObservationV1> observations, string gameVersion, string pluginVersion) {
        var values = observations.ToList();
        var observationIds = new List<string>(values.Count);
        await WriteToDatabase(() => {
            foreach(var value in values) {
                var observedAt = value.ObservedAt.ToUniversalTime();
                var identity = GetPlayerIdentities().FindById(value.ContentId);
                if(identity == null) {
                    identity = new PlayerIdentityRecord {
                        Id = value.ContentId,
                        AccountId = value.AccountId,
                        CurrentName = value.CurrentAlias.Name,
                        CurrentWorld = value.CurrentAlias.HomeWorld,
                        CurrentWorldId = value.CurrentAlias.HomeWorldId,
                        Sources = [value.Source],
                        FirstObservedAt = observedAt,
                        LastObservedAt = observedAt,
                        GameVersion = gameVersion,
                        PluginVersion = pluginVersion,
                    };
                } else {
                    identity.AccountId ??= value.AccountId;
                    identity.CurrentName = value.CurrentAlias.Name;
                    identity.CurrentWorld = value.CurrentAlias.HomeWorld;
                    identity.CurrentWorldId = value.CurrentAlias.HomeWorldId;
                    identity.Sources.Add(value.Source);
                    if(observedAt < identity.FirstObservedAt) identity.FirstObservedAt = observedAt;
                    if(observedAt > identity.LastObservedAt) identity.LastObservedAt = observedAt;
                    identity.GameVersion = gameVersion;
                    identity.PluginVersion = pluginVersion;
                }
                GetPlayerIdentities().Upsert(identity);

                var observation = PlayerAliasObservationRecord.FromPayload(value);
                observationIds.Add(observation.Id);
                if(GetPlayerAliasObservations().FindById(observation.Id) == null) {
                    GetPlayerAliasObservations().Insert(observation);
                }
                if(GetIdentitySyncStates().FindById(observation.Id) == null) {
                    GetIdentitySyncStates().Insert(new IdentitySyncStateRecord {
                        Id = observation.Id,
                        Status = CloudUploadStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }
            return values.Count;
        });
        return observationIds;
    }

    internal List<(string Id, IdentityObservationV1 Observation)> GetPendingIdentityObservations(int limit) {
        var states = GetIdentitySyncStates().Query()
            .Where(state => state.Status != CloudUploadStatus.Uploaded)
            .OrderBy(state => state.CreatedAt)
            .Limit(limit)
            .ToList();
        return states.Select(state => (state.Id, GetPlayerAliasObservations().FindById(state.Id)))
            .Where(item => item.Item2 != null)
            .Select(item => (item.Id, item.Item2!.ToPayload()))
            .ToList();
    }

    internal async Task MarkIdentitySyncAttempt(IEnumerable<string> ids) {
        var values = ids.ToList();
        await WriteToDatabase(() => {
            foreach(var id in values) {
                var state = GetIdentitySyncStates().FindById(id);
                if(state == null) continue;
                state.AttemptCount++;
                state.LastAttemptAt = DateTime.UtcNow;
                GetIdentitySyncStates().Update(state);
            }
            return values.Count;
        });
    }

    internal async Task MarkIdentitySyncResult(IEnumerable<string> ids, bool uploaded, string? error) {
        var values = ids.ToList();
        await WriteToDatabase(() => {
            foreach(var id in values) {
                var state = GetIdentitySyncStates().FindById(id);
                if(state == null) continue;
                state.Status = uploaded ? CloudUploadStatus.Uploaded : CloudUploadStatus.Failed;
                state.UploadedAt = uploaded ? DateTime.UtcNow : null;
                state.LastError = error is { Length: > 500 } ? error[..500] : error;
                GetIdentitySyncStates().Update(state);
            }
            return values.Count;
        });
    }

    private void LogUpdate(string? id = null, int count = 0) {
        var callingMethod = new StackFrame(2, true).GetMethod();
        var writeMethod = new StackFrame(1, true).GetMethod();

        _plugin.Log.Verbose(string.Format("Invoking {0,-25} {2,-30}{3,-30} Caller: {1,-70}",
            writeMethod?.Name, $"{callingMethod?.DeclaringType?.ToString() ?? ""}.{callingMethod?.Name ?? ""}", id != null ? $"ID: {id}" : "", count != 0 ? $"Count: {count}" : ""));
    }

    private async Task WriteToDatabase(Func<object> action) {
        try {
            await _dbLock.WaitAsync();
            action.Invoke();
        } finally {
            _dbLock.Release();
        }
    }
}
