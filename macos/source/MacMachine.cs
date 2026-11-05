using System;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("macos15.0")]

namespace Technobabble {
    internal sealed partial class Machine {
        public const string PlatformName = "macOS";
        public const string DiskLabel = "macOS storage devices";
        public const string GpuHint = "GPU utilization is unavailable.";
        public static bool Supported => OperatingSystem.IsMacOSVersionAtLeast(15);
        static readonly string[] FilePaths = {"/bin/ls","/bin/cat","/usr/bin/env","/usr/bin/uname","/etc/services"};
        static readonly string[] TreePaths = {"/etc","/usr/share","/usr/lib","/System/Library","/Library"};
        readonly MacProcesses proc = new();
        readonly MacDevices devices = new();
        static string[] Logs() => FileLogs("/var/log/system.log");

        static NetworkReading[] Interfaces() {
            var result = new List<NetworkReading>();
            try {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var row = new NetworkReading { Id=nic.Id, Name=nic.Name, Up=nic.OperationalStatus==OperationalStatus.Up };
                    try {
                        var stats = nic.GetIPStatistics();
                        row.Received = stats.BytesReceived;
                        row.Sent = stats.BytesSent;
                        row.Valid = row.Received >= 0 && row.Sent >= 0;
                    } catch (NetworkInformationException) { }
                    result.Add(row);
                }
            } catch (NetworkInformationException) { }
            return result.ToArray();
        }
    }
}
