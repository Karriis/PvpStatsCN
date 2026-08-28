using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PvpStats.Services.Cloud;

internal static class CloudUploadProtocol {
    internal const int SchemaVersion = 1;
    internal const string ApiBaseUrl = "https://pvplogs.karriis.com/";
    internal const string UploadPath = "/api/v1/plugin/uploads";
    internal const string IdentityUploadPath = "/api/v1/plugin/identities";
    internal const string OwnershipVerificationPath = "/api/v1/plugin/ownership-verifications";
    internal const string AccountProfilePath = "/api/v1/plugin/account-profile";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static byte[] SerializeAndCompress(UploadEnvelopeV1 envelope) {
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        using var output = new MemoryStream();
        using(var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true)) {
            gzip.Write(json);
        }
        return output.ToArray();
    }

    internal static string GetClientBuildHash() {
        var mvid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString("N");
        return $"mvid:{mvid}";
    }

    internal static SignedUploadRequest Sign(byte[] body, UploadCredentials credentials, string pluginVersion, string buildHash, DateTimeOffset now, string nonce, string idempotencyKey, string path = UploadPath) {
        RejectLineBreaks(credentials.InstallationId, credentials.AccountId, credentials.KeyVersion, pluginVersion, buildHash, nonce, idempotencyKey);
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var canonical = string.Join('\n',
            "PVPLOGS-HMAC-V1",
            "POST",
            path,
            credentials.InstallationId,
            credentials.AccountId,
            credentials.KeyVersion,
            timestamp,
            nonce,
            pluginVersion,
            SchemaVersion.ToString(),
            buildHash,
            idempotencyKey,
            bodyHash);
        var signatureBytes = HMACSHA256.HashData(credentials.Secret, Encoding.UTF8.GetBytes(canonical));

        return new SignedUploadRequest(timestamp, bodyHash, Base64Url(signatureBytes));
    }

    internal static string CreateIdempotencyKey(string installationId, string sourceMatchId) {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{installationId}\n{sourceMatchId}"));
        return $"upload_{Convert.ToHexStringLower(digest)}";
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void RejectLineBreaks(params string[] values) {
        foreach(var value in values) {
            if(string.IsNullOrWhiteSpace(value) || value.Contains('\r') || value.Contains('\n')) {
                throw new InvalidOperationException("Signed upload fields must be non-empty and cannot contain line breaks.");
            }
        }
    }
}

internal sealed record UploadCredentials(string InstallationId, string AccountId, string KeyVersion, byte[] Secret);
internal sealed record SignedUploadRequest(string Timestamp, string BodySha256, string Signature);
