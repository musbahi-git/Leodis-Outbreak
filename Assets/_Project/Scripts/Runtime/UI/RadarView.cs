using System.Collections.Generic;
using LeodisOutbreak.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LeodisOutbreak.UI
{
    // Main Woodhouse Moor only. Hand-authored schematic, not surveyed geometry.
    // Context: https://www.headingleyleeds.com/parks-woodhouse-moor
    // Demo positions are synthetic; this drawing is NOT a GPS or elimination boundary.
    public sealed class RadarView : VisualElement
    {
        public static readonly Color BackgroundColor = new Color(.07f,.10f,.15f,1);
        public static readonly Color GridColor = new Color(1,1,1,.06f);
        public static readonly Color BoundaryColor = new Color(.90f,.92f,.95f,.35f);
        public static readonly Color HunterColor = new Color(.86f,.17f,.24f,1);
        public static readonly Color RunnerColor = new Color(.99f,.78f,.25f,1);
        public static readonly Color LocalColor = new Color(.52f,.86f,1,1);
        public static readonly Color CapturedColor = new Color(.6f,.6f,.6f,.8f);
        private readonly List<PlayerView> _players = new List<PlayerView>();
        private float _halfExtent=250, _elapsed, _revealInterval=20;
        // Normalised illustrative perimeter and five avenues meeting at the centre.
        private static readonly float[] Perimeter={.12f,.18f,.40f,.09f,.90f,.60f,.80f,.90f,.13f,.78f,.12f,.18f};
        private static readonly float[][] Paths={
            new float[]{.14f,.20f,.48f,.48f,.86f,.61f},
            new float[]{.40f,.12f,.48f,.48f,.78f,.87f},
            new float[]{.48f,.48f,.16f,.76f}
        };
        public RadarView()
        {
            AddToClassList("radar"); style.backgroundColor=BackgroundColor;
            generateVisualContent+=Draw;
            var caption=new Label("WOODHOUSE MOOR / SCHEMATIC");
            caption.pickingMode=PickingMode.Ignore; caption.style.position=Position.Absolute;
            caption.style.left=12; caption.style.top=8; caption.style.fontSize=9;
            caption.style.color=BoundaryColor; Add(caption);
        }
        public void SetView(SessionView view,float revealIntervalSeconds)
        {
            _players.Clear();
            if(view!=null) { _players.AddRange(view.Players); _halfExtent=Mathf.Max(1,view.AreaHalfExtentMetres); _elapsed=view.ElapsedSeconds; }
            _revealInterval=Mathf.Max(1,revealIntervalSeconds); MarkDirtyRepaint();
        }
        private static void Trace(Painter2D p,Vector2 origin,float size,float[] points)
        {
            p.BeginPath(); p.MoveTo(origin+new Vector2(points[0],points[1])*size);
            for(int i=2;i<points.Length;i+=2) p.LineTo(origin+new Vector2(points[i],points[i+1])*size);
        }
        private void Draw(MeshGenerationContext context)
        {
            Rect rect=contentRect; if(rect.width<4||rect.height<4) return;
            Painter2D p=context.painter2D; float size=Mathf.Max(1,Mathf.Min(rect.width,rect.height)-24);
            Vector2 centre=rect.center, origin=centre-Vector2.one*size*.5f;
            Trace(p,origin,size,Perimeter); p.ClosePath();
            p.fillColor=new Color(.12f,.24f,.20f,.55f); p.Fill();
            p.strokeColor=BoundaryColor; p.lineWidth=2; Trace(p,origin,size,Perimeter); p.Stroke();
            // Schematic paths, with paired tree avenues rather than city streets.
            foreach(var path in Paths)
            {
                p.strokeColor=GridColor; p.lineWidth=7; Trace(p,origin,size,path); p.Stroke();
                p.strokeColor=new Color(.76f,.83f,.79f,.55f); p.lineWidth=1.5f; Trace(p,origin,size,path); p.Stroke();
                for(int j=2;j<path.Length;j+=2)
                {
                    Vector2 a=new Vector2(path[j-2],path[j-1]), b=new Vector2(path[j],path[j+1]);
                    Vector2 d=b-a, normal=new Vector2(-d.y,d.x).normalized*.018f;
                    int count=Mathf.Max(2,Mathf.FloorToInt(d.magnitude/.055f));
                    for(int k=1;k<count;k++)
                    {
                        Vector2 q=Vector2.Lerp(a,b,(float)k/count);
                        Tree(p,origin+(q+normal)*size,size*.008f); Tree(p,origin+(q-normal)*size,size*.008f);
                    }
                }
            }
            float scale=size*.5f/_halfExtent;
            foreach(var player in _players) if(player.Role==PlayerRole.Runner) DrawPlayer(p,player,centre,scale);
            foreach(var player in _players) if(player.Role==PlayerRole.Hunter) DrawPlayer(p,player,centre,scale);
        }
        private static void Tree(Painter2D p,Vector2 at,float radius)
        {
            p.strokeColor=new Color(.42f,.65f,.52f,.45f); p.lineWidth=1;
            p.BeginPath(); p.Arc(at,Mathf.Max(1,radius),0,360); p.Stroke();
        }
        private void DrawPlayer(Painter2D painter,PlayerView player,Vector2 centre,float scale)
        {
            if(!player.HasKnownPosition) return;
            Vector2 pos=centre+new Vector2(player.KnownPosition.x,-player.KnownPosition.y)*scale;
            if(player.Role==PlayerRole.Runner)
            {
                float age=Mathf.Clamp01((_elapsed-player.KnownPositionTime)/_revealInterval);
                Color ring=RunnerColor; ring.a=Mathf.Lerp(.55f,.08f,age);
                painter.lineWidth=2; painter.strokeColor=ring; painter.BeginPath();
                painter.Arc(pos,Mathf.Lerp(9,26,age),0,360); painter.Stroke();
                painter.fillColor=RunnerColor; painter.BeginPath(); painter.Arc(pos,6,0,360); painter.Fill(); return;
            }
            float r=player.IsLocalPlayer?9:7;
            if(player.IsLocalPlayer)
            {
                Color halo=LocalColor; halo.a=.25f; painter.fillColor=halo;
                painter.BeginPath(); painter.Arc(pos,18,0,360); painter.Fill();
            }
            painter.fillColor=player.IsLocalPlayer?LocalColor:HunterColor; painter.BeginPath();
            painter.MoveTo(pos+new Vector2(0,-r)); painter.LineTo(pos+new Vector2(r,0));
            painter.LineTo(pos+new Vector2(0,r)); painter.LineTo(pos+new Vector2(-r,0));
            painter.ClosePath(); painter.Fill();
        }
    }
}
