using System;

namespace PvpStats.Services.Cloud;

internal static class FrontlineUploadConventions {
    internal static int ToApiPlacement(int? localPlacement) => localPlacement switch {
        0 => 1,
        1 => 2,
        2 => 3,
        _ => throw new InvalidOperationException("A completed Frontline match must have a local placement between 0 and 2."),
    };
}
