using System;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Widget {
        sealed class StudioCodeStream {
            static readonly string[][] Documents = LoadDocuments();
            readonly string[] lines;
            readonly int[] rowStarts;
            readonly int seed;
            int columns, lastTick, topLine, topColumn;
            CodeStyle lastStyle;
            CodeTyping typing;
            double lastTime;

            static string[][] LoadDocuments() {
                var assembly = typeof(Widget).Assembly;
                return assembly.GetManifestResourceNames()
                    .Where(name => name.EndsWith(".cs", StringComparison.Ordinal))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Select(name => new[] { "// File: " + name }
                        .Concat(ReadText(name).Select(line => line.Replace("\t", "    ")))
                        .ToArray())
                    .ToArray();
            }

            public StudioCodeStream(int seed) {
                this.seed = seed;
                var random = new Random(seed);
                var ordered = (string[][])Documents.Clone();
                for (int i = ordered.Length - 1; i > 0; i--) {
                    int other = random.Next(i + 1);
                    var document = ordered[i];
                    ordered[i] = ordered[other];
                    ordered[other] = document;
                }
                lines = ordered.SelectMany(document => document).ToArray();
                rowStarts = new int[lines.Length + 1];
            }

            void Measure(int width) {
                columns = width;
                for (int i = 0; i < lines.Length; i++) {
                    int wrappedRows = Math.Max(1, (lines[i].Length + width - 1) / width);
                    rowStarts[i + 1] = rowStarts[i] + wrappedRows;
                }
            }

            void Advance(int tick) {
                if (tick == lastTick) return;
                int total = rowStarts[lines.Length];
                long current = rowStarts[topLine] + topColumn / columns;
                int next = (int)((current + tick - lastTick) % total);
                if (next < 0) next += total;
                topLine = CodeTyping.AtOrBefore(rowStarts, next);
                topColumn = (next - rowStarts[topLine]) * columns;
                lastTick = tick;
            }

            public void Draw(Grid grid, int tick, double elapsed, CodeStyle style) {
                bool firstFrame = columns == 0;
                if (firstFrame) Measure(grid.W);
                // Advance in the old geometry before reflowing. Keep the source
                // character as the anchor across repeated resizes while paused.
                if (lastStyle == CodeStyle.Scroll) Advance(tick);
                else typing.Advance(elapsed - lastTime, lastStyle);
                if (columns != grid.W) Measure(grid.W);

                if (style != CodeStyle.Scroll && lastStyle == CodeStyle.Scroll) {
                    if (typing == null) typing = new CodeTyping(lines, seed);
                    // On an existing scroll, start typing the bottom visible row.
                    // A layout launched in typing mode starts at its first character.
                    int row = firstFrame ? 0 : (rowStarts[topLine] + topColumn / columns + grid.H - 1) % rowStarts[lines.Length];
                    int line = CodeTyping.AtOrBefore(rowStarts, row);
                    typing.Seek(line, (row - rowStarts[line]) * columns);
                }
                if (style != CodeStyle.Scroll || lastStyle != CodeStyle.Scroll) {
                    int cursorLine, cursorColumn;
                    typing.Position(out cursorLine, out cursorColumn);
                    // A complete line exactly as wide as the pane remains on its
                    // last row until the newline arrives, just like a terminal.
                    int segment = Math.Min(cursorColumn / columns, rowStarts[cursorLine + 1] - rowStarts[cursorLine] - 1);
                    int cursorRow = rowStarts[cursorLine] + segment;
                    int firstRow = Math.Max(0, cursorRow - grid.H + 1);
                    if (style != CodeStyle.Scroll) {
                        DrawTypedRows(grid, firstRow, cursorRow, cursorLine, cursorColumn, segment);
                    } else {
                        topLine = CodeTyping.AtOrBefore(rowStarts, firstRow);
                        topColumn = (firstRow - rowStarts[topLine]) * columns;
                        lastTick = tick;
                    }
                }
                lastStyle = style;
                lastTime = elapsed;
                if (style == CodeStyle.Scroll) DrawScrolledRows(grid);
            }

            void DrawTypedRows(Grid grid, int first, int cursor, int cursorLine, int cursorColumn, int segment) {
                int line = CodeTyping.AtOrBefore(rowStarts, first);
                string prefix = lines[cursorLine].Substring(0, cursorColumn);
                for (int row = first; row <= cursor; row++) {
                    if (row >= rowStarts[line + 1]) line++;
                    int start = (row - rowStarts[line]) * columns;
                    Syntax(grid, -start, row - first, line == cursorLine ? prefix : lines[line]);
                }
                grid.Set(cursorColumn - segment * columns, cursor - first, '_', Ink.Aqua);
            }

            void DrawScrolledRows(Grid grid) {
                int line = topLine;
                int start = topColumn / columns * columns;
                for (int y = 0; y < grid.H; y++) {
                    Syntax(grid, -start, y, lines[line]);
                    start += columns;
                    if (start >= lines[line].Length) {
                        start = 0;
                        line = (line + 1) % lines.Length;
                    }
                }
            }
        }
    }
}
