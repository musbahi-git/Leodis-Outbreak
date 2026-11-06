using System.Collections.Generic;
using LeodisOutbreak.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LeodisOutbreak.UI
{
    /// <summary>
    /// Stylised top-down map of the play area drawn with UI Toolkit's vector API.
    /// Shows hunters live and runners at their last revealed position (with a fading ring
    /// that indicates how stale the reveal is). This is a placeholder for the 3D map; it keeps
    /// the same data contract (<see cref="SessionView"/>) so the swap is a view change only.
    /// </summary>
    public sealed class RadarView : VisualElement
    {
        public static readonly Color BackgroundColor = new Color(0.07f, 0.10f, 0.15f, 1f);
        public static readonly Color GridColor = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color BoundaryColor = new Color(0.90f, 0.92f, 0.95f, 0.35f);
        public static readonly Color HunterColor = new Color(0.86f, 0.17f, 0.24f, 1f);
        public static readonly Color RunnerColor = new Color(0.99f, 0.78f, 0.25f, 1f);
        public static readonly Color LocalColor = new Color(0.52f, 0.86f, 1f, 1f);
        public static readonly Color CapturedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);

        private readonly List<PlayerView> _players = new List<PlayerView>();
        private float _halfExtent = 250f;
        private float _elapsed;
        private float _revealInterval = 20f;

        public RadarView()
        {
            AddToClassList("radar");
            style.backgroundColor = BackgroundColor;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetView(SessionView view, float revealIntervalSeconds)
        {
            _players.Clear();
            if (view != null)
            {
                _players.AddRange(view.Players);
                _halfExtent = Mathf.Max(1f, view.AreaHalfExtentMetres);
                _elapsed = view.ElapsedSeconds;
            }

            _revealInterval = Mathf.Max(1f, revealIntervalSeconds);
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width < 4f || rect.height < 4f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 centre = rect.center;
            float scale = (size * 0.5f - 8f) / _halfExtent; // pixels per metre

            DrawGrid(painter, centre, size, scale);
            DrawBoundary(painter, centre, size);

            // Runners first so hunters render on top.
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Role == PlayerRole.Runner)
                {
                    DrawPlayer(painter, _players[i], centre, scale);
                }
            }

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Role == PlayerRole.Hunter)
                {
                    DrawPlayer(painter, _players[i], centre, scale);
                }
            }
        }

        private void DrawGrid(Painter2D painter, Vector2 centre, float size, float scale)
        {
            const float gridMetres = 50f;
            float half = size * 0.5f - 8f;
            painter.lineWidth = 1f;
            painter.strokeColor = GridColor;
            painter.BeginPath();

            for (float m = gridMetres; m * scale < half; m += gridMetres)
            {
                float d = m * scale;
                painter.MoveTo(new Vector2(centre.x + d, centre.y - half));
                painter.LineTo(new Vector2(centre.x + d, centre.y + half));
                painter.MoveTo(new Vector2(centre.x - d, centre.y - half));
                painter.LineTo(new Vector2(centre.x - d, centre.y + half));
                painter.MoveTo(new Vector2(centre.x - half, centre.y + d));
                painter.LineTo(new Vector2(centre.x + half, centre.y + d));
                painter.MoveTo(new Vector2(centre.x - half, centre.y - d));
                painter.LineTo(new Vector2(centre.x + half, centre.y - d));
            }

            // Centre cross.
            painter.MoveTo(new Vector2(centre.x, centre.y - half));
            painter.LineTo(new Vector2(centre.x, centre.y + half));
            painter.MoveTo(new Vector2(centre.x - half, centre.y));
            painter.LineTo(new Vector2(centre.x + half, centre.y));
            painter.Stroke();
        }

        private void DrawBoundary(Painter2D painter, Vector2 centre, float size)
        {
            float half = size * 0.5f - 8f;
            painter.lineWidth = 2f;
            painter.strokeColor = BoundaryColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(centre.x - half, centre.y - half));
            painter.LineTo(new Vector2(centre.x + half, centre.y - half));
            painter.LineTo(new Vector2(centre.x + half, centre.y + half));
            painter.LineTo(new Vector2(centre.x - half, centre.y + half));
            painter.ClosePath();
            painter.Stroke();
        }

        private void DrawPlayer(Painter2D painter, PlayerView player, Vector2 centre, float scale)
        {
            if (!player.HasKnownPosition)
            {
                return;
            }

            // Map metres (x east, y north) to pixels (y down).
            Vector2 p = new Vector2(
                centre.x + player.KnownPosition.x * scale,
                centre.y - player.KnownPosition.y * scale);

            if (player.Role == PlayerRole.Runner)
            {
                // Stale reveal: ring grows with age until the next reveal.
                float age = Mathf.Clamp01((_elapsed - player.KnownPositionTime) / _revealInterval);
                float ringRadius = Mathf.Lerp(9f, 26f, age);
                Color ring = RunnerColor;
                ring.a = Mathf.Lerp(0.55f, 0.08f, age);

                painter.lineWidth = 2f;
                painter.strokeColor = ring;
                painter.BeginPath();
                painter.Arc(p, ringRadius, 0f, 360f);
                painter.Stroke();

                painter.fillColor = RunnerColor;
                painter.BeginPath();
                painter.Arc(p, 6f, 0f, 360f);
                painter.Fill();
                return;
            }

            Color fill = player.IsLocalPlayer ? LocalColor : HunterColor;
            float r = player.IsLocalPlayer ? 9f : 7f;

            if (player.IsLocalPlayer)
            {
                Color halo = LocalColor;
                halo.a = 0.25f;
                painter.fillColor = halo;
                painter.BeginPath();
                painter.Arc(p, 18f, 0f, 360f);
                painter.Fill();
            }

            // Hunters are drawn as diamonds so shape alone distinguishes roles.
            painter.fillColor = fill;
            painter.BeginPath();
            painter.MoveTo(new Vector2(p.x, p.y - r));
            painter.LineTo(new Vector2(p.x + r, p.y));
            painter.LineTo(new Vector2(p.x, p.y + r));
            painter.LineTo(new Vector2(p.x - r, p.y));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
