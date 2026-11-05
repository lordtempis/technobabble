using System;
using System.Linq;
using System.Runtime.Versioning;
using Technobabble;

[assembly: SupportedOSPlatform("macos15.0")]

internal static partial class Checks {
    static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    static string Contents(Grid grid) =>
        string.Join("\n", grid.Ch.Select(row => new string(row)));

    public static void Main() {
        using var host = new Machine();
        var pictures = new Pictures();
        StudioUpgrades(host, pictures);
        PaneEditorChecks(host, pictures);
        CodeScrollChecks(host, pictures);
        CodeTypingChecks(host, pictures);
        RelayChecks(host, pictures);
    }
}
