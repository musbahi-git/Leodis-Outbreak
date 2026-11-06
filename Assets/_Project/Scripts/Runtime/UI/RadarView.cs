using System.Collections.Generic;
using LeodisOutbreak.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LeodisOutbreak.UI
{
    // Hand-authored schematic of central Leeds, not surveyed geometry or a navigation map.
    // Named streets/context: https://www.firstbus.co.uk/uploads/maps/1.%20Leeds%20City%20Centre%20Map.pdf
    // No third-party map artwork is embedded. Demo coordinates remain synthetic.
    public sealed class RadarView : VisualElement
    {
        public static readonly Color BackgroundColor = new Color(.07f,.10f,.15f,1f);
        public static readonly Color GridColor = new Color(1f,1f,1f,.06f);
        public static readonly Color BoundaryColor = new Color(.90f,.92f,.95f,.35f);
        public static readonly Color HunterColor = new Color(.86f,.17f,.24f,1f);
        public static readonly Color RunnerColor = new Color(.99f,.78f,.25f,1f);
        public static readonly Color LocalColor = new Color(.52f,.86f,1f,1f);
        public static readonly Color CapturedColor = new Color(.6f,.6f,.6f,.8f);
        private readonly List<PlayerView> _players = new List<PlayerView>();
        private float _halfExtent = 250f, _elapsed, _revealInterval = 20f;
        // Normalised north-up schematic: x east, y south.
        private static readonly float[][] Streets = {
            new float[]{.06f,.31f,.25f,.30f,.43f,.31f,.65f,.33f,.82f,.34f,.94f,.40f}, // Westgate, Headrow, Eastgate
            new float[]{.43f,.31f,.40f,.49f,.37f,.64f}, // Park Row
            new float[]{.37f,.64f,.53f,.65f,.64f,.65f}, // Boar Lane
            new float[]{.65f,.33f,.63f,.49f,.64f,.65f,.65f,.77f,.66f,.85f}, // Briggate, Bridge End
            new float[]{.75f,.23f,.74f,.34f,.73f,.50f,.73f,.60f}, // Vicar Lane
            new float[]{.64f,.65f,.73f,.60f,.83f,.64f,.94f,.68f}, // Kirkgate
            new float[]{.06f,.58f,.22f,.62f,.37f,.64f}, // Wellington Street
            new float[]{.37f,.64f,.38f,.73f,.40f,.85f,.40f,.94f}, // Bishopgate, Neville Street
            new float[]{.53f,.65f,.52f,.49f,.52f,.31f,.50f,.19f}, // Albion Street
            new float[]{.33f,.32f,.29f,.18f,.26f,.06f}, // Calverley Street
            new float[]{.43f,.31f,.43f,.18f,.49f,.06f}, // Woodhouse Lane
            new float[]{.33f,.13f,.47f,.14f,.59f,.16f}, // Great George Street
            new float[]{.50f,.19f,.63f,.22f,.75f,.23f,.81f,.18f}, // Merrion Street
            new float[]{.65f,.33f,.64f,.21f,.61f,.09f}, // New Briggate
            new float[]{.73f,.50f,.84f,.49f,.94f,.51f}, // York Street
            new float[]{.64f,.65f,.70f,.71f,.81f,.74f,.94f,.75f}, // Call Lane, The Calls
            new float[]{.24f,.33f,.23f,.46f,.25f,.61f}, // East Parade, King Street
            new float[]{.23f,.46f,.40f,.49f,.52f,.49f,.63f,.49f}, // Infirmary, Bond, Commercial streets
            new float[]{.09f,.40f,.18f,.40f,.18f,.48f,.09f,.48f,.09f,.40f}, // Park Square
            new float[]{.82f,.34f,.85f,.25f,.88f,.12f}, // Regent Street
            new float[]{.85f,.51f,.88f,.63f,.91f,.82f,.92f,.94f}, // Marsh Lane
            new float[]{.91f,.82f,.79f,.85f,.66f,.85f,.51f,.88f,.40f,.85f}, // Southbank connections
            new float[]{.79f,.85f,.82f,.94f} // Crown Point
        };
        private static readonly float[] River = {.03f,.76f,.17f,.75f,.29f,.81f,.40f,.81f,.50f,.78f,.64f,.78f,.74f,.80f,.86f,.86f,.97f,.91f};
        public RadarView()
        {
            AddToClassList("radar"); style.backgroundColor = BackgroundColor;
            generateVisualContent += Draw;
            var caption = new Label("LEEDS CITY CENTRE / SCHEMATIC");
            caption.pickingMode = PickingMode.Ignore;
            caption.style.position = Position.Absolute; caption.style.left = 12; caption.style.top = 8;
            caption.style.fontSize = 9; caption.style.color = BoundaryColor; Add(caption);
        }
        public void SetView(SessionView view, float revealIntervalSeconds)
        {
            _players.Clear();
            if (view != null) { _players.AddRange(view.Players); _halfExtent = Mathf.Max(1,view.AreaHalfExtentMetres); _elapsed = view.ElapsedSeconds; }
            _revealInterval = Mathf.Max(1,revealIntervalSeconds); MarkDirtyRepaint();
        }
        private static void Path(Painter2D p, Vector2 origin, float size, float[] points)
        {
            p.BeginPath(); p.MoveTo(origin + new Vector2(points[0],points[1])*size);
            for(int i=2;i<points.Length;i+=2) p.LineTo(origin+new Vector2(points[i],points[i+1])*size);
            p.Stroke();
        }
        private void Draw(MeshGenerationContext context)
        {
            Rect rect=contentRect; if(rect.width<4 || rect.height<4) return;
            Painter2D p=context.painter2D; float size=Mathf.Max(1,Mathf.Min(rect.width,rect.height)-16);
            Vector2 centre=rect.center, origin=centre-Vector2.one*size*.5f;
            p.strokeColor=new Color(.18f,.39f,.49f,.5f); p.lineWidth=11; Path(p,origin,size,River);
            p.strokeColor=new Color(.38f,.68f,.77f,.35f); p.lineWidth=2; Path(p,origin,size,River);
            // Soft road casing beneath fine vector street lines.
            p.strokeColor=GridColor; p.lineWidth=6; foreach(var road in Streets) Path(p,origin,size,road);
            p.strokeColor=BoundaryColor; p.lineWidth=1.2f; foreach(var road in Streets) Path(p,origin,size,road);
            // Station tracks south-west of City Square.
            p.strokeColor=new Color(.65f,.70f,.77f,.22f); p.lineWidth=1;
            for(int i=0;i<4;i++) Path(p,origin,size,new float[]{.05f,.70f+i*.012f,.22f,.72f+i*.012f,.36f,.71f+i*.012f});
            float scale=size*.5f/_halfExtent;
            foreach(var player in _players) if(player.Role==PlayerRole.Runner) DrawPlayer(p,player,centre,scale);
            foreach(var player in _players) if(player.Role==PlayerRole.Hunter) DrawPlayer(p,player,centre,scale);
        }
        private void DrawPlayer(Painter2D painter, PlayerView player, Vector2 centre, float scale)
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
