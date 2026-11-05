using System;

namespace Technobabble {
    internal enum CodeStyle { Scroll, Slow, Normal, Fast }

    // One time interval per character, including the newline after each line.
    // Measuring once lets a hidden pane catch up without replaying keystrokes.
    internal sealed class CodeTyping {
        readonly int[] lineStarts;
        readonly int[] characterTimes;
        double playhead;

        public CodeTyping(string[] lines, int seed) {
            lineStarts = new int[lines.Length + 1];
            for (int i = 0; i < lines.Length; i++)
                lineStarts[i + 1] = lineStarts[i] + lines[i].Length + 1;
            characterTimes = new int[lineStarts[lines.Length] + 1];
            var random = new Random(seed);
            int index = 0;
            foreach (string line in lines) {
                for (int col = 0; col <= line.Length; col++) {
                    char ch = col == line.Length ? '\n' : line[col];
                    int delay = random.Next(6, 15);
                    if (index > 0 && col > 0 && ",;:.{}".IndexOf(line[col - 1]) >= 0) delay += 13;
                    if (ch == '\n') delay += 24;
                    characterTimes[index + 1] = characterTimes[index] + delay;
                    index++;
                }
            }
        }

        public void Seek(int line, int column) {
            playhead = characterTimes[lineStarts[line] + column];
        }

        static double UnitsPerSecond(CodeStyle style) {
            return style == CodeStyle.Slow ? 120 : style == CodeStyle.Normal ? 280 : 600;
        }
        public void Advance(double seconds, CodeStyle style) {
            playhead = (playhead + Math.Max(0, seconds) * UnitsPerSecond(style)) % characterTimes[characterTimes.Length - 1];
        }
        // A scripted console command uses the same cadence but stops at the newline.
        public int FirstLinePrefix(double seconds, CodeStyle style) {
            int length = lineStarts[1] - 1;
            double limit = Math.Min(characterTimes[length], Math.Max(0, seconds) * UnitsPerSecond(style));
            return AtOrBefore(characterTimes, (int)limit);
        }

        public void Position(out int line, out int column) {
            int character = AtOrBefore(characterTimes, (int)playhead);
            line = AtOrBefore(lineStarts, character);
            column = character - lineStarts[line];
        }

        internal static int AtOrBefore(int[] starts, int position) {
            int found = Array.BinarySearch(starts, position);
            return found >= 0 ? found : ~found - 1;
        }
    }
}
