using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using PvpStats.Helpers;
using System.Diagnostics;
using System.Numerics;

namespace PvpStats.Windows;

internal sealed class CloudOnboardingWindow : Window {
    private readonly Plugin _plugin;

    internal CloudOnboardingWindow(Plugin plugin) : base(Loc.T("PVPLogsCN Cloud Setup")) {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(520, 520),
        };
        Flags |= ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;
    }

    internal bool OpenIfRequired() {
        if(_plugin.CloudUploads.IsBound ||
           (_plugin.Configuration.CloudOnboardingConsentDecided && _plugin.Configuration.CloudOnboardingCharacterDecided)) {
            return false;
        }
        IsOpen = true;
        return true;
    }

    public override void Draw() {
        if(!_plugin.Configuration.CloudOnboardingConsentDecided) {
            DrawWebsitePolicyStep();
            return;
        }
        DrawCharacterChoice();
    }

    private void DrawWebsitePolicyStep() {
        ImGui.TextColored(_plugin.Configuration.Colors.Header, Loc.T("Read the privacy policy on the website"));
        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionMax().X);
        ImGui.TextWrapped(Loc.T("The complete PVPLogsCN privacy policy and consent confirmation are provided on the website. Sign in, read the current policy, and confirm it before generating a binding code."));
        ImGui.Spacing();
        ImGui.TextWrapped(Loc.T("If you do not enable the cloud service, every original local feature remains available."));
        ImGui.PopTextWrapPos();

        if(ImGui.Button(Loc.T("Open privacy policy"))) {
            Process.Start(new ProcessStartInfo {
                UseShellExecute = true,
                FileName = "https://pvplogs.karriis.com/privacy-policy",
            });
        }

        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 42f * ImGuiHelpers.GlobalScale);
        if(ImGui.Button(Loc.T("Use local features only"))) {
            _plugin.Configuration.CloudOnboardingConsentDecided = true;
            _plugin.Configuration.CloudOnboardingCharacterDecided = true;
            _plugin.Configuration.CloudUploadConsentAccepted = false;
            _plugin.Configuration.CloudUploadEnabled = false;
            _plugin.Configuration.Save();
            IsOpen = false;
            _plugin.WindowManager.OpenSplashWindowDirect();
        }
        ImGui.SameLine();
        if(ImGui.Button(Loc.T("Continue to character selection"))) {
            _plugin.Configuration.CloudOnboardingConsentDecided = true;
            _plugin.Configuration.Save();
        }
    }

    private void DrawCharacterChoice() {
        ImGui.TextColored(_plugin.Configuration.Colors.Header, Loc.T("Select your only bound character"));
        ImGui.Spacing();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionMax().X);
        ImGui.TextWrapped(Loc.T("Each website account can bind only one character. Please log in with the character you use most before continuing."));
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        var current = _plugin.GameState.CurrentPlayer;
        var contentId = _plugin.PlayerState.ContentId;
        var worldId = _plugin.ObjectTable.LocalPlayer?.HomeWorld.RowId ?? 0;
        if(current == null || contentId == 0 || worldId == 0) {
            ImGui.TextColored(new Vector4(1f, 0.72f, 0.2f, 1f), Loc.T("Waiting for a logged-in character..."));
        } else {
            ImGui.TextDisabled(Loc.T("Detected character"));
            ImGui.TextColored(new Vector4(0.35f, 0.85f, 0.45f, 1f), $"{current.Name} @ {current.HomeWorld}");
        }

        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 42f * ImGuiHelpers.GlobalScale);
        if(ImGui.Button(Loc.T("Do not bind · use local features only"))) {
            _plugin.Configuration.CloudOnboardingCharacterDecided = true;
            _plugin.Configuration.CloudUploadEnabled = false;
            _plugin.Configuration.Save();
            IsOpen = false;
            _plugin.WindowManager.OpenSplashWindowDirect();
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(current == null || contentId == 0 || worldId == 0);
        if(ImGui.Button(Loc.T("Use this character")) && current != null && contentId != 0 && worldId != 0) {
            _plugin.Configuration.CloudOnboardingCharacterDecided = true;
            _plugin.Configuration.CloudSelectedCharacterName = current.Name.Trim();
            _plugin.Configuration.CloudSelectedCharacterWorld = current.HomeWorld.Trim();
            _plugin.Configuration.CloudSelectedCharacterWorldId = worldId;
            _plugin.Configuration.CloudSelectedCharacterContentId = contentId.ToString();
            _plugin.Configuration.Save();
            IsOpen = false;
            _plugin.WindowManager.OpenConfigWindow();
        }
        ImGui.EndDisabled();
    }
}
