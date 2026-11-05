using System;
namespace Technobabble {
    internal sealed class ProcessRow {
        public int Id; public string Name; public long Memory,Started; public double Cpu;
    }
    internal sealed class Snapshot {
        public DateTime Time=DateTime.Now;
        public ProcessRow[] Processes=new ProcessRow[0];
        public string[] Logs=new string[0];
        public string Nic="waiting";
        public string NicId="",NetworkStatus="";
        public bool StudioNetwork,NetworkReady;
        public NetworkRow[] Networks=new NetworkRow[0];
        public double Cpu, Rx, Tx, TotalMB, FreeMB;
        public double[] CoreCpu=new double[0];
        public double[] RxHistory=new double[0],TxHistory=new double[0];
        public int ProcessCount;
        public int RefreshSeconds=2;
        public DiskSnapshot Disks=new DiskSnapshot();
        public GpuSnapshot Gpus=new GpuSnapshot();
    }
    internal sealed class DiskRow {
        public string Name;
        public double? Read,Write;
    }
    internal sealed class DiskSnapshot {
        public DiskRow[] Rows=new DiskRow[0];
        public string Status="Waiting for disk samples";
        public int RefreshSeconds=2;
        public double[] ReadHistory=new double[0],WriteHistory=new double[0];
    }

    internal sealed class GpuRow {
        public string Id,Name;
        public double? Usage;
        public double[] History=new double[0];
        public GpuMemory Memory=new GpuMemory();
    }
    internal sealed class GpuSnapshot {
        public GpuRow[] Rows=new GpuRow[0];
        public string Status="Waiting for GPU samples";
        public int RefreshSeconds=2;
    }
    internal sealed class GpuMemory {
        public double? Dedicated,Shared,DedicatedLimit,SharedLimit;
    }
}
