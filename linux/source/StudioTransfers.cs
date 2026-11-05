using System;

namespace Technobabble {
    internal sealed partial class Widget {
        sealed class TransferFile {
            public readonly string Name;
            public readonly long Bytes;
            public readonly int Seconds;

            public TransferFile(string name, int minKiB, Random random) {
                Name = name;
                Bytes = (minKiB + random.Next(minKiB / 2)) * 1024L;
                Seconds = random.Next(4, 9);
            }
        }

        TransferFile[] transferFiles;
        int playlistSeconds;

        void CreateTransferPlaylist() {
            var random = new Random(seed);
            transferFiles = new[] {
                new TransferFile("boot.log", 128, random),
                new TransferFile("manifest.json", 320, random),
                new TransferFile("index.csv", 768, random),
                new TransferFile("audit.evtx", 2048, random),
                new TransferFile("trace.etl", 4096, random),
                new TransferFile("symbols.pdb", 8192, random),
                new TransferFile("capture.pcapng", 16384, random),
                new TransferFile("telemetry.db", 32768, random),
                new TransferFile("assets.pak", 65536, random),
                new TransferFile("update.cab", 131072, random),
                new TransferFile("memory.dmp", 262144, random),
                new TransferFile("backup.zip", 524288, random)
            };
            for (int i = transferFiles.Length - 1; i > 0; i--) {
                int other = random.Next(i + 1);
                var file = transferFiles[i];
                transferFiles[i] = transferFiles[other];
                transferFiles[other] = file;
            }
            foreach (var file in transferFiles)
                playlistSeconds += file.Seconds + 1; // Hold the completed file for one second.
        }

        void DrawStudioTransfer(Grid grid, double elapsed) {
            if (transferFiles == null) CreateTransferPlaylist();
            // Derive position from scene time so pausing, repainting or resizing
            // never advances a file. Only simulated names and sizes are used.
            double position = elapsed % playlistSeconds;
            int index = 0;
            while (position >= transferFiles[index].Seconds + 1) {
                position -= transferFiles[index].Seconds + 1;
                index++;
            }
            var current = transferFiles[index];
            double fraction = Math.Min(1, position / current.Seconds);
            string percent = ((int)(100 * fraction)).ToString() + "%";
            string total = Size(current.Bytes);
            if (grid.H == 1) {
                grid.Put(0, 0, percent + " " + total + " " + current.Name, Ink.White);
                return;
            }
            int row = 0;
            if (grid.H >= 5) grid.Put(0, row++, "pv | simulated transfers", Ink.Aqua);
            grid.Put(0, row++, current.Name, Ink.Bright);
            string counts = grid.W >= 34 ? Size(current.Bytes * fraction) + " / " + total : "of " + total;
            grid.Put(0, row++, percent + " " + counts, Ink.White);

            if (grid.H > row && grid.W >= 3) {
                int width = grid.W - 2;
                int filled = (int)(width * fraction);
                grid.Put(0, row++, "[" + new string('=', filled) + new string(' ', width - filled) + "]", Ink.Green);
            }
            int remaining = Math.Max(0, (int)Math.Ceiling(current.Seconds - position));
            string timing = remaining == 0 ? "Complete" : "ETA " + remaining + "s";
            grid.Put(0, row++, Size((double)current.Bytes / current.Seconds) + "/s  " + timing, Ink.White);

            int history = elapsed < playlistSeconds ? index : transferFiles.Length - 1;
            if (history == 0 || row + 1 >= grid.H) return;
            grid.Put(0, row++, "Completed", Ink.Aqua);
            for (int back = 1; back <= history && row < grid.H; back++) {
                int previous = (index - back + transferFiles.Length) % transferFiles.Length;
                var file = transferFiles[previous];
                grid.Put(0, row++, Size(file.Bytes).PadLeft(9) + "  " + file.Name, Ink.Gray);
            }
        }
    }
}
