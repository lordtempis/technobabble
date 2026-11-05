using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Technobabble {
    internal static partial class LinuxProgram {
        const string Usage = """
            Technobabble for Linux 1.13
            technobabble [-s 1..20] [-d seconds] [--classic | --studio] [--layout NAME] [--refresh 1|2|5]

            Start in Classic, or try: technobabble --layout "Relay Watch"
            Space pauses, R reshuffles, S switches modes, ? shows controls, Q exits.
            Studio: E edits panes, Tab selects, P pins, B resizes, W saves and L opens.
            C Process Watch, D disk, G GPU, N network adapter, Y code typing speed.
            Layouts: Relay Watch, System Watch, Process Watch, GPU Watch, Disk Watch,
            Network Watch and Blank Canvas. --version prints the edition and exits.
            """;

        static int Main(string[] args) {
            try {
                var options = Options.Parse(args);
                if (options.Help) { Console.WriteLine(Usage); return 0; }
                if (options.Version) { Console.WriteLine("Technobabble 1.13.0 (Linux)"); return 0; }
                if (!OperatingSystem.IsLinux()) { Console.Error.WriteLine("This edition runs on Linux."); return 2; }
                if (Console.IsInputRedirected || Console.IsOutputRedirected) {
                    Console.Error.WriteLine("Open a Linux terminal to run Technobabble interactively."); return 2;
                }
                using var machine = new Machine();
                var scene = new Scene(options.Count, options.Delay, new Random(), machine, new Pictures());
                var board = new Studio(scene, LayoutStore.ForUser());
                if (options.Layout != null) {
                    string error = board.TryLoad(options.Layout);
                    if (error != null) { Console.Error.WriteLine(error); return 2; }
                }
                int stop = 0;
                void Stop(PosixSignalContext context) { context.Cancel = true; Interlocked.Exchange(ref stop,1); }
                using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
                using var hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, Stop);
                using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
                Console.OutputEncoding = new UTF8Encoding(false);
                bool previous = Console.TreatControlCAsInput;
                try {
                    Console.TreatControlCAsInput = true;
                    Console.Write("\x1b[?1049h\x1b[?7l\x1b[?25l");
                    Play(scene,board,options,() => Volatile.Read(ref stop) != 0);
                } finally {
                    try { Console.Write("\x1b[0m\x1b[?7h\x1b[?25h\x1b[?1049l"); }
                    finally { Console.TreatControlCAsInput = previous; }
                }
                return 0;
            } catch (ArgumentException e) {
                Console.Error.WriteLine(e.Message); return 2;
            } catch (IOException e) {
                Console.Error.WriteLine(e.Message); return 1;
            }
        }
    }
}
