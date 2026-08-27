using PvpStats.Types.Match;
using PvpStats.Types.Match.Timeline;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PvpStats.Services.Cloud;

internal sealed class CloudUploadService : IDisposable {
    private readonly Plugin _plugin;
    private readonly FrontlineUploadMapper _mapper;
    private readonly CrystallineConflictUploadMapper _ccMapper;
    private readonly RivalWingsUploadMapper _rwMapper;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Channel<PendingUpload> _queue = Channel.CreateUnbounded<PendingUpload>(new UnboundedChannelOptions {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private readonly object _identityQueueLock = new();
    private readonly HashSet<string> _queuedIdentityIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedLoginContentIds = new(StringComparer.Ordinal);
    private bool _reportedNotReady;

    internal CloudUploadService(Plugin plugin) {
        _plugin = plugin;
        _mapper = new FrontlineUploadMapper(plugin);
        _ccMapper = new CrystallineConflictUploadMapper(plugin);
        _rwMapper = new RivalWingsUploadMapper(plugin);
        _worker = Task.Run(RunAsync);
    }

    internal async Task<bool> EnqueueAsync(FrontlineMatch match, FrontlineMatchTimeline? timeline = null) {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted) {
            return false;
        }

        try {
            if(!TryGetEndpoint(out _) || !TryGetCredentials(out var credentials)) {
                if(!_reportedNotReady) {
                    _plugin.Log.Warning("Cloud upload is enabled but its endpoint or credentials are not configured.");
                    _reportedNotReady = true;
                }
                return false;
            }

            var envelope = _mapper.Map(match, timeline);
            var sourceMatchId = envelope.Matches[0].SourceMatchId;
            return await EnqueueEnvelopeAsync(envelope, sourceMatchId, credentials);
        } catch(Exception ex) {
            _plugin.Log.Warning(ex, $"Frontline match {match.Id} was not queued for cloud upload.");
            return false;
        }
    }

    internal async Task<bool> EnqueueAsync(CrystallineConflictMatch match, CrystallineConflictMatchTimeline? timeline = null) {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted) return false;
        try {
            if(!TryGetEndpoint(out _) || !TryGetCredentials(out var credentials)) return false;
            var envelope = _ccMapper.Map(match, timeline);
            return await EnqueueEnvelopeAsync(envelope, envelope.CrystallineConflictMatches![0].SourceMatchId, credentials);
        } catch(Exception ex) { _plugin.Log.Warning(ex, $"Crystalline Conflict match {match.Id} was not queued for cloud upload."); return false; }
    }

