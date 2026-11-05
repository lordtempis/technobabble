using System;

namespace Technobabble {
    internal sealed partial class Machine {
        public const string PlatformName = "Linux";
        public const string DiskLabel = "Linux block devices";
        public const string GpuHint = "No readable DRM or NVIDIA metrics.";
        public static bool Supported => OperatingSystem.IsLinux();
        static readonly string[] FilePaths = {"/usr/bin/ls","/usr/bin/bash","/usr/bin/env","/usr/bin/cat","/usr/bin/uname","/etc/os-release","/etc/services"};
        static readonly string[] TreePaths = {"/etc","/usr/share","/usr/lib","/sys/class","/proc/sys"};
        readonly LinuxProc proc = new();
        readonly LinuxDevices devices = new();
        static NetworkReading[] Interfaces() => LinuxProc.Interfaces();
        static string[] Logs() {
            var lines = FileLogs("/var/log/syslog","/var/log/messages");
            return lines.Length > 0 ? lines : LinuxDevices.Run("/usr/bin/journalctl","--system","--no-pager","-q","-n","48","-o","short")
                .Split('\n',StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
