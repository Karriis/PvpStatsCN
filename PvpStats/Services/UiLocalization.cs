using PvpStats.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace PvpStats.Services;

internal static class UiLocalization {
    private const string ChineseResourceName = "PvpStats.Localization.zh-CN.json";

    private static Plugin? _plugin;
    private static IReadOnlyDictionary<string, string> _chinese = new Dictionary<string, string>();

    internal static void Initialize(Plugin plugin) {
        _plugin = plugin;
        _chinese = LoadResource(ChineseResourceName);
    }

    internal static UiLanguageMode EffectiveLanguage {
        get {
            var configured = _plugin?.Configuration.UiLanguage ?? UiLanguageMode.Auto;
            if(configured != UiLanguageMode.Auto) {
                return configured;
            }

            var clientLanguage = _plugin?.ClientState.ClientLanguage.ToString() ?? string.Empty;
            if(clientLanguage.Contains("Chinese", StringComparison.OrdinalIgnoreCase)
                || CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) {
                return UiLanguageMode.SimplifiedChinese;
            }

            return UiLanguageMode.English;
        }
    }

    internal static string T(string english, params object?[] args) {
        var text = english;
        if(EffectiveLanguage == UiLanguageMode.SimplifiedChinese && _chinese.TryGetValue(english, out var translated)) {
            text = translated;
        }

        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    private static IReadOnlyDictionary<string, string> LoadResource(string resourceName) {
        try {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if(stream is null) {
                Plugin.Log2.Warning($"UI localization resource not found: {resourceName}");
                return new Dictionary<string, string>();
            }

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
                ?? new Dictionary<string, string>();
        } catch(Exception e) {
            Plugin.Log2.Error(e, $"Failed to load UI localization resource: {resourceName}");
            return new Dictionary<string, string>();
        }
    }
}