    internal async Task<bool> EnqueueAsync(RivalWingsMatch match, RivalWingsMatchTimeline? timeline = null) {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted) return false;
        try {
            if(!TryGetEndpoint(out _) || !TryGetCredentials(out var credentials)) return false;
            var envelope = _rwMapper.Map(match, timeline);
            return await EnqueueEnvelopeAsync(envelope, envelope.RivalWingsMatches![0].SourceMatchId, credentials);
        } catch(Exception ex) { _plugin.Log.Warning(ex, $"Rival Wings match {match.Id} was not queued for cloud upload."); return false; }
    }

    private async Task<bool> EnqueueEnvelopeAsync(UploadEnvelopeV1 envelope, string sourceMatchId, UploadCredentials credentials) {
        var localCharacter = FindLocalCharacter(envelope);
        CloudCharacterApprovalRecord? characterApproval = null;
        if(localCharacter != null) {
            characterApproval = await _plugin.Storage.ObserveCloudCharacter(
                credentials.InstallationId,
                localCharacter.Key,
                localCharacter.Name,
                localCharacter.World,
                localCharacter.ContentId);
        }

        var existing = _plugin.Storage.GetCloudUploads().FindById(sourceMatchId);
        var record = existing ?? new CloudUploadRecord { Id = sourceMatchId, Status = CloudUploadStatus.Pending, CreatedAt = DateTime.UtcNow };
        if(localCharacter != null) {
            record.CharacterKey = localCharacter.Key;
            record.CharacterName = localCharacter.Name;
            record.CharacterWorld = localCharacter.World;
        }
        if(characterApproval?.Status == CloudCharacterApprovalStatus.Pending) {
            record.Status = CloudUploadStatus.WaitingForCharacterApproval;
            record.LastError = null;
            await _plugin.Storage.UpsertCloudUpload(record);
            _plugin.Log.Information($"Cloud upload for {sourceMatchId} is waiting for approval of character {localCharacter!.Name} {localCharacter.World}.");
            _ = _plugin.Framework.RunOnFrameworkThread(() => _plugin.WindowManager.OpenConfigWindow());
            return false;
        }

        var observations = envelope.IdentityObservations ?? [];
        if(observations.Count > 0) {
            await _plugin.Storage.ObserveIdentities(observations, envelope.Client.GameVersion, envelope.Client.PluginVersion);
            envelope.IdentityObservations = null;
            await EnqueueIdentityBacklogAsync(credentials);
        }
        if(existing?.Status == CloudUploadStatus.Uploaded) return true;
        record.Status = CloudUploadStatus.Pending; record.LastError = null;
        await _plugin.Storage.UpsertCloudUpload(record);
        var body = CloudUploadProtocol.SerializeAndCompress(envelope);
        var idempotencyKey = CloudUploadProtocol.CreateIdempotencyKey(credentials.InstallationId, sourceMatchId);
        await _queue.Writer.WriteAsync(new PendingUpload(body, envelope.Client.PluginVersion, envelope.Client.BuildHash, idempotencyKey, sourceMatchId, CloudUploadProtocol.UploadPath, null), _shutdown.Token);
        return true;
    }

    internal async Task EnqueueBacklogAsync() {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted) {
            return;
        }

        if(TryGetCredentials(out var credentials)) {
            await EnqueueIdentityBacklogAsync(credentials);
        }

        var pendingIds = _plugin.Storage.GetCloudUploads().Query()
            .Where(record => record.Status != CloudUploadStatus.Uploaded)
            .OrderBy(record => record.CreatedAt)
            .Limit(100)
            .ToList()
            .Select(record => record.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var match in _plugin.FLCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.FLCache.GetTimeline(match) as FrontlineMatchTimeline);
        }
        foreach(var match in _plugin.CCCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.CCCache.GetTimeline(match) as CrystallineConflictMatchTimeline);
        }
        foreach(var match in _plugin.RWCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.RWCache.GetTimeline(match) as RivalWingsMatchTimeline);
        }
    }

    internal IReadOnlyList<CloudCharacterApprovalRecord> GetCharacterApprovals() {
        return _plugin.Storage.GetCloudCharacterApprovals().FindAll()
            .Where(character => character.InstallationId == _plugin.Configuration.CloudUploadInstallationId)
            .OrderByDescending(character => character.IsPrimary)
            .ThenBy(character => character.FirstSeenAt)
            .ToList();
    }

    internal async Task<bool> ApproveCharacterAsync(string key) {
        if(!await _plugin.Storage.ApproveCloudCharacter(key)) return false;
        var approved = _plugin.Storage.GetCloudCharacterApprovals().FindById(key);
        if(approved != null && !string.IsNullOrWhiteSpace(approved.ContentId)) {
            await ClaimLocalCharacterAsync(approved);
        }
        await EnqueueCharacterBacklogAsync(key);
        return true;
    }

    private async Task EnqueueCharacterBacklogAsync(string key) {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted) return;
        var pendingIds = _plugin.Storage.GetCloudUploads().Find(record =>
                record.CharacterKey == key && record.Status != CloudUploadStatus.Uploaded)
            .Select(record => record.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var match in _plugin.FLCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.FLCache.GetTimeline(match) as FrontlineMatchTimeline);
        }
        foreach(var match in _plugin.CCCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.CCCache.GetTimeline(match) as CrystallineConflictMatchTimeline);
        }
        foreach(var match in _plugin.RWCache.Matches.Where(match => pendingIds.Contains(match.Id.ToString()))) {
            await EnqueueAsync(match, _plugin.RWCache.GetTimeline(match) as RivalWingsMatchTimeline);
        }
    }

    internal async Task ObserveCurrentCharacterAsync() {
        if(!_plugin.Configuration.CloudUploadEnabled || !_plugin.Configuration.CloudUploadConsentAccepted || !IsBound) return;
        var current = _plugin.GameState.CurrentPlayer;
        if(current == null || string.IsNullOrWhiteSpace(current.Name) || string.IsNullOrWhiteSpace(current.HomeWorld)) return;
        var contentId = _plugin.PlayerState.ContentId;
        var worldId = _plugin.PlayerState.HomeWorld.RowId;
        if(contentId == 0 || worldId == 0) return;
        var key = CreateCharacterKey(current.Name, current.HomeWorld);
        var approval = await _plugin.Storage.ObserveCloudCharacter(
            _plugin.Configuration.CloudUploadInstallationId,
            key,
            current.Name.Trim(),
            current.HomeWorld.Trim(),
            contentId.ToString());
        if(approval.Status == CloudCharacterApprovalStatus.Pending) {
            _plugin.Log.Information($"Character {approval.Name} {approval.World} is waiting for cloud upload approval.");
            _ = _plugin.Framework.RunOnFrameworkThread(() => _plugin.WindowManager.OpenConfigWindow());
            return;
        }
        await ClaimLocalCharacterAsync(approval, worldId);
    }

    private async Task ClaimLocalCharacterAsync(CloudCharacterApprovalRecord character, uint worldId = 0, CancellationToken cancellationToken = default) {
        if(!TryGetCredentials(out var credentials)
            || !TryGetApiEndpoint(CloudUploadProtocol.LocalCharacterClaimPath, out var endpoint)
            || string.IsNullOrWhiteSpace(character.ContentId)) return;
        if(worldId == 0 && string.Equals(_plugin.GameState.CurrentPlayer?.Name, character.Name, StringComparison.OrdinalIgnoreCase)) {
            worldId = _plugin.PlayerState.HomeWorld.RowId;
        }
        if(worldId == 0) return;
        lock(_claimedLoginContentIds) {
            if(!_claimedLoginContentIds.Add(character.ContentId)) return;
        }
        try {
            var body = JsonSerializer.SerializeToUtf8Bytes(new {
                contentId = character.ContentId,
                name = character.Name,
                world = character.World,
                worldId,
                observedAt = DateTime.UtcNow,
            });
            var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            var buildHash = CloudUploadProtocol.GetClientBuildHash();
            var nonce = Guid.NewGuid().ToString();
            var idempotencyKey = $"local_character_{Guid.NewGuid():N}";
            var signed = CloudUploadProtocol.Sign(body, credentials, pluginVersion, buildHash, DateTimeOffset.UtcNow, nonce, idempotencyKey, CloudUploadProtocol.LocalCharacterClaimPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            AddSignedHeaders(request, credentials, signed, nonce, pluginVersion, buildHash, idempotencyKey);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if(response.IsSuccessStatusCode) {
                _plugin.Log.Information($"Claimed logged-in character {character.Name} {character.World} for the bound website account.");
                return;
            }
            _plugin.Log.Warning($"Logged-in character claim was rejected with HTTP {(int)response.StatusCode}.");
        } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
        } catch(Exception ex) {
            _plugin.Log.Warning(ex, "Logged-in character claim failed.");
        }
        lock(_claimedLoginContentIds) { _claimedLoginContentIds.Remove(character.ContentId); }
    }

    private async Task EnqueueIdentityBacklogAsync(UploadCredentials credentials) {
        var pending = _plugin.Storage.GetPendingIdentityObservations(500);
        lock(_identityQueueLock) {
            pending = pending.Where(item => !_queuedIdentityIds.Contains(item.Id)).ToList();
            foreach(var item in pending) _queuedIdentityIds.Add(item.Id);
        }
        if(pending.Count == 0) return;

        var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var buildHash = CloudUploadProtocol.GetClientBuildHash();
        var ids = pending.Select(item => item.Id).ToList();
        var envelope = new UploadEnvelopeV1 {
            ExportedAt = DateTime.UtcNow,
            Client = new UploadClientV1 {
                PluginVersion = pluginVersion,
                GameVersion = GetCurrentGameVersion(),
                BuildHash = buildHash,
            },
            Matches = [],
            IdentityObservations = pending.Select(item => item.Observation).ToList(),
        };
        var body = CloudUploadProtocol.SerializeAndCompress(envelope);
        var idempotencyKey = CloudUploadProtocol.CreateIdempotencyKey(credentials.InstallationId, "identity:" + string.Join(',', ids));
        try {
            await _queue.Writer.WriteAsync(new PendingUpload(body, pluginVersion, buildHash, idempotencyKey, idempotencyKey, CloudUploadProtocol.IdentityUploadPath, ids), _shutdown.Token);
        } catch {
            RemoveQueuedIdentities(ids);
            throw;
        }
    }

    internal void SetCredentials(string installationId, string accountId, string keyVersion, byte[] secret) {
        _plugin.Configuration.CloudUploadInstallationId = installationId.Trim();
        _plugin.Configuration.CloudUploadAccountId = accountId.Trim();
        _plugin.Configuration.CloudUploadKeyVersion = keyVersion.Trim();
        _plugin.Configuration.CloudUploadProtectedSecret = CloudCredentialProtector.Protect(secret);
        _plugin.Configuration.Save();
        _reportedNotReady = false;
    }

    internal bool IsBound => TryGetCredentials(out _);

    internal async Task<CloudBindingResult> VerifyOwnershipAsync(string code, CancellationToken cancellationToken = default) {
        if(!TryGetCredentials(out var credentials) || !TryGetApiEndpoint(CloudUploadProtocol.OwnershipVerificationPath, out var endpoint)) {
            return new CloudBindingResult(false, "Bind the plugin to your website account first.");
        }
        code = code.Trim();
        if(string.IsNullOrWhiteSpace(code)) {
            return new CloudBindingResult(false, "Enter the ownership verification code generated by the website.");
        }
        try {
            var body = JsonSerializer.SerializeToUtf8Bytes(new { code });
            var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            var buildHash = CloudUploadProtocol.GetClientBuildHash();
            var nonce = Guid.NewGuid().ToString();
            var idempotencyKey = $"ownership_{Guid.NewGuid():N}";
            var signed = CloudUploadProtocol.Sign(body, credentials, pluginVersion, buildHash, DateTimeOffset.UtcNow, nonce, idempotencyKey, CloudUploadProtocol.OwnershipVerificationPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            AddSignedHeaders(request, credentials, signed, nonce, pluginVersion, buildHash, idempotencyKey);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if(response.IsSuccessStatusCode) {
                return new CloudBindingResult(true, "Character ownership verification succeeded. Refresh the website account page.");
            }
            return new CloudBindingResult(false, response.StatusCode == HttpStatusCode.UnprocessableEntity ? "The verification code is invalid, expired, or already used." : "The verification service rejected the request.");
        } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            return new CloudBindingResult(false, "Verification was cancelled.");
        } catch(Exception ex) {
            _plugin.Log.Warning(ex, "Character ownership verification failed.");
            return new CloudBindingResult(false, "Unable to reach the verification service. Try again later.");
        }
    }

    internal async Task<CloudBindingResult> BindAsync(string code, CancellationToken cancellationToken = default) {
        if(!TryGetApiEndpoint("/api/v1/plugin/bind", out var endpoint)) {
            return new CloudBindingResult(false, "Enter a valid HTTPS API address first.");
        }
        code = code.Trim();
        if(string.IsNullOrWhiteSpace(code)) {
            return new CloudBindingResult(false, "Enter the binding code generated by the website.");
        }

        try {
            var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            var requestBody = JsonSerializer.Serialize(new {
                code,
                pluginVersion,
                gameVersion = GetCurrentGameVersion(),
                clientBuildHash = CloudUploadProtocol.GetClientBuildHash(),
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
            };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if(!response.IsSuccessStatusCode) {
                _plugin.Log.Warning($"Plugin binding was rejected with HTTP {(int)response.StatusCode}.");
                return new CloudBindingResult(false, response.StatusCode == HttpStatusCode.UnprocessableEntity ? "The binding code is invalid, expired, or already used." : "The binding service rejected the request.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<CloudBindingResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if(result == null || string.IsNullOrWhiteSpace(result.InstallationId) || string.IsNullOrWhiteSpace(result.AccountId) || result.KeyVersion <= 0) {
                return new CloudBindingResult(false, "The server returned incomplete binding credentials.");
            }
            var secret = Convert.FromBase64String(result.Secret);
            if(secret.Length < 32) {
                return new CloudBindingResult(false, "The server returned an invalid binding secret.");
            }
            SetCredentials(result.InstallationId, result.AccountId, result.KeyVersion.ToString(), secret);
            await ObserveCurrentCharacterAsync();
            return new CloudBindingResult(true, "Plugin binding succeeded.");
        } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            return new CloudBindingResult(false, "Binding was cancelled.");
        } catch(Exception ex) {
            _plugin.Log.Warning(ex, "Plugin binding failed.");
            return new CloudBindingResult(false, "Unable to reach the binding service. Try again later.");
        }
    }

    internal void ClearCredentials() {
        _plugin.Configuration.CloudUploadEnabled = false;
        _plugin.Configuration.CloudUploadInstallationId = "";
        _plugin.Configuration.CloudUploadAccountId = "";
        _plugin.Configuration.CloudUploadKeyVersion = "";
        _plugin.Configuration.CloudUploadProtectedSecret = "";
        _plugin.Configuration.Save();
        _reportedNotReady = false;
    }

    private async Task RunAsync() {
        try {
            await foreach(var pending in _queue.Reader.ReadAllAsync(_shutdown.Token)) {
                await UploadWithRetryAsync(pending, _shutdown.Token);
            }
        } catch(OperationCanceledException) when(_shutdown.IsCancellationRequested) {
        } catch(Exception ex) {
            _plugin.Log.Error(ex, "Cloud upload worker stopped unexpectedly.");
        }
    }

    private async Task UploadWithRetryAsync(PendingUpload pending, CancellationToken cancellationToken) {
        int[] retryDelaysSeconds = [0, 2, 10];
        string? lastError = null;
        for(var attempt = 0; attempt < retryDelaysSeconds.Length; attempt++) {
            if(retryDelaysSeconds[attempt] > 0) {
                await Task.Delay(TimeSpan.FromSeconds(retryDelaysSeconds[attempt]), cancellationToken);
            }

            try {
                if(!TryGetApiEndpoint(pending.Path, out var endpoint) || !TryGetCredentials(out var credentials)) {
                    return;
                }
                await RecordAttemptAsync(pending);
                using var request = CreateRequest(endpoint, credentials, pending);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict) {
                    await RecordUploadedAsync(pending);
                    _plugin.Log.Information($"Cloud upload completed for {pending.RecordId}.");
                    return;
                }
                if((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests) {
                    lastError = $"HTTP {(int)response.StatusCode}";
                    await RecordFailedAsync(pending, lastError);
                    _plugin.Log.Warning($"Cloud upload rejected {pending.RecordId}: {lastError}.");
                    return;
                }
                lastError = $"HTTP {(int)response.StatusCode}";
                _plugin.Log.Warning($"Cloud upload attempt {attempt + 1} failed for {pending.RecordId}: HTTP {(int)response.StatusCode}.");
            } catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
                return;
            } catch(Exception ex) {
                lastError = $"{ex.GetType().Name}: {ex.Message}";
                _plugin.Log.Warning(ex, $"Cloud upload attempt {attempt + 1} failed for {pending.RecordId}.");
            }
        }
        await RecordFailedAsync(pending, lastError ?? "Upload attempts exhausted.");
    }

    private async Task RecordAttemptAsync(PendingUpload pending) {
        if(pending.IdentityObservationIds is { Count: > 0 }) {
            await _plugin.Storage.MarkIdentitySyncAttempt(pending.IdentityObservationIds);
            return;
        }
        var record = _plugin.Storage.GetCloudUploads().FindById(pending.RecordId);
        if(record == null) return;
        record.AttemptCount++;
        record.LastAttemptAt = DateTime.UtcNow;
        await _plugin.Storage.UpsertCloudUpload(record);
    }

    private async Task RecordUploadedAsync(PendingUpload pending) {
        if(pending.IdentityObservationIds is { Count: > 0 }) {
            await _plugin.Storage.MarkIdentitySyncResult(pending.IdentityObservationIds, true, null);
            RemoveQueuedIdentities(pending.IdentityObservationIds);
            return;
        }
        var record = _plugin.Storage.GetCloudUploads().FindById(pending.RecordId);
        if(record == null) return;
        record.Status = CloudUploadStatus.Uploaded;
        record.UploadedAt = DateTime.UtcNow;
        record.LastError = null;
        await _plugin.Storage.UpsertCloudUpload(record);
    }

    private async Task RecordFailedAsync(PendingUpload pending, string error) {
        if(pending.IdentityObservationIds is { Count: > 0 }) {
            await _plugin.Storage.MarkIdentitySyncResult(pending.IdentityObservationIds, false, error);
            RemoveQueuedIdentities(pending.IdentityObservationIds);
            return;
        }
        var record = _plugin.Storage.GetCloudUploads().FindById(pending.RecordId);
        if(record == null) return;
        record.Status = CloudUploadStatus.Failed;
        record.LastError = error.Length <= 500 ? error : error[..500];
        await _plugin.Storage.UpsertCloudUpload(record);
    }

    private static HttpRequestMessage CreateRequest(Uri endpoint, UploadCredentials credentials, PendingUpload pending) {
        var nonce = Guid.NewGuid().ToString();
        var signed = CloudUploadProtocol.Sign(pending.Body, credentials, pending.PluginVersion, pending.BuildHash, DateTimeOffset.UtcNow, nonce, pending.IdempotencyKey, pending.Path);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = new ByteArrayContent(pending.Body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content.Headers.ContentEncoding.Add("gzip");
        AddSignedHeaders(request, credentials, signed, nonce, pending.PluginVersion, pending.BuildHash, pending.IdempotencyKey);
        return request;
    }

    private void RemoveQueuedIdentities(IEnumerable<string> ids) {
        lock(_identityQueueLock) {
            foreach(var id in ids) _queuedIdentityIds.Remove(id);
        }
    }

    private static void AddSignedHeaders(HttpRequestMessage request, UploadCredentials credentials, SignedUploadRequest signed, string nonce, string pluginVersion, string buildHash, string idempotencyKey) {
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Installation", credentials.InstallationId);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Account", credentials.AccountId);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Key-Version", credentials.KeyVersion);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Timestamp", signed.Timestamp);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Plugin-Version", pluginVersion);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Schema-Version", CloudUploadProtocol.SchemaVersion.ToString());
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Client-Build", buildHash);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Content-SHA256", signed.BodySha256);
        request.Headers.TryAddWithoutValidation("X-PvPLogs-Signature", signed.Signature);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private bool TryGetCredentials(out UploadCredentials credentials) {
        credentials = null!;
        var config = _plugin.Configuration;
        if(string.IsNullOrWhiteSpace(config.CloudUploadInstallationId)
            || string.IsNullOrWhiteSpace(config.CloudUploadAccountId)
            || string.IsNullOrWhiteSpace(config.CloudUploadKeyVersion)
            || string.IsNullOrWhiteSpace(config.CloudUploadProtectedSecret)) {
            return false;
        }
        try {
            var secret = CloudCredentialProtector.Unprotect(config.CloudUploadProtectedSecret);
            if(secret.Length < 32) {
                return false;
            }
            credentials = new UploadCredentials(config.CloudUploadInstallationId, config.CloudUploadAccountId, config.CloudUploadKeyVersion, secret);
            return true;
        } catch(Exception ex) {
            _plugin.Log.Warning(ex, "Unable to unlock the cloud upload credential for the current Windows user.");
            return false;
        }
    }

    private bool TryGetEndpoint(out Uri endpoint) {
        return TryGetApiEndpoint(CloudUploadProtocol.UploadPath, out endpoint);
    }

    private LocalCloudCharacter? FindLocalCharacter(UploadEnvelopeV1 envelope) {
        if(envelope.Matches.Count > 0) {
            var match = envelope.Matches[0];
            return Find(match.LocalPlayer, match.Players.Select(player => (player.Alias, player.ContentId)));
        }
        if(envelope.CrystallineConflictMatches is { Count: > 0 }) {
            var match = envelope.CrystallineConflictMatches[0];
            return Find(match.LocalPlayer, match.Players.Select(player => (player.Alias, player.ContentId)));
        }
        if(envelope.RivalWingsMatches is { Count: > 0 }) {
            var match = envelope.RivalWingsMatches[0];
            return Find(match.LocalPlayer, match.Players.Select(player => (player.Alias, player.ContentId)));
        }
        return null;
    }

    private LocalCloudCharacter? Find(UploadAliasV1? localPlayer, IEnumerable<(UploadAliasV1 Alias, string? ContentId)> players) {
        if(localPlayer == null || string.IsNullOrWhiteSpace(localPlayer.Name) || string.IsNullOrWhiteSpace(localPlayer.HomeWorld)) return null;
        var participant = players.FirstOrDefault(player =>
            string.Equals(player.Alias.Name, localPlayer.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(player.Alias.HomeWorld, localPlayer.HomeWorld, StringComparison.OrdinalIgnoreCase));
        var name = localPlayer.Name.Trim();
        var world = localPlayer.HomeWorld.Trim();
        var key = CreateCharacterKey(name, world);
        return new LocalCloudCharacter(key, name, world, participant.ContentId);
    }

    private string CreateCharacterKey(string name, string world) {
        var installation = _plugin.Configuration.CloudUploadInstallationId.Trim();
        return $"{installation}:alias:{name.Trim().ToUpperInvariant()}@{world.Trim().ToUpperInvariant()}";
    }

    private bool TryGetApiEndpoint(string path, out Uri endpoint) {
        endpoint = null!;
        if(!Uri.TryCreate(_plugin.Configuration.CloudUploadApiBaseUrl, UriKind.Absolute, out var baseUri)) {
            return false;
        }
        if(baseUri.Scheme != Uri.UriSchemeHttps && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback)) {
            return false;
        }
        endpoint = new Uri(baseUri, path);
        return true;
    }

    private static unsafe string GetCurrentGameVersion() {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        return framework != null ? framework->GameVersionString : "unknown";
    }

    public void Dispose() {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try {
            _worker.Wait(TimeSpan.FromSeconds(2));
        } catch(AggregateException) {
        }
        _shutdown.Dispose();
        _httpClient.Dispose();
    }

    private sealed record PendingUpload(byte[] Body, string PluginVersion, string BuildHash, string IdempotencyKey, string RecordId, string Path, IReadOnlyList<string>? IdentityObservationIds);
    private sealed record LocalCloudCharacter(string Key, string Name, string World, string? ContentId);
    private sealed class CloudBindingResponse {
        public string InstallationId { get; init; } = "";
        public string AccountId { get; init; } = "";
        public int KeyVersion { get; init; }
        public string Secret { get; init; } = "";
    }
}

internal sealed record CloudBindingResult(bool Success, string Message);
