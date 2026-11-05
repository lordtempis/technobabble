using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Technobabble;

internal static partial class Checks {
    static List<string> ExpectedRows(string file, int width) {
        var rows = new List<string>();
        string text;
        using (var reader = new StreamReader(typeof(Widget).Assembly.GetManifestResourceStream(file))) {
            text = reader.ReadToEnd().Replace("\r", "").Replace("\t", "    ");
        }
        foreach (string line in ("// File: " + file + "\n" + text).Split('\n')) {
            if (line.Length == 0) rows.Add("");
            for (int start = 0; start < line.Length; start += width) {
                rows.Add(line.Substring(start, Math.Min(width, line.Length - start)));
            }
        }
        return rows;
    }

    static void CodeScrollChecks(Machine host, Pictures images) {
        const int width = 37, height = 53;
        int tick = 0;
        bool previousMode = host.StudioActive;
        try {
            host.StudioActive = true;
            var code = new Widget(Array.IndexOf(Widget.Names, "code"), 27, host, images, 0);
            var files = new HashSet<string>(typeof(Widget).Assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".cs", StringComparison.Ordinal)));
            int fileCount = files.Count;
            Check(fileCount >= 10, "Studio source library missing");
            string first = null;
            while (files.Count > 0) {
                Grid view = code.Frame(width, height, (tick + .1) * .24);
                string heading = new string(view.Ch[0]).TrimEnd();
                Check(heading.StartsWith("// File: "), "Missing complete file boundary at row " + tick);
                string name = heading.Substring(9);
                Check(files.Remove(name), "Source file repeated or missing: " + name);
                if (first == null) first = heading;
                var expected = ExpectedRows(name, width);
                for (int offset = 0; offset < expected.Count;) {
                    view = code.Frame(width, height, (tick + .1) * .24);
                    int visible = Math.Min(height, expected.Count - offset);
                    for (int y = 0; y < visible; y++) {
                        Check(new string(view.Ch[y]) == expected[offset + y].PadRight(width),
                            "Source scrolling skipped or clipped text in " + name);
                    }
                    offset += visible;
                    tick += visible;
                }
            }
            Check(tick > 1000 && new string(code.Frame(width, height, (tick + .1) * .24).Ch[0]).TrimEnd() == first,
                "Studio code repeated before rendering the complete library");
            Grid frame = code.Frame(width, height, (tick + 17.1) * .24);
            Grid next = code.Frame(width, height, (tick + 18.1) * .24);
            for (int y = 0; y < height - 1; y++) {
                Check(frame.Ch[y + 1].SequenceEqual(next.Ch[y]) && frame.Fg[y + 1].SequenceEqual(next.Fg[y]),
                    "One tick moved more than one colored screen row");
            }
            double paused = (tick + 18.1) * .24;
            foreach (int columns in new[] { 1, 24, 140, 17, width }) code.Frame(columns, height, paused);
            Check(Contents(code.Frame(width, height, paused)) == Contents(next), "Paused resize lost the source position");
            host.StudioActive = false;
            var original = new Widget(Array.IndexOf(Widget.Names, "code"), 27, host, images, 0);
            Check(Contents(code.Frame(width, height, paused)) == Contents(original.Frame(width, height, paused)),
                "Classic reused the Studio stream at the same tick");
            host.StudioActive = true;
            Check(Contents(code.Frame(width, height, paused)) == Contents(next), "Studio reused the Classic frame");
            Console.WriteLine("Studio code: {0} complete files, {1} wrapped rows, pause, resize and mode switching passed.", fileCount, tick);
        } finally { host.StudioActive = previousMode; }
    }

    static void CodeTypingChecks(Machine host, Pictures images) {
        var dir = Path.Combine(Path.GetTempPath(), "technobabble-typing-" + Guid.NewGuid().ToString("N"));
        var scene = new Scene(8, 10, new Random(24), host, images);
        var store = new LayoutStore(dir);
        var board = new Studio(scene, store);
        try {
            scene.Resize(100, 40);
            board.Load(LayoutStore.BlankExample);
            board.CycleCodeStyle();
            Check(scene.CodePlayback == CodeStyle.Scroll, "Typing key changed a different widget");
            board.OpenPaneEditor();
            board.Panes.SelectWidget("code");
            Check(board.ApplyPaneEdit(), "Could not place code in editor");
            string original = scene.LayoutText();
            int lastLength = 0;
            foreach (var speed in new[] {CodeStyle.Slow, CodeStyle.Normal, CodeStyle.Fast}) {
                board.CycleCodeStyle();
                Check(scene.CodePlayback == speed && board.Status().Contains("typing "), "Missing typing controls");
                var code = new Widget(Array.IndexOf(Widget.Names, "code"), 27, host, images, 0);
                var first = code.Frame(96, 16, 0, speed);
                Check(new string(first.Ch[0]).TrimEnd() == "_", "Typing exposed future text at startup");
                var partial = code.Frame(96, 16, .32, speed);
                int length = Contents(partial).Count(ch => !Char.IsWhiteSpace(ch) && ch != '_');
                Check(length > lastLength, "Typing speeds did not change the reveal rate");
                lastLength = length;
                var later = code.Frame(96, 16, 3, speed);
                string frozen = Contents(later);
                var switched = code.Frame(96, 16, 3, speed == CodeStyle.Fast ? CodeStyle.Slow : CodeStyle.Fast);
                Check(Contents(switched) == frozen, "Speed change moved the paused cursor");
                foreach (int width in new[] {1, 8, 24, 160, 96}) code.Frame(width, 16, 3, speed);
                Check(Contents(code.Frame(96,16,3,speed)) == frozen, "Paused reflow changed typed text");
            }
            string fast = scene.LayoutText();
            Check(fast.StartsWith("Hollywood Studio 4\n") && fast.EndsWith(" Fast\n"), "Typing settings were not serialized");
            store.Save("Typing", fast, false);
            board.Leave();
            board.CycleCodeStyle();
            Check(scene.CodePlayback == CodeStyle.Scroll, "Typing leaked into Classic");
            board.Enter();
            Check(scene.LayoutText() == fast && scene.CodePlayback == CodeStyle.Fast, "Mode restoration lost typing");
            Check(board.UndoPaneEdit() && scene.CodePlayback == CodeStyle.Fast, "Undo reverted typing preferences");
            board.Load("Typing");
            Check(scene.LayoutText() == fast && board.CodeSelected, "Saved typing layout did not load");
            foreach (string invalid in new[]{"Turbo", "3", "Scroll", ""}) {
                bool rejected = false;
                try { scene.RestoreLayout(fast.Replace(" Fast\n", " " + invalid + "\n")); }
                catch(InvalidDataException) { rejected = true; }
                Check(rejected && scene.LayoutText() == fast, "Malformed typing preference changed the layout");
            }
            board.CycleCodeStyle();
            Check(scene.LayoutText() == original, "Scrolling no longer emits the original layout format");
            var timeline = new CodeTyping(new[] {"ab;", "", "cd"}, 23);
            var positions = new List<string>();
            for (int sample = 0; sample < 400; sample++) {
                int line, column;
                timeline.Position(out line, out column);
                string position = line + "/" + column;
                if (positions.Count == 0 || positions.Last() != position) positions.Add(position);
                timeline.Advance(.002, CodeStyle.Normal);
            }
            Check(String.Join(" ", positions.Take(9)) == "0/0 0/1 0/2 0/3 1/0 2/0 2/1 2/2 0/0",
                "Typing skipped characters or blank lines at rollover");
            Console.WriteLine("Code typing: character reveal, three speeds, pause/resize, empty lines, rollover, undo, save/load, and Classic isolation passed.");
        } finally {
            board.Leave();
            string file = Path.Combine(dir, "Typing.layout");
            if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(dir)) Directory.Delete(dir);
        }
    }
}
