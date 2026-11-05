using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Technobabble {
    internal sealed class PdhCounters : IDisposable {
        [StructLayout(LayoutKind.Sequential)]
        struct CounterValue { public uint Status; public double Value; }
        [StructLayout(LayoutKind.Sequential)]
        struct CounterItem { public IntPtr Name; public CounterValue Value; }
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]
        static extern uint PdhOpenQuery(string source,UIntPtr user,out IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]
        static extern uint PdhAddEnglishCounter(IntPtr query,string path,UIntPtr user,out IntPtr counter);
        [DllImport("pdh.dll")]
        static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]
        static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint bytes,out uint count,IntPtr buffer);
        [DllImport("pdh.dll")]
        static extern uint PdhCloseQuery(IntPtr query);

        const uint MoreData=0x800007d2,NoData=0x800007d5;

        IntPtr query;
        readonly List<IntPtr> handles=new List<IntPtr>();
        public bool IsOpen {get{return query!=IntPtr.Zero;}}
        public uint Open(params string[] paths){
            Dispose();
            uint status=PdhOpenQuery(null,UIntPtr.Zero,out query);
            if(status!=0){Dispose();return status;}
            foreach(string path in paths){
                IntPtr counter;status=PdhAddEnglishCounter(query,path,UIntPtr.Zero,out counter);
                if(status!=0){Dispose();return status;}
                handles.Add(counter);
            }
            return 0;
        }
        public uint Collect(){return PdhCollectQueryData(query);}
        static bool Valid(CounterValue value){
            return value.Status<=1 && value.Value>=0 && !Double.IsNaN(value.Value) && !Double.IsInfinity(value.Value);
        }
        public uint Values(int index,out Dictionary<string,double?> values){
            IntPtr counter=handles[index];
            values=new Dictionary<string,double?>(StringComparer.OrdinalIgnoreCase);
            // The instance list can change between the size query and the read.
            for(int attempt=0;attempt<2;attempt++){
                uint bytes=0,count;
                uint status=PdhGetFormattedCounterArray(counter,0x200|0x8000,ref bytes,out count,IntPtr.Zero);
                if(status!=MoreData)return status==0?NoData:status;
                if(bytes==0 || bytes>16*1024*1024)return NoData;
                IntPtr buffer=Marshal.AllocHGlobal((int)bytes);
                try{
                    status=PdhGetFormattedCounterArray(counter,0x200|0x8000,ref bytes,out count,buffer);
                    if(status==MoreData)continue;
                    if(status!=0)return status;
                    int stride=Marshal.SizeOf(typeof(CounterItem));
                    if((ulong)count*(uint)stride>bytes)return NoData;
                    for(int i=0;i<count;i++){
                        var item=(CounterItem)Marshal.PtrToStructure(IntPtr.Add(buffer,i*stride),typeof(CounterItem));
                        string name=Marshal.PtrToStringUni(item.Name);
                        if(String.IsNullOrEmpty(name) || name.Equals("_Total",StringComparison.OrdinalIgnoreCase))continue;
                        values[name]=Valid(item.Value)?(double?)item.Value.Value:null;
                    }
                    return 0;
                }finally{Marshal.FreeHGlobal(buffer);}
            }
            return MoreData;
        }

        public void Dispose(){
            if(query!=IntPtr.Zero){PdhCloseQuery(query);query=IntPtr.Zero;}
            handles.Clear();
        }
    }
}
