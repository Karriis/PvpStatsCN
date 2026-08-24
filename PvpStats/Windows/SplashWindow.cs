using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using PvpStats.Helpers;
using System.Numerics;

namespace PvpStats.Windows;
internal class SplashWindow : Window {

    private Plugin _plugin;

    public SplashWindow(Plugin plugin) : base(Loc.T("PvP Tracker")) {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(460, 235),
            MaximumSize = new Vector2(460, 235)
        };
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public override void Draw() {
        ImGui.PushTextWrapPos(ImGui.GetContentRegionMax().X);
        ImGui.TextColored(
            new Vector4(1f, 0.35f, 0.25f, 1f),
            Loc.T("Important: Enable the plugin before entering a PvP match. Do not enable, disable, or reload it after entering; otherwise commands may stop working and the match record will be incomplete."));
        ImGui.PopTextWrapPos();
        ImGui.Separator();

        ImGui.TextUnformatted(Loc.T("Trackers:"));
        if(ImGui.Button(Loc.T("Crystalline Conflict"))) {
            _plugin.WindowManager.OpenCCWindow();

        }
        if(ImGui.Button(Loc.T("Frontline"))) {
            _plugin.WindowManager.OpenFLWindow();
        }
        if(ImGui.Button(Loc.T("Rival Wings"))) {
            _plugin.WindowManager.OpenRWWindow();
        }

        //ImGui.NewLine();
        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 25f * ImGuiHelpers.GlobalScale);
        using(_ = ImRaii.PushFont(UiBuilder.IconFont)) {
            if(ImGui.Button($"{FontAwesomeIcon.Cog.ToIconString()}##--OpenSettings")) {
                _plugin.WindowManager.OpenConfigWindow();
            }
        }
        ImGuiHelper.WrappedTooltip(Loc.T("Settings"));
        ImGui.SameLine();
        //ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 30f * ImGuiHelpers.GlobalScale);
        ImGuiHelper.DonateButton();
    }
}
