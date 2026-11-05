using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Technobabble {
    // Match names by DXGI LUID, never by enumeration index (Task Manager orders GPUs differently).
    internal static class GpuAdapters {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct Description {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name;
            public uint Vendor,Device,Subsystem,Revision;
            public UIntPtr VideoMemory,SystemMemory,SharedMemory;
            public uint Low,High,Flags;
        }
        [DllImport("dxgi.dll",ExactSpelling=true)]
        static extern int CreateDXGIFactory1(ref Guid id,out IntPtr factory);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Enumerate(IntPtr self,uint index,out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Describe(IntPtr self,out Description description);
        static Delegate Method(IntPtr obj,int slot,Type type){
            return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),type);
        }
        internal static Dictionary<string,string> Read(){
            HashSet<string> software;return Read(out software);
        }
        internal static Dictionary<string,string> Read(out HashSet<string> software){
            Dictionary<string,GpuCapacity> capacities;return Read(out software,out capacities);
        }
        internal static Dictionary<string,string> Read(out HashSet<string> software,out Dictionary<string,GpuCapacity> capacities){
            var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            software=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            capacities=new Dictionary<string,GpuCapacity>(StringComparer.OrdinalIgnoreCase);
            IntPtr factory=IntPtr.Zero;
            try{
                Guid id=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
                if(CreateDXGIFactory1(ref id,out factory)<0)return names;
                // IDXGIFactory1::EnumAdapters1 and IDXGIAdapter1::GetDesc1, from dxgi.h.
                var enumerate=(Enumerate)Method(factory,12,typeof(Enumerate));
                for(uint i=0;i<64;i++){
                    IntPtr adapter;
                    if(enumerate(factory,i,out adapter)<0)break;
                    try{
                        Description d;
                        if(((Describe)Method(adapter,10,typeof(Describe)))(adapter,out d)>=0){
                            string luid="0x"+d.High.ToString("x8")+"_0x"+d.Low.ToString("x8");
                            names[luid]=d.Name;
                            capacities[luid]=new GpuCapacity{Dedicated=(double)d.VideoMemory.ToUInt64()+d.SystemMemory.ToUInt64(),Shared=d.SharedMemory.ToUInt64()};
                            if((d.Flags&2)!=0)software.Add(luid); // DXGI_ADAPTER_FLAG_SOFTWARE
                        }
                    }finally{Marshal.Release(adapter);}
                }
            }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}catch(COMException){}
            finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);}
            return names;
        }
    }
}
