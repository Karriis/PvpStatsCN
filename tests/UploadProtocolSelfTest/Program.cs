using PvpStats.Services.Cloud;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

var secret = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
var signed = CloudUploadProtocol.Sign(
    Encoding.UTF8.GetBytes("{\"ok\":true}"),
    new UploadCredentials("install_test", "account_test", "7", secret),
    "2.7.0.4",
    "mvid:test",
    DateTimeOffset.FromUnixTimeSeconds(1787630400),
    "53e7541e-f708-4dd1-9e9c-ef56e93ac65e",
    "upload_test");

AssertEqual("4062edaf750fb8074e7e83e0c9028c94e32468a8b6f1614774328ef045150f93", signed.BodySha256, "body SHA-256");
AssertEqual("NuaKYWrI03E71zWP3d7oxd-_iMas-Py_458qoDh4yaA", signed.Signature, "HMAC signature");

var identitySigned = CloudUploadProtocol.Sign(
    Encoding.UTF8.GetBytes("{\"ok\":true}"),
    new UploadCredentials("install_test", "account_test", "7", secret),
    "2.7.0.4",
    "mvid:test",
    DateTimeOffset.FromUnixTimeSeconds(1787630400),
    "53e7541e-f708-4dd1-9e9c-ef56e93ac65e",
    "upload_test",
    CloudUploadProtocol.IdentityUploadPath);
if(identitySigned.Signature == signed.Signature) {
    throw new InvalidOperationException("Identity and match upload paths must produce different HMAC signatures.");
}

var envelope = new UploadEnvelopeV1 {
    ExportedAt = DateTime.UnixEpoch,
    Client = new UploadClientV1 { PluginVersion = "2.7.0.4", GameVersion = "2026.08.25", BuildHash = "mvid:test" },
    Matches = [new FrontlineMatchV1 {
        SourceMatchId = "match_test",
        IsCompleted = true,
        DutyStartTime = DateTime.UnixEpoch,
        MatchStartTime = DateTime.UnixEpoch,
        MatchEndTime = DateTime.UnixEpoch.AddMinutes(10),
        Arena = "seal_rock",
        GameVersion = "2026.08.25",
        PluginVersion = "2.7.0.4",
        Players = [],
        Teams = [],
        Timeline = new FrontlineTimelineV1 {
            TeamPoints = [new TeamPointsEventV1 { Timestamp = DateTime.UnixEpoch.AddMinutes(1), Team = "maelstrom", Points = 100 }],
            SelfBattleHigh = [new BattleHighEventV1 { Timestamp = DateTime.UnixEpoch.AddMinutes(2), Level = 20 }],
        },
    }],
};
var compressed = CloudUploadProtocol.SerializeAndCompress(envelope);
using var input = new MemoryStream(compressed);
using var gzip = new GZipStream(input, CompressionMode.Decompress);
using var json = JsonDocument.Parse(gzip);
AssertEqual(1, json.RootElement.GetProperty("schemaVersion").GetInt32(), "schema version");
AssertEqual("2.7.0.4", json.RootElement.GetProperty("client").GetProperty("pluginVersion").GetString(), "camel-case JSON");
AssertEqual(100L, json.RootElement.GetProperty("matches")[0].GetProperty("timeline").GetProperty("teamPoints")[0].GetProperty("points").GetInt64(), "timeline serialization");

var identityEnvelope = new UploadEnvelopeV1 {
    ExportedAt = DateTime.UnixEpoch,
    Client = new UploadClientV1 { PluginVersion = "2.7.0.4", GameVersion = "2026.08.25", BuildHash = "mvid:test" },
    Matches = [],
    IdentityObservations = [new IdentityObservationV1 {
        ContentId = "12345",
        CurrentAlias = new UploadAliasV1 { Name = "Test Player", HomeWorld = "Chocobo", HomeWorldId = 33 },
        Source = "frontline_match",
        ObservedAt = DateTime.UnixEpoch,
    }],
};
using var identityInput = new MemoryStream(CloudUploadProtocol.SerializeAndCompress(identityEnvelope));
using var identityGzip = new GZipStream(identityInput, CompressionMode.Decompress);
using var identityJson = JsonDocument.Parse(identityGzip);
AssertEqual(0, identityJson.RootElement.GetProperty("matches").GetArrayLength(), "identity-only match count");
AssertEqual(1, identityJson.RootElement.GetProperty("identityObservations").GetArrayLength(), "identity observation count");

var idempotencyKey = CloudUploadProtocol.CreateIdempotencyKey("install_test", "match_test");
AssertEqual(idempotencyKey, CloudUploadProtocol.CreateIdempotencyKey("install_test", "match_test"), "deterministic idempotency key");

var protectedSecret = CloudCredentialProtector.Protect(secret);
if(protectedSecret.Contains(Convert.ToBase64String(secret), StringComparison.Ordinal)) {
    throw new InvalidOperationException("DPAPI output contains the plaintext secret.");
}
if(!secret.SequenceEqual(CloudCredentialProtector.Unprotect(protectedSecret))) {
    throw new InvalidOperationException("DPAPI round trip failed.");
}

Console.WriteLine("Upload protocol self-test passed.");

static void AssertEqual<T>(T expected, T actual, string name) {
    if(!EqualityComparer<T>.Default.Equals(expected, actual)) {
        throw new InvalidOperationException($"Unexpected {name}: expected {expected}, got {actual}.");
    }
}
