using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Technobabble {
    internal sealed class Pane {
        public Widget Widget;
        public Pane A,B;
        public bool HorizontalCut,Pinned;
        public int Ratio=50;
        public Rectangle Bounds;
        public bool Leaf { get {return A==null;} }
        public void Arrange(Rectangle r) {
            Bounds=r;if(Leaf)return;
            int extent=HorizontalCut?r.Height:r.Width;
            int available=Math.Max(0,extent-1);
            int first=Ratio==50?available/2:available*Ratio/100;
            if(Ratio!=50 && available>=2)first=Math.Max(1,Math.Min(available-1,first));
            int second=Math.Max(0,extent-first-1);
            if(HorizontalCut){
                A.Arrange(new Rectangle(r.X,r.Y,r.Width,first));
                B.Arrange(new Rectangle(r.X,r.Y+first+1,r.Width,second));
            }else{
                A.Arrange(new Rectangle(r.X,r.Y,first,r.Height));
                B.Arrange(new Rectangle(r.X+first+1,r.Y,second,r.Height));
            }
        }
        public IEnumerable<Pane> Leaves(){
            if(Leaf){yield return this;yield break;}
            foreach(var p in A.Leaves())yield return p;
            foreach(var p in B.Leaves())yield return p;
        }
        public Pane BottomRight(){return Leaf?this:B.BottomRight();}
    }
    internal sealed partial class Scene {
        readonly Random random;
        readonly Machine machine;
        readonly Pictures pictures;
        readonly int initialCount,initialDelay;
        public readonly ProcessWatch Processes=new ProcessWatch();
        int count,delay;
        readonly Queue<int> deck=new Queue<int>();
        public Pane Root;
        public int Generation {get;private set;}
        public double Time {get;private set;}
        double next;
        bool building;
        int added;
        Rectangle bounds;
        public Scene(int count,int delay,Random random,Machine machine,Pictures pictures) {
            this.count=initialCount=count;this.delay=initialDelay=delay;this.random=random;this.machine=machine;this.pictures=pictures;
            Root=new Pane{Widget=Make(random.Next(Widget.ClassicCount))};BeginRound();
        }
        Widget Make(int kind) {return new Widget(kind,random.Next(),machine,pictures,Time,Processes);}
        void BeginRound() {
            var pins=Root.Leaves().Where(p=>p.Pinned).Select(p=>p.Widget.Name).ToList();
            if(pins.Count==0){
                var survivor=Root.BottomRight().Widget;
                Root=new Pane{Widget=survivor};pins.Add(survivor.Name);
            }
            var candidates=Enumerable.Range(0,Widget.ClassicCount).Where(i=>!pins.Contains(Widget.Names[i])).ToList();
            deck.Clear();
            for(int i=candidates.Count-1;i>=0;i--){
                int j=random.Next(i+1);deck.Enqueue(candidates[j]);candidates[j]=candidates[i];
            }
            if(!Root.Leaf)RebuildFreeRegions(Root);
            Root.Arrange(bounds);
            added=Root.Leaves().Count()-1;building=added+1<count && Root.Leaves().Any(p=>!p.Pinned);
            next=Time+(building?.5:delay);Generation++;
        }
        void RebuildFreeRegions(Pane p){
            if(p.Pinned)return;
            if(!p.Leaves().Any(leaf=>leaf.Pinned)){
                p.A=null;p.B=null;p.Ratio=50;p.Widget=Make(deck.Dequeue());return;
            }
            RebuildFreeRegions(p.A);RebuildFreeRegions(p.B);
        }
        public void Shuffle(){BeginRound();}
        public void AdvanceFocused(double dt){Time+=dt;next+=dt;}
        public void Resize(int w,int h){bounds=new Rectangle(0,0,Math.Max(0,w),Math.Max(0,h));Root.Arrange(bounds);}
        public void Advance(double dt){
            Time+=dt;if(Time<next)return;
            if(!building){BeginRound();return;}
            bool cut=added%2==0;
            List<Pane> eligible=Eligible(cut);
            if(eligible.Count==0){cut=!cut;eligible=Eligible(cut);}
            if(eligible.Count>0 && deck.Count>0){
                Pane p=eligible[random.Next(eligible.Count)];
                p.A=new Pane{Widget=p.Widget};p.B=new Pane{Widget=Make(deck.Dequeue())};
                p.Widget=null;p.HorizontalCut=cut;p.Ratio=50;p.Arrange(p.Bounds);added++;
            }
            building=eligible.Count>0 && added+1<count && deck.Count>0;
            next=Time+(building?.2:delay);
        }
        List<Pane> Eligible(bool horizontal){
            return Root.Leaves().Where(p=>!p.Pinned && (horizontal?p.Bounds.Height>=7 && p.Bounds.Width>=10:p.Bounds.Width>=21 && p.Bounds.Height>=3)).ToList();
        }
    }
}
