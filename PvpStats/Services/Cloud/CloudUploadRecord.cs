using System;

namespace PvpStats.Services.Cloud;

internal enum CloudUploadStatus {
    Pending,
    Failed,
    Uploaded,
    WaitingForCharacterApproval,
}

internal sealed class CloudUploadRecord {
    public required string Id { get; init; }
    public CloudUploadStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? LastError { get; set; }
    public string? CharacterKey { get; set; }
    public string? CharacterName { get; set; }
    public string? CharacterWorld { get; set; }
}

internal enum CloudCharacterApprovalStatus {
    Pending,
    Approved,
}

internal sealed class CloudCharacterApprovalRecord {
    public required string Id { get; init; }
    public required string InstallationId { get; init; }
    public required string Name { get; set; }
    public required string World { get; set; }
    public string? ContentId { get; set; }
    public bool IsPrimary { get; set; }
    public CloudCharacterApprovalStatus Status { get; set; }
    public DateTime FirstSeenAt { get; init; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
