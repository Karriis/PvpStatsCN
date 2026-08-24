using Dalamud.Bindings.ImGui;
using PvpStats.Helpers;
using System;
using System.Threading.Tasks;

namespace PvpStats.Windows.Filter;
internal class PlayerQuickSearchFilter : DataFilter {
    public override string Name => Loc.T("Quick Search");

    public string SearchText { get; set; } = "";

    public PlayerQuickSearchFilter() { }

    internal PlayerQuickSearchFilter(Plugin plugin, Func<Task> action, PlayerQuickSearchFilter? filter = null) : base(plugin, action) {
        if(filter is not null) {
            SearchText = filter.SearchText;
        }
    }

    //not used!
    internal override void Draw() {
        string quickSearch = SearchText;
        ImGuiHelper.SetDynamicWidth(150f, 250f, 3f);
        if(ImGui.InputTextWithHint("###playerQuickSearch", Loc.T("Search..."), ref quickSearch, 100)) {
            SearchText = quickSearch;
            //Refresh...
        }
        ImGuiHelper.HelpMarker(Loc.T("Comma separate multiple players."));
    }
}
