using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PvpStats.Services.Cloud;

internal sealed class PlayerIdentityRecord {
    public required string Id { get; init; }
    public string? AccountId { get; set; }
    public required string CurrentName { get; set; }
    public required string CurrentWorld { get; set; }
    public uint CurrentWorldId { get; set; }
    public HashSet<string> Sources { get; set; } = [];
    public DateTime FirstObservedAt { get; set; }
    public DateTime LastObservedAt { get; set; }
    public required string GameVersion { get; set; }
    public required string PluginVersion { get; set; }
    public int SchemaVersion { get; set; } = CloudUploadProtocol.SchemaVersion;
}

internal sealed class PlayerAliasObservationRecord {
    public required string Id { get; init; }
    public string? AccountId { get; init; }
    public required string ContentId { get; init; }
    public required UploadAliasV1 CurrentAlias { get; init; }
    public List<UploadAliasV1>? LinkedAliases { get; init; }
    public required string Source { get; init; }
    public DateTime ObservedAt { get; init; }

    internal IdentityObservationV1 ToPayload() => new() {
        AccountId = AccountId,
        ContentId = ContentId,
        CurrentAlias = CurrentAlias,
        LinkedAliases = LinkedAliases,
        Source = Source,
        ObservedAt = ObservedAt,
    };

    internal static PlayerAliasObservationRecord FromPayload(IdentityObservationV1 value) => new() {
        Id = CreateId(value),
        AccountId = value.AccountId,
        ContentId = value.ContentId,
        CurrentAlias = value.CurrentAlias,
        LinkedAliases = value.LinkedAliases,
        Source = value.Source,
        ObservedAt = value.ObservedAt.ToUniversalTime(),
    };

    private static string CreateId(IdentityObservationV1 value) {
        var linked = value.LinkedAliases?
            .OrderBy(alias => alias.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(alias => alias.HomeWorldId)
            .Select(alias => $"{alias.Name.Trim().ToLowerInvariant()}@{alias.HomeWorldId}") ?? [];
        var canonical = string.Join("\n", new[] {
            value.ContentId,
            value.CurrentAlias.Name.Trim().ToLowerInvariant(),
            value.CurrentAlias.HomeWorldId.ToString(),
            value.Source,
            string.Join("|", linked),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

internal sealed class IdentitySyncStateRecord {
    public required string Id { get; init; }
    public CloudUploadStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? LastError { get; set; }
}
