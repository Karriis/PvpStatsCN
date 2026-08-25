using System;

namespace PvpStats.Services.Cloud;

internal enum CloudUploadStatus {
    Pending,
    Failed,
    Uploaded,
}

internal sealed class CloudUploadRecord {
    public required string Id { get; init; }
    public CloudUploadStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? LastError { get; set; }
}
