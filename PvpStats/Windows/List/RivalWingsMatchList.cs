using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using PvpStats.Helpers;
using PvpStats.Types.Match;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace PvpStats.Windows.List;
internal class RivalWingsMatchList : MatchList<RivalWingsMatch> {

    public override string Name => "RW Matches";

    protected override List<ColumnParams> Columns { get; set; } = new() {
        new ColumnParams{Name = Loc.T("Start Time"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 125f },
        new ColumnParams{Name = Loc.T("Arena"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 140f },
        new ColumnParams{Name = Loc.T("Job"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 75f, Priority = 1 },
        new ColumnParams{Name = Loc.T("Team"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 65f },
        new ColumnParams{Name = Loc.T("Duration"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 40f, Priority = 2 },
        new ColumnParams{Name = Loc.T("Result"), Flags = ImGuiTableColumnFlags.WidthFixed, Width = 40f },
        new ColumnParams{Name = Loc.T("Tags"), Flags = ImGuiTableColumnFlags.WidthStretch, Width = 80f, Priority = 3 },
    };

    public RivalWingsMatchList(Plugin plugin, SemaphoreSlim? interlock = null) : base(plugin, plugin.RWCache, interlock) {
    }

    public override void DrawListItem(RivalWingsMatch item) {
        ImGui.SameLine(0f * ImGuiHelpers.GlobalScale);
        if(item.IsBookmarked) {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(_plugin.Configuration.Colors.Favorite - new Vector4(0f, 0f, 0f, 0.7f)));
        }
        ImGui.Text($"{item.DutyStartTime.ToLocalTime():yyyy-MM-dd HH:mm}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(MatchHelper.GetArenaName(item.Arena));

        ImGui.TableNextColumn();
        var localPlayerJob = item.LocalPlayerTeamMember?.Job;
        var jobName = PlayerJobHelper.GetNameFromJob(localPlayerJob);
        ImGuiHelper.CenterAlignCursor(jobName);
        ImGui.TextColored(_plugin.Configuration.GetJobColor(localPlayerJob), jobName);

        ImGui.TableNextColumn();
        var teamColor = _plugin.Configuration.GetRivalWingsTeamColor(item.LocalPlayerTeam);
        ImGui.TextColored(teamColor, MatchHelper.GetTeamName(item.LocalPlayerTeam));

        ImGui.TableNextColumn();
        var timeSpanString = ImGuiHelper.GetTimeSpanString(item.MatchDuration ?? TimeSpan.Zero);
        ImGuiHelper.DrawNumericCell(timeSpanString, -10f);

        ImGui.TableNextColumn();
        bool isWin = item.IsWin;
        bool isLoss = item.IsLoss;

        var color = isWin ? _plugin.Configuration.Colors.Win : isLoss ? _plugin.Configuration.Colors.Loss : _plugin.Configuration.Colors.Other;
        string resultText = isWin ? Loc.T("WIN") : isLoss ? Loc.T("LOSS") : "???";
        ImGuiHelper.CenterAlignCursor(resultText);
        ImGui.TextColored(color, resultText);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(item.Tags);
    }

    protected override string CSVRow(RivalWingsMatch match) {
        string csv = "";
        csv += match.DutyStartTime + ",";
        csv += (match.Arena != null ? MatchHelper.GetArenaName((RivalWingsMap)match.Arena) : "") + ",";
        csv += PlayerJobHelper.GetNameFromJob(match.LocalPlayerTeamMember?.Job) + ",";
        csv += MatchHelper.GetTeamName(match.LocalPlayerTeam) + ",";
        csv += match.MatchDuration + ",";
        csv += match.IsWin + ",";
        csv += "\n";
        return csv;
    }
}
