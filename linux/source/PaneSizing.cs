using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Technobabble {
    internal sealed partial class Scene {
        public IEnumerable<Pane> Dividers {get{return DividersIn(Root);}}
        static IEnumerable<Pane> DividersIn(Pane pane){
            if(pane.Leaf)yield break;
            yield return pane;
            foreach(var child in DividersIn(pane.A))yield return child;
            foreach(var child in DividersIn(pane.B))yield return child;
        }
        public Pane DividerAt(Point point){
            return Dividers.Reverse().FirstOrDefault(p=>p.Bounds.Contains(point) &&
                (p.HorizontalCut?point.Y==p.A.Bounds.Bottom:point.X==p.A.Bounds.Right));
        }
        public Pane NearestDivider(){return Dividers.LastOrDefault(p=>p.Leaves().Contains(Selected));}
        public void SetRatio(Pane divider,int ratio){
            if(divider==null || !Dividers.Contains(divider))return;
            divider.Ratio=Math.Max(10,Math.Min(90,ratio));Root.Arrange(bounds);
        }
        public void DragDivider(Pane divider,Point point){
            int available=(divider.HorizontalCut?divider.Bounds.Height:divider.Bounds.Width)-1;
            if(available<2)return;
            int pos=divider.HorizontalCut?point.Y-divider.Bounds.Top:point.X-divider.Bounds.Left;
            SetRatio(divider,(int)Math.Round(100.0*pos/available));
        }
    }
}
