using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Technobabble {
    internal sealed partial class Scene {
        Widget selected;
        public CodeStyle CodePlayback {get;set;}
        public int RefreshSeconds {get{return machine.RefreshSeconds;}}
        public void SetRefresh(int seconds){machine.RefreshSeconds=seconds;}
        public void EnterStudio(){machine.StudioActive=true;if(relay!=null)relay.Resume(Time);}
        public NetworkChoice Network {get{return machine.Network;}}
        public void SetNetwork(NetworkChoice choice){machine.Network=choice;}
        public NetworkRow[] NetworkAdapters {get{return machine.Latest.Networks;}}
        public void UpdateProcesses(){machine.RequestProcesses();Processes.Update(machine.Latest);}
        public Pane Selected {
            get {return Root.Leaves().FirstOrDefault(p=>p.Widget==selected) ?? Root.Leaves().First();}
        }
        public void Select(Widget w){selected=w;}
        public void SelectNext(int step){
            var leaves=Root.Leaves().ToList();int index=leaves.IndexOf(Selected);
            selected=leaves[(index+step+leaves.Count)%leaves.Count].Widget;
        }
        public string TogglePin(){
            var p=Selected;
            if(Root.Leaf && building && !p.Pinned)return "Wait for the first split before pinning.";
            p.Pinned=!p.Pinned;return (p.Pinned?"Pinned ":"Unpinned ")+p.Widget.Name;
        }
        public string Label(Widget w){
            var p=Root.Leaves().First(leaf=>leaf.Widget==w);
            return (p==Selected?"> ":"  ")+(p.Pinned?"* ":"")+w.Name;
        }
        public void LeaveStudio(){
            if(relay!=null)relay.Suspend(Time);
            SetRefresh(2);
            CodePlayback=CodeStyle.Scroll;
            machine.StudioActive=false;SetNetwork(NetworkChoice.Automatic);Processes.Reset();
            bool weighted=Dividers.Any(p=>p.Ratio!=50);
            bool reset=count!=initialCount || delay!=initialDelay || Root.Leaves().Any(p=>p.Pinned) || weighted;
            if(Root.Leaves().Any(p=>Array.IndexOf(Widget.Names,p.Widget.Name)>=Widget.ClassicCount)){
                Root=new Pane{Widget=Make(random.Next(Widget.ClassicCount))};reset=true;
            }
            foreach(var p in Root.Leaves())p.Pinned=false;
            foreach(var p in Dividers)p.Ratio=50;
            count=initialCount;delay=initialDelay;selected=null;
            if(reset)BeginRound();
        }
        public void ShowDisk(){ShowMonitor("disk");}
        public void ShowGpu(){ShowMonitor("gpu");}
        public void ShowProcesses(){ShowMonitor("process");}
        void ShowMonitor(string name){
            var existing=Root.Leaves().FirstOrDefault(p=>p.Widget.Name==name);
            if(existing!=null){Select(existing.Widget);return;}
            Pane target=Selected;
            target.Widget=Make(Array.IndexOf(Widget.Names,name));Select(target.Widget);
        }
        public string LayoutText(){
            bool typing=CodePlayback!=CodeStyle.Scroll;
            bool extended=typing || Processes.Sort!=ProcessSort.Cpu || Processes.Filter.Length>0 || Network.Id.Length>0 ||
                Dividers.Any(p=>p.Ratio!=50) || Root.Leaves().Any(p=>p.Widget.Name=="process");
            var tokens=new List<string>();Record(Root,tokens,extended);
            // Preserve the original format for the default rate, including old layout files.
            // The file signature predates the product rename. Keep both generations interoperable.
            return "Hollywood Studio "+(typing?"4":extended?"3":RefreshSeconds==2?"1":"2")+"\n"+count+" "+delay+
                (extended || RefreshSeconds!=2?" "+RefreshSeconds:"")+"\n"+String.Join(" ",tokens)+"\n"+
                (extended?(int)Processes.Sort+" "+Encode(Processes.Filter)+" "+Encode(Network.Id)+" "+Encode(Network.Id.Length==0?"":Network.Name)+(typing?" "+CodePlayback:"")+"\n":"");
        }
        static string Encode(string text){return text.Length==0?"-":Convert.ToBase64String(Encoding.UTF8.GetBytes(text));}
        static string Decode(string text,int limit){
            if(text=="-")return "";
            try{var value=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(text));if(value.Length>limit || value.Any(Char.IsControl))throw Invalid();return value;}
            catch(FormatException){throw Invalid();}catch(DecoderFallbackException){throw Invalid();}
        }
        static void Record(Pane p,List<string> tokens,bool weighted){
            if(p.Leaf){tokens.Add((p.Pinned?"*":"")+p.Widget.Name);return;}
            tokens.Add((p.HorizontalCut?"H":"V")+(weighted?":"+p.Ratio:""));Record(p.A,tokens,weighted);Record(p.B,tokens,weighted);
        }
        static InvalidDataException Invalid(){return new InvalidDataException("Invalid or unsupported Studio layout.");}
        public void RestoreLayout(string text){
            string[] lines=text.Replace("\r","").TrimEnd('\n').Split('\n');
            bool v4=lines[0]=="Hollywood Studio 4";
            bool extended=v4 || lines[0]=="Hollywood Studio 3";
            if(lines.Length!=(extended?4:3) || (lines[0]!="Hollywood Studio 1" && lines[0]!="Hollywood Studio 2" && !extended))throw Invalid();
            bool v2=lines[0]=="Hollywood Studio 2";
            string[] timing=lines[1].Split(' ');int cap,seconds,refresh=2;
            if(timing.Length!=(v2 || extended?3:2) || !int.TryParse(timing[0],out cap) || cap<1 || cap>Widget.ClassicCount ||
               !int.TryParse(timing[1],out seconds) || seconds<1 || seconds>86400)throw Invalid();
            if((v2 || extended) && (!int.TryParse(timing[2],out refresh) || !Machine.ValidRefresh(refresh)))throw Invalid();
            int sort=0;string filter="";NetworkChoice network=NetworkChoice.Automatic;
            CodeStyle style=CodeStyle.Scroll;
            if(extended){
                string[] settings=lines[3].Split(' ');
                if(settings.Length!=(v4?5:4) || !Int32.TryParse(settings[0],out sort) || sort<0 || sort>2)throw Invalid();
                if(v4 && (!Enum.TryParse(settings[4],out style) || !Enum.IsDefined(typeof(CodeStyle),style) ||
                    style==CodeStyle.Scroll || style.ToString()!=settings[4]))throw Invalid();
                filter=Decode(settings[1],64);string id=Decode(settings[2],256),name=Decode(settings[3],128);
                if((id.Length==0)!=(name.Length==0))throw Invalid();
                if(id.Length>0)network=new NetworkChoice(id,name);
            }
            var tokens=new Queue<string>(lines[2].Split(' '));
            if(tokens.Count>2*Widget.Names.Length-1)throw Invalid();
            var seen=new HashSet<string>();
            Pane nextRoot=ReadPane(tokens,seen,extended);
            if(tokens.Count!=0 || seen.Count>cap)throw Invalid();
            nextRoot.Arrange(bounds);
            SetRefresh(refresh);
            CodePlayback=style;
            EnterStudio();SetNetwork(network);Processes.Configure((ProcessSort)sort,filter);
            Root=nextRoot;count=cap;delay=seconds;selected=null;building=false;deck.Clear();next=Time+delay;Generation++;
        }
        Pane ReadPane(Queue<string> tokens,HashSet<string> seen,bool weighted){
            if(tokens.Count==0)throw Invalid();
            string token=tokens.Dequeue();
            int ratio=50;string axis=token;
            if(weighted && (token.StartsWith("H:") || token.StartsWith("V:"))){
                axis=token.Substring(0,1);if(!Int32.TryParse(token.Substring(2),out ratio) || ratio<10 || ratio>90)throw Invalid();
            }
            if(axis=="H" || axis=="V")return new Pane{HorizontalCut=axis=="H",Ratio=ratio,A=ReadPane(tokens,seen,weighted),B=ReadPane(tokens,seen,weighted)};
            bool pin=token.StartsWith("*");string name=pin?token.Substring(1):token;
            int kind=Array.IndexOf(Widget.Names,name);
            if(kind<0 || !seen.Add(name))throw Invalid();
            return new Pane{Widget=Make(kind),Pinned=pin};
        }
    }

    internal sealed class LayoutStore {
        public const string Example="Network Watch";
        public const string DiskExample="Disk Watch";
        public const string GpuExample="GPU Watch";
        public const string SystemExample="System Watch";
        public const string ProcessExample="Process Watch";
        public const string BlankExample="Blank Canvas";
        public const string RelayExample="Relay Watch";
        const string RelayText="Hollywood Studio 1\n3 15\nV *radar H *signal *engineering\n";
        const string BlankText="Hollywood Studio 1\n1 15\n*cmatrix\n";
        const string ProcessText="Hollywood Studio 3\n4 15 2\nV:65 *process H:50 *speedometer V:50 cmatrix code\n0 - - -\n";
        const string SystemText="Hollywood Studio 1\n6 15\nH V *htop *gpu H V *disk *speedometer V cmatrix code\n";
        const string GpuText="Hollywood Studio 1\n6 15\nV *gpu H V htop speedometer H logs V cmatrix code\n";
        const string ExampleText="Hollywood Studio 1\n6 15\nV *speedometer H V htop logs H cmatrix V hexdump code\n";
        const string DiskText="Hollywood Studio 1\n6 15\nV *disk H V htop speedometer H logs V cmatrix code\n";
        readonly string directory, legacyDirectory;
        public LayoutStore(string directory, string legacyDirectory = null) {
            this.directory = directory;
            this.legacyDirectory = legacyDirectory;
        }
        public static LayoutStore ForUser(){
            foreach (var key in new[] {"TECHNOBABBLE_LAYOUT_DIR", "HOLLYWOOD_LAYOUT_DIR"}) {
                string path = Environment.GetEnvironmentVariable(key);
                if (!String.IsNullOrWhiteSpace(path)) return new LayoutStore(path);
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new LayoutStore(Path.Combine(local, "Technobabble", "Layouts"), Path.Combine(local, "Hollywood", "Layouts"));
        }
        public string[] Names(){
            var names=new List<string>{Example,DiskExample,GpuExample,SystemExample,ProcessExample,BlankExample,RelayExample};
            names.AddRange(new[] {directory, legacyDirectory}.Where(Directory.Exists)
                .SelectMany(dir => Directory.GetFiles(dir, "*.layout")).Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static bool IsExample(string name){
            return String.Equals(name,Example,StringComparison.OrdinalIgnoreCase) || String.Equals(name,DiskExample,StringComparison.OrdinalIgnoreCase) || String.Equals(name,GpuExample,StringComparison.OrdinalIgnoreCase) || String.Equals(name,SystemExample,StringComparison.OrdinalIgnoreCase) || String.Equals(name,ProcessExample,StringComparison.OrdinalIgnoreCase) || String.Equals(name,BlankExample,StringComparison.OrdinalIgnoreCase) || String.Equals(name,RelayExample,StringComparison.OrdinalIgnoreCase);
        }
        string FileFor(string name){
            if(name==null || !Regex.IsMatch(name,@"\A[\p{L}\p{N}][\p{L}\p{N} _-]{0,47}\z") || name.TrimEnd()!=name ||
                Regex.IsMatch(name,@"\A(CON|AUX|NUL|PRN|COM[0-9¹²³]|LPT[0-9¹²³])\z",RegexOptions.IgnoreCase))
                throw new ArgumentException("Use 1–48 letters, numbers, spaces, hyphens or underscores. Start with a letter or number.");
            return Path.Combine(directory,name+".layout");
        }
        string ReadPath(string name) {
            string current = FileFor(name);
            if (legacyDirectory != null && !File.Exists(current)) {
                string legacy = Path.Combine(legacyDirectory, Path.GetFileName(current));
                if (File.Exists(legacy)) return legacy;
            }
            return current;
        }
        public string Read(string name){
            string path=ReadPath(name);
            if(!File.Exists(path)){
                if(String.Equals(name,Example,StringComparison.OrdinalIgnoreCase))return ExampleText;
                if(String.Equals(name,DiskExample,StringComparison.OrdinalIgnoreCase))return DiskText;
                if(String.Equals(name,GpuExample,StringComparison.OrdinalIgnoreCase))return GpuText;
                if(String.Equals(name,SystemExample,StringComparison.OrdinalIgnoreCase))return SystemText;
                if(String.Equals(name,ProcessExample,StringComparison.OrdinalIgnoreCase))return ProcessText;
                if(String.Equals(name,BlankExample,StringComparison.OrdinalIgnoreCase))return BlankText;
                if(String.Equals(name,RelayExample,StringComparison.OrdinalIgnoreCase))return RelayText;
            }
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                if(stream.Length>4096)throw new InvalidDataException("Layout file is too large.");
                using(var r=new StreamReader(stream))return r.ReadToEnd();
            }
        }
        public bool Exists(string name){return File.Exists(ReadPath(name));}
        public void Save(string name,string text,bool overwrite){
            string path=FileFor(name);
            bool exists=Exists(name);
            if(IsExample(name) && !exists)throw new ArgumentException("This layout is built in. Save your copy under another name.");
            if(exists && !overwrite)throw new IOException("A layout with this name already exists.");
            Directory.CreateDirectory(directory);
            string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
            try{
                File.WriteAllText(temp,text,new UTF8Encoding(false));
                if(overwrite && File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
