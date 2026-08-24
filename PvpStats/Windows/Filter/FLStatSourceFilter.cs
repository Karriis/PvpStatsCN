using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PvpStats.Windows.Filter;
public class FLStatSourceFilter : StatSourceFilter {

    public static new Dictionary<StatSource, string> FilterNames => new() {
        { StatSource.LocalPlayer, Loc.T("Local Player") },
        { StatSource.Teammate, Loc.T("Teammates") },
        { StatSource.Opponent, Loc.T("Opponents") },
    };

    public FLStatSourceFilter() {
        Initialize();
    }

    public FLStatSourceFilter(FLStatSourceFilter filter) {
        Initialize(filter);
    }

    internal FLStatSourceFilter(Plugin plugin, Func<Task> action, FLStatSourceFilter? filter = null) : base(plugin, action) {
        Initialize(filter);
    }

    private void Initialize(FLStatSourceFilter? filter = null) {
        FilterState = new() {
                {StatSource.LocalPlayer, true },
                {StatSource.Teammate, true },
                {StatSource.Opponent, true },
        };
        if(filter is not null) {
            foreach(var category in filter.FilterState) {
                FilterState[category.Key] = category.Value;
            }
            InheritFromPlayerFilter = filter.InheritFromPlayerFilter;
        }
        UpdateAllSelected();
    }
}
