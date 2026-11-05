using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Technobabble {
    internal static class MacSensors {
        const string Library = "libtechnobabble-sensors.dylib";
        [DllImport(Library, EntryPoint="tb_cpu")]
        internal static extern int Cpu([Out] uint[] ticks, int capacity);
        [DllImport(Library, EntryPoint="tb_memory")]
        internal static extern int Memory(out ulong total, out ulong available);

        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
        internal struct Disk {
            public ulong Id, Read, Written;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string Name;
        }
        [DllImport(Library, EntryPoint="tb_disks")]
        internal static extern int Disks([Out] Disk[] rows, int capacity);
    }

    internal sealed class MacProcesses {
        uint[] previousCpu = Array.Empty<uint>();
        Dictionary<(int Id, long Start), double> previousTasks = new();
        readonly Stopwatch clock = Stopwatch.StartNew();
        double previousTime;

        public void Sample(Snapshot sample) {
            SampleCpu(sample);
            if (MacSensors.Memory(out ulong total, out ulong free) != 0) {
                sample.TotalMB = total / 1048576.0;
                sample.FreeMB = Math.Min(free, total) / 1048576.0;
            }

            double now = clock.Elapsed.TotalSeconds;
            double interval = now - previousTime;
            var next = new Dictionary<(int, long), double>();
            var rows = new List<ProcessRow>();
            foreach (var process in Process.GetProcesses()) {
                using (process) {
                    try {
                        var row = new ProcessRow {Id=process.Id, Name=process.ProcessName};
                        rows.Add(row);
                        row.Started = process.StartTime.ToUniversalTime().Ticks;
                        row.Memory = process.WorkingSet64;
                        double cpu = process.TotalProcessorTime.TotalSeconds;
                        var identity = (row.Id, row.Started);
                        if (interval > 0 && previousTasks.TryGetValue(identity, out double prior) && cpu >= prior)
                            row.Cpu = Math.Clamp(100 * (cpu-prior) / (interval*Environment.ProcessorCount), 0, 100);
                        next[identity] = cpu;
                    } catch (InvalidOperationException) { } // Exited during sampling.
                    catch (Win32Exception) { } // macOS can restrict another user's process metrics.
                }
            }
            previousTime = now;
            previousTasks = next;
            sample.ProcessCount = rows.Count;
            sample.Processes = rows.OrderByDescending(p => p.Cpu).ThenByDescending(p => p.Memory).ToArray();
        }

        void SampleCpu(Snapshot sample) {
            int capacity = Math.Max(256, Environment.ProcessorCount);
            var ticks = new uint[4*capacity]; // Darwin order: user, system, idle, nice.
            int cores = MacSensors.Cpu(ticks, capacity);
            var perCore = new double[cores];
            long all = 0, idle = 0;
            if (previousCpu.Length == 4*cores) {
                for (int i = 0; i < cores; i++) {
                    long total = 0;
                    for (int j=4*i; j<4*i+4; j++) total += unchecked(ticks[j] - previousCpu[j]);
                    long rest = unchecked(ticks[4*i+2] - previousCpu[4*i+2]);
                    perCore[i] = total > 0 ? 100.0*(total-rest)/total : 0;
                    all += total; idle += rest;
                }
            }
            sample.CoreCpu = perCore;
            sample.Cpu = all > 0 ? 100.0*(all-idle)/all : 0;
            previousCpu = ticks[..(4*cores)];
        }
    }

    internal sealed class MacDevices {
        Dictionary<ulong, MacSensors.Disk> baseline = new();
        DiskSnapshot disk = new();
        double last;
        public DiskSnapshot Disks(double now, int refresh) {
            var records = new MacSensors.Disk[128];
            int count = MacSensors.Disks(records, records.Length);
            var next = new Dictionary<ulong, MacSensors.Disk>();
            var rows = new List<DiskRow>();
            foreach (var d in records.Take(count)) {
                next[d.Id] = d;
                var row = new DiskRow {Name=string.IsNullOrEmpty(d.Name) ? $"disk:{d.Id:x}" : d.Name};
                if (now > last && baseline.TryGetValue(d.Id, out var old) && d.Read >= old.Read && d.Written >= old.Written) {
                    row.Read = (d.Read-old.Read)/(now-last);
                    row.Write = (d.Written-old.Written)/(now-last);
                }
                rows.Add(row);
            }
            var result = new DiskSnapshot { Rows=rows.OrderBy(r=>r.Name,StringComparer.Ordinal).ToArray(), RefreshSeconds=refresh,
                Status=rows.Count == 0 ? "Storage counters unavailable on this Mac" : "Collecting storage baselines" };
            if (rows.Count > 0 && rows.All(r=>r.Read.HasValue)) {
                bool same = refresh == disk.RefreshSeconds && next.Keys.ToHashSet().SetEquals(baseline.Keys);
                result.ReadHistory = Snapshot.Append(same ? disk.ReadHistory : Array.Empty<double>(), rows.Sum(r=>r.Read.Value));
                result.WriteHistory = Snapshot.Append(same ? disk.WriteHistory : Array.Empty<double>(), rows.Sum(r=>r.Write.Value));
                result.Status = $"macOS storage devices | refresh {refresh}s";
            }
            baseline = next; last = now; disk = result;
            return result;
        }
        public void ResetDisks() { baseline.Clear(); disk = new(); }
        public void ResetGpu() { }
        public GpuSnapshot Gpus(int refresh) => new() { RefreshSeconds=refresh, Status="GPU metrics unavailable in this macOS preview" };
    }
}
