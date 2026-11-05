using System;
using System.Diagnostics;
using System.Threading;
using Technobabble;
class RefreshProbe {
    static void Check(bool test,string message){if(!test)throw new Exception(message);}
    static Snapshot Next(Machine machine,Snapshot prior,int timeout){
        var clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<timeout){
            machine.RequestDisks();machine.RequestGpus();
            var sample=machine.Latest;
            if(!Object.ReferenceEquals(sample,prior) && sample.ProcessCount>0)return sample;
            Thread.Sleep(20);
        }
        throw new Exception("No new sample within "+timeout+" ms");
    }
    static int Main(){
        try{
        Stopwatch shutdown;
        using(var machine=new Machine()){
            var old=Next(machine,machine.Latest,8000);
            foreach(int seconds in new[]{2,5,1,2}){
                machine.RefreshSeconds=seconds;
                var first=Next(machine,old,9000);
                if(first.RefreshSeconds!=seconds)first=Next(machine,first,12000);
                DateTime published=DateTime.Now;
                var second=Next(machine,first,12000);
                double elapsed=(second.Time-first.Time).TotalSeconds;
                double idle=(second.Time-published).TotalSeconds;
                Check(first.RefreshSeconds==seconds && second.RefreshSeconds==seconds,"Sample rate mismatch");
                Check(idle>=seconds-.15 && idle<seconds+1,"Unexpected sampling delay "+idle);
                Check(second.RxHistory.Length>=2,"Network history did not grow");
                Check(second.Disks.Rows.Length>0 && second.Disks.RefreshSeconds==seconds,"Disk demand stopped at "+seconds+"s");
                Check(second.Gpus.Rows.Length>0 && second.Gpus.RefreshSeconds==seconds,"GPU demand stopped at "+seconds+"s");
                Console.WriteLine("{0}s requested: {1:F3}s delay, {2:F3}s including collection; {3} volumes, {4} GPUs, {5} processes",seconds,idle,elapsed,second.Disks.Rows.Length,second.Gpus.Rows.Length,second.ProcessCount);
                old=second;
            }
            machine.RefreshSeconds=5;
            shutdown=Stopwatch.StartNew();
        }
        Check(shutdown.ElapsedMilliseconds<1000,"Slow refresh delayed exit");
        Console.WriteLine("Live intervals, demand-driven disk/GPU sampling, rate switching and prompt shutdown passed.");
        return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
