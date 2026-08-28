using Dalamud.Configuration;
using Dalamud.Interface.Colors;
using PvpStats.Helpers;
using PvpStats.Types.Display;
using PvpStats.Types.Match;
using PvpStats.Types.Player;
using System;
using System.Numerics;
using System.Threading;

namespace PvpStats.Settings;

public enum UiLanguageMode {
    Auto,
    English,
    SimplifiedChinese,
}

[Serializable]
public class Configuration : IPluginConfiguration {
    public static readonly int CurrentVersion = 0;
    public int Version { get; set; } = CurrentVersion;
    public string LastPluginVersion { get; set; } = "0.0.0.0";
    public UiLanguageMode UiLanguage { get; set; } = UiLanguageMode.Auto;
    public bool? EnableDBCachingCC { get; set; }
    public bool? EnableDBCachingFL { get; set; }
    public bool? EnableDBCachingRW { get; set; }

    public bool? EnableTimelineCC { get; set; }
    public bool? EnableActionAnalyticsCC { get; set; }
    public bool? EnableTimelineFL { get; set; }
    public bool? EnableTimelineRW { get; set; }

    // Cloud uploads are opt-in. The secret is encrypted with Windows DPAPI for the current user.
    public bool CloudUploadEnabled { get; set; } = false;
    public bool CloudUploadConsentAccepted { get; set; } = false;
    public bool CloudOnboardingConsentDecided { get; set; } = false;
    public bool CloudOnboardingCharacterDecided { get; set; } = false;
    public string CloudSelectedCharacterName { get; set; } = "";
    public string CloudSelectedCharacterWorld { get; set; } = "";
    public uint CloudSelectedCharacterWorldId { get; set; }
    public string CloudSelectedCharacterContentId { get; set; } = "";
    // Retained only to migrate credentials issued by older configurable endpoints.
    // Runtime requests always use CloudUploadProtocol.ApiBaseUrl.
    public string CloudUploadApiBaseUrl { get; set; } = "";
    public string CloudUploadInstallationId { get; set; } = "";
    public string CloudUploadAccountId { get; set; } = "";
    public string CloudUploadDisplayName { get; set; } = "";
    public string CloudUploadKeyVersion { get; set; } = "";
    public string CloudUploadProtectedSecret { get; set; } = "";

    public bool? DisableMatchGuardsRW { get; set; }
    public bool EnablePlayerLinking { get; set; } = true;
    public bool EnableAutoPlayerLinking { get; set; } = true;
    public bool EnableManualPlayerLinking { get; set; } = true;
    public bool LeftPlayerTeam { get; set; } = false;
    public bool? OrderFrontlineTeamsByPlacement { get; set; }
    public bool AnchorTeamNames { get; set; } = true;
    //public bool? JobIconCells { get; set; }
    public bool ResizeableMatchWindow { get; set; } = true;
    public bool ShowBackgroundImage { get; set; } = true;
    public bool? StretchScoreboardColumns { get; set; }
    public bool SizeFiltersToFit { get; set; } = false;
    public bool PersistWindowSizePerTab { get; set; } = true;
    public bool MinimizeWindow { get; set; } = true;
    public bool MinimizeDirectionLeft { get; set; } = false;
    public bool ResizeWindowLeft { get; set; } = false;
    public bool AdjustWindowHeightOnFilterCollapse { get; set; } = false;
    public bool ColorScaleStats { get; set; } = true;

    public float TeamRowAlpha { get; set; } = 0.6f;
    public float PlayerRowAlpha { get; set; } = 0.3f;
    //public float ScoreboardRowPaddingFactor { get; set; } = 1f;
    public WindowConfiguration CCWindowConfig { get; set; } = new();
    public WindowConfiguration FLWindowConfig { get; set; } = new();
    public WindowConfiguration RWWindowConfig { get; set; } = new();
    public ColorConfiguration Colors { get; set; } = new();

    [NonSerialized]
    private Plugin? _plugin;
    [NonSerialized]
    private SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    public Configuration() {
    }

    public void Initialize(Plugin plugin) {
        _plugin = plugin;
    }

    public void Save() {
        //try {
        //    await _fileLock.WaitAsync();
        //    _plugin!.PluginInterface.SavePluginConfig(this);
        //} finally {
        //    _fileLock.Release();
        //}
        _plugin!.PluginInterface.SavePluginConfig(this);
    }

    public Vector4 GetJobColor(Job? job) {
        return PlayerJobHelper.GetSubRoleFromJob(job) switch {
            JobSubRole.TANK => Colors.Tank,
            JobSubRole.HEALER => Colors.Healer,
            JobSubRole.MELEE => Colors.Melee,
            JobSubRole.RANGED => Colors.Ranged,
            JobSubRole.CASTER => Colors.Caster,
            _ => ImGuiColors.DalamudWhite,
        };
    }

    public Vector4 GetFrontlineTeamColor(FrontlineTeamName? team) {
        return team switch {
            FrontlineTeamName.Maelstrom => Colors.Maelstrom,
            FrontlineTeamName.Adders => Colors.Adders,
            FrontlineTeamName.Flames => Colors.Flames,
            _ => ImGuiColors.DalamudWhite,
        };
    }

    public Vector4 GetRivalWingsTeamColor(RivalWingsTeamName? team) {
        return team switch {
            RivalWingsTeamName.Falcons => Colors.Falcons,
            RivalWingsTeamName.Ravens => Colors.Ravens,
            _ => ImGuiColors.DalamudWhite,
        };
    }

    public Vector4 GetFrontlineWinRateColor(FLAggregateStats stats) {
        return stats.FirstPlaces * 2 > stats.SecondPlaces + stats.ThirdPlaces ? Colors.Win : stats.FirstPlaces * 2 < stats.SecondPlaces + stats.ThirdPlaces ? Colors.Loss : ImGuiColors.DalamudWhite;
    }
}
