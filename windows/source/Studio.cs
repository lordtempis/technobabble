using System;
using System.IO;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Studio {
        readonly Scene scene;
        readonly LayoutStore store;
        string[] names=new string[0];
        string name="",error="";
        bool saving,confirm,replaceOnType;
        bool filtering,networking;
        bool active;
        string suspendedLayout,suspendedName,suspendedPane;
        NetworkChoice[] networkChoices=new NetworkChoice[0];
        int index=-1;
        bool editing;
        public readonly PaneEditor Panes;
        public bool Editing {get{return editing || Panes.IsOpen;}private set{editing=value;}}
        public string ActiveName {get;private set;}
        public string Notice {get;private set;}
        public string NameText {get{return name;}}
        public string[] Choices {get{return names;}}
        public string Error {get{return error;}}
        public bool PickingNetwork {get{return networking;}}
        public int EditorLimit {get{return filtering?64:48;}}
        public string EditorTitle {get{return networking?"Choose network adapter":filtering?"Filter processes":saving?"Save Studio layout":"Open Studio layout";}}
        public string EditorLabel {get{return networking?"Network adapter":filtering?"Process name or PID":"Layout name";}}
        public string EditorHint {get{return networking?"Use Up / Down to choose, Enter to apply.":filtering?"Matches names or PIDs. Clear to show all.":"Type a name, or use Up / Down to choose.";}}
        public string AcceptLabel {get{return filtering || networking?"Apply":saving?"Save":"Open";}}
        public void SetName(string value){name=value;index=networking?Array.IndexOf(names,value):-1;replaceOnType=false;Changed();}
        public Studio(Scene scene,LayoutStore store){this.scene=scene;this.store=store;Panes=new PaneEditor(scene);}
        public void Load(string layout){
            scene.RestoreLayout(store.Read(layout));active=true;suspendedLayout=null;
            scene.ResetRelay();
            Panes.ClearHistory();Editing=false;Resizing=false;ActiveName=layout;Notice="Opened "+layout;
        }
        public string TryLoad(string layout){
            try{Load(layout);return null;}
            catch(InvalidDataException e){return e.Message;}catch(IOException e){return e.Message;}
            catch(UnauthorizedAccessException e){return e.Message;}catch(ArgumentException e){return e.Message;}
        }
        public void Enter(){
            if(!active){
                if(suspendedLayout!=null){
                    scene.RestoreLayout(suspendedLayout);ActiveName=suspendedName;
                    var pane=scene.Root.Leaves().FirstOrDefault(p=>p.Widget.Name==suspendedPane);
                    if(pane!=null)scene.Select(pane.Widget);
                    Notice="Restored Studio layout";
                }else Notice="Studio controls enabled. L opens layouts.";
            }
            scene.EnterStudio();active=true;
        }
        public void Leave(){
            if(!active)return;
            // Keep an in-memory layout before resetting Classic. No profile is written,
            // and later Classic shuffles cannot mutate the suspended Studio scene.
            suspendedLayout=scene.LayoutText();suspendedName=ActiveName;suspendedPane=scene.Selected.Widget.Name;
            Panes.Close();scene.LeaveStudio();active=false;ActiveName=null;Notice=null;Editing=false;Resizing=false;
        }
        public void Pin(){Notice=scene.TogglePin();}
        public void Gpu(){scene.ShowGpu();Notice="GPU activity selected. P pins this pane.";}
        public void Disk(){scene.ShowDisk();Notice="Disk activity selected. P pins this pane.";}
        public void Step(int step){scene.SelectNext(step);Notice=null;}
        public void Refresh(){
            scene.SetRefresh(scene.RefreshSeconds==2?5:scene.RefreshSeconds==5?1:2);
            Notice=scene.RefreshSeconds==5?"Lower-overhead sampling":scene.RefreshSeconds==1?"Faster sampling":"Standard sampling";
        }
        public void Open(bool save){
            Panes.Close();filtering=networking=false;Resizing=false;
            saving=save;Editing=true;confirm=false;index=-1;error="";
            name=save && !LayoutStore.IsExample(ActiveName) ? ActiveName??"": "";
            try{names=store.Names();}catch(IOException){names=new string[0];}catch(UnauthorizedAccessException){names=new string[0];}
            if(!save && names.Length>0){index=0;name=names[index];}
            replaceOnType=name.Length!=0;
        }
        public void Cancel(){Editing=false;Panes.Close();}
        public void OpenPaneEditor(){
            if(!active)return;
            Editing=false;Resizing=false;Panes.Open();
        }
        public bool ApplyPaneEdit(){bool changed=Panes.Apply();Notice=Panes.Message;return changed;}
        public bool RemovePane(){bool changed=Panes.Remove();Notice=Panes.Message;return changed;}
        public bool UndoPaneEdit(){
            if(!active)return false;
            bool changed=Panes.Undo();Notice=Panes.Message;Resizing=false;return changed;
        }
        public void PaneKey(ConsoleKeyInfo key){Panes.Key(key);if(!Panes.IsOpen)Notice=Panes.Message;}
        void Changed(){confirm=false;error="";}
        public void Letter(char c){
            if(networking || char.IsControl(c))return;
            if(replaceOnType)Clear();
            if(name.Length<EditorLimit){name+=c;index=-1;Changed();}
        }
        public void Backspace(){if(networking)return;if(replaceOnType)Clear();else if(name.Length>0)name=name.Substring(0,name.Length-1);index=-1;Changed();}
        public void Clear(){if(networking)return;name="";index=-1;replaceOnType=false;Changed();}
        public void Cycle(int step){
            if(names.Length==0)return;
            index=index<0?(step<0?names.Length-1:0):(index+step+names.Length)%names.Length;
            name=names[index];replaceOnType=true;Changed();
        }
        public void Submit(){
            try{
                if(networking){
                    if(index<0 || index>=networkChoices.Length){error="Choose an adapter from the list.";return;}
                    scene.SetNetwork(networkChoices[index]);Notice="Network: "+scene.Network.Name+" (next sample)";
                }else if(filtering){
                    scene.Processes.Configure(scene.Processes.Sort,name);Notice=name.Length==0?"Showing all processes":"Process filter applied";
                }else if(saving){
                    if(!confirm && store.Exists(name)){error="Replace this saved layout? Press Enter again.";confirm=true;return;}
                    store.Save(name,scene.LayoutText(),confirm);ActiveName=name;Notice="Saved "+name;
                }else{var message=TryLoad(name);if(message!=null){error=message;return;}}
                Editing=false;
            }catch(InvalidDataException e){error=e.Message;}catch(IOException e){error=e.Message;}
            catch(UnauthorizedAccessException e){error=e.Message;}catch(ArgumentException e){error=e.Message;}
        }
        public Grid Panel(int cols,int rows){
            if(Panes.IsOpen)return Panes.Panel(cols,rows);
            int width=Math.Max(1,Math.Min(68,cols)),height=Math.Max(1,Math.Min(8,rows));
            var g=new Grid(width,height);
            for(int y=0;y<height;y++)g.Band(y,"",Ink.Bright,Ink.Blue);
            g.Put(1,0,EditorTitle.ToUpperInvariant(),Ink.Gold,Ink.Blue);
            g.Put(1,2,(networking?"":"Name: ")+name+(networking?"":"_"),Ink.Bright,Ink.Blue);
            g.Put(1,4,error.Length>0?error:EditorHint,error.Length>0?Ink.Gold:Ink.White,Ink.Blue);
            g.Put(1,6,networking?"Enter apply   Esc cancel   Up/Down choose":"Enter confirm   Esc cancel   Ctrl+U clear",Ink.White,Ink.Blue);
            return g;
        }
        public string Status(int available=int.MaxValue){
            if(Resizing && ResizeDivider!=null)return "RESIZE "+(ResizeDivider.HorizontalCut?"horizontal":"vertical")+" "+ResizeDivider.Ratio+"% | arrows move | Tab divider | 0 reset | Enter done";
            var p=scene.Selected;
            string details=scene.RefreshSeconds+"s | "+p.Widget.Name+(p.Pinned?" [PIN]":"")+
                (CodeSelected?" | Y: "+(scene.CodePlayback==CodeStyle.Scroll?"scroll":"typing "+scene.CodePlayback.ToString().ToLowerInvariant()):"");
            string name=ActiveName??"";
            int room=Math.Max(0,available-details.Length-3);
            if(name.Length>room)name=room>1?name.Substring(0,room-1)+"…":"";
            return (name.Length>0?name+" | ":"")+details+(String.IsNullOrEmpty(Notice)?"":" | "+Notice);
        }
    }
}
