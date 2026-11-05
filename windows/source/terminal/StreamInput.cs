using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Technobabble {
    // Local launcher protocol: SIZE columns rows, KEY virtual-key modifiers UTF16-codepoint.
    // The Ubuntu launcher owns its tty; Windows keeps the complete scene, widgets and counters.
    internal sealed class StreamInput {
        readonly Queue<ConsoleKeyInfo> keys=new Queue<ConsoleKeyInfo>();
        readonly object sync=new object();
        int width,height;
        public volatile bool Closed;
        public bool Ready {get{lock(sync)return width>0 && height>0;}}
        public bool KeyAvailable {get{lock(sync)return keys.Count>0;}}
        public ConsoleKeyInfo ReadKey(){lock(sync)return keys.Dequeue();}
        public void Size(out int w,out int h){lock(sync){w=width;h=height;}}
        public StreamInput(TextReader reader){
            new Thread(()=>Read(reader)){IsBackground=true,Name="Technobabble terminal bridge"}.Start();
        }
        void Read(TextReader reader){
            try{
                string line;
                while((line=reader.ReadLine())!=null){
                    if(line.Length>128)continue;
                    string[] parts=line.Split(' ');int a,b,c;
                    if(parts.Length<3 || !int.TryParse(parts[1],out a) || !int.TryParse(parts[2],out b))continue;
                    lock(sync){
                        if(parts[0]=="SIZE" && parts.Length==3 && a>=1 && a<=1000 && b>=1 && b<=500){width=a;height=b;}
                        if(parts[0]=="KEY" && parts.Length==4 && a>=0 && a<=255 && b>=0 && b<=7 &&
                            int.TryParse(parts[3],out c) && c>=0 && c<=65535 && keys.Count<256)
                            keys.Enqueue(new ConsoleKeyInfo((char)c,(ConsoleKey)a,(b&2)!=0,(b&1)!=0,(b&4)!=0));
                    }
                }
            }catch(IOException){}finally{Closed=true;}
        }
    }
}
