using System;
using System.Collections.Generic;
using LeodisOutbreak.Core;
using UnityEngine;
using Random = System.Random;

namespace LeodisOutbreak.Demo
{
    /// <summary>
    /// On-device simulation of a session with synthetic players. No GPS, no network.
    /// <para>
    /// It deliberately mirrors the trust boundary the real backend will enforce:
    /// true runner positions live only inside this class, and the <see cref="SessionView"/>
    /// handed to the UI contains runner positions from the last reveal only.
    /// Synthetic hunters are also restricted to revealed data when choosing where to go.
    /// </para>
    /// </summary>
    public sealed class LocalDemoBackend : IGameBackend
    {
        private sealed class SimPlayer
        {
            public string Id;
            public string Name;
            public PlayerRole Role;
            public bool IsLocal;
            public bool Captured;
            public Vector2 TruePosition;
            public float Heading;          // radians
            public float SpeedMetresPerSecond;
            public Vector2 RevealedPosition;
            public bool HasReveal;
            public float RevealTime;
            public float NextHeadingChange;
        }

        private static readonly string[] RunnerNames =
        {
            "Otley", "Hyde", "Woodhouse", "Headingley", "Kirkstall", "Roundhay",
            "Chapeltown", "Holbeck", "Armley", "Burley", "Meanwood", "Harehills"
        };

        private static readonly string[] HunterNames =
        {
            "Parkinson", "Brotherton", "Roger Stevens", "Laidlaw", "Clothworkers", "Maurice Keyworth"
        };

        private const int MaxEvents = 6;

        private readonly List<SimPlayer> _players = new List<SimPlayer>();
        private readonly SessionView _view = new SessionView();
        private SessionConfig _config = new SessionConfig();
        private Random _random = new Random();
        private float _elapsed;
        private float _nextRevealAt;

        public event Action<SessionView> ViewChanged;

        public SessionView View => _view;

        public void StartSession(SessionConfig config)
        {
            _config = config ?? new SessionConfig();
            _random = _config.RandomSeed == 0 ? new Random() : new Random(_config.RandomSeed);
            _players.Clear();
            _elapsed = 0f;
            _nextRevealAt = _config.RevealIntervalSeconds;

            float half = _config.AreaHalfExtentMetres;

            for (int i = 0; i < _config.HunterCount; i++)
            {
                _players.Add(new SimPlayer
                {
                    Id = "hunter-" + (i + 1),
                    Name = HunterNames[i % HunterNames.Length],
                    Role = PlayerRole.Hunter,
                    IsLocal = i == 0,
                    TruePosition = RandomPointInRadius(Vector2.zero, half * 0.1f),
                    Heading = RandomAngle(),
                    SpeedMetresPerSecond = Lerp(2.0f, 2.6f, NextFloat())
                });
            }

            for (int i = 0; i < _config.RunnerCount; i++)
            {
                _players.Add(new SimPlayer
                {
                    Id = "runner-" + (i + 1),
                    Name = RunnerNames[i % RunnerNames.Length],
                    Role = PlayerRole.Runner,
                    TruePosition = RandomPointInRing(Vector2.zero, half * 0.35f, half * 0.85f),
                    Heading = RandomAngle(),
                    SpeedMetresPerSecond = Lerp(1.2f, 2.0f, NextFloat())
                });
            }

            _view.Phase = SessionPhase.Running;
            _view.Outcome = SessionOutcome.Undecided;
            _view.RevealsPublished = 0;
            _view.AreaHalfExtentMetres = half;
            _view.RecentEvents.Clear();
            PushEvent("Session started. First reveal in " + Mathf.RoundToInt(_config.RevealIntervalSeconds) + " s.");

            // The first reveal happens immediately so hunters have something to chase.
            PublishReveal();
            _nextRevealAt = _config.RevealIntervalSeconds;

            RebuildView();
            RaiseViewChanged();
        }

        public void EndSession()
        {
            if (_view.Phase != SessionPhase.Running)
            {
                return;
            }

            FinishSession(SessionOutcome.Aborted, "Session ended by the player.");
        }

        public void ResetToLobby()
        {
            _players.Clear();
            _view.Phase = SessionPhase.Lobby;
            _view.Outcome = SessionOutcome.Undecided;
            _view.ElapsedSeconds = 0f;
            _view.RemainingSeconds = 0f;
            _view.SecondsUntilNextReveal = 0f;
            _view.RevealsPublished = 0;
            _view.Players.Clear();
            _view.RecentEvents.Clear();
            RaiseViewChanged();
        }

        public void Tick(float deltaSeconds)
        {
            if (_view.Phase != SessionPhase.Running || deltaSeconds <= 0f)
            {
                return;
            }

            // Clamp so a long hitch (app resume, debugger pause) cannot teleport players.
            deltaSeconds = Mathf.Min(deltaSeconds, 0.25f);
            _elapsed += deltaSeconds;

            MoveRunners(deltaSeconds);
            MoveHunters(deltaSeconds);
            ResolveCaptures();

            if (_view.Phase != SessionPhase.Running)
            {
                return; // ResolveCaptures may have finished the session.
            }

            if (_elapsed >= _nextRevealAt)
            {
                PublishReveal();
                _nextRevealAt += _config.RevealIntervalSeconds;
            }

            if (_elapsed >= _config.DurationSeconds)
            {
                FinishSession(SessionOutcome.RunnersSurvived, "Time is up. The remaining runners survived.");
                return;
            }

            RebuildView();
            RaiseViewChanged();
        }

        // ------------------------------------------------------------------ simulation

        private void MoveRunners(float dt)
        {
            float half = _config.AreaHalfExtentMetres;

            foreach (SimPlayer p in _players)
            {
                if (p.Role != PlayerRole.Runner || p.Captured)
                {
                    continue;
                }

                p.NextHeadingChange -= dt;
                if (p.NextHeadingChange <= 0f)
                {
                    p.Heading += Lerp(-1.2f, 1.2f, NextFloat());
                    p.NextHeadingChange = Lerp(2f, 6f, NextFloat());
                }

                // Runners drift away from the nearest hunter they can see (hunters are public).
                SimPlayer nearestHunter = Nearest(p.TruePosition, PlayerRole.Hunter, true);
                if (nearestHunter != null)
                {
                    Vector2 away = p.TruePosition - nearestHunter.TruePosition;
                    float dist = away.magnitude;
                    if (dist < 60f && dist > 0.01f)
                    {
                        float fleeHeading = Mathf.Atan2(away.y, away.x);
                        p.Heading = Mathf.LerpAngle(p.Heading * Mathf.Rad2Deg, fleeHeading * Mathf.Rad2Deg, 0.6f) * Mathf.Deg2Rad;
                    }
                }

                Step(p, dt, half);
            }
        }

        private void MoveHunters(float dt)
        {
            float half = _config.AreaHalfExtentMetres;

            foreach (SimPlayer hunter in _players)
            {
                if (hunter.Role != PlayerRole.Hunter)
                {
                    continue;
                }

                // Hunters only know revealed positions, exactly like the real client will.
                SimPlayer target = null;
                float best = float.MaxValue;
                foreach (SimPlayer r in _players)
                {
                    if (r.Role != PlayerRole.Runner || r.Captured || !r.HasReveal)
                    {
                        continue;
                    }

                    float d = (r.RevealedPosition - hunter.TruePosition).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        target = r;
                    }
                }

                if (target != null)
                {
                    Vector2 to = target.RevealedPosition - hunter.TruePosition;
                    if (to.sqrMagnitude > 4f)
                    {
                        hunter.Heading = Mathf.Atan2(to.y, to.x);
                    }
                    else
                    {
                        // Arrived at a stale reveal: search in a slow circle until the next reveal.
                        hunter.Heading += 1.5f * dt;
                    }
                }
                else
                {
                    hunter.NextHeadingChange -= dt;
                    if (hunter.NextHeadingChange <= 0f)
                    {
                        hunter.Heading = RandomAngle();
                        hunter.NextHeadingChange = Lerp(3f, 7f, NextFloat());
                    }
                }

                Step(hunter, dt, half);
            }
        }

        private void ResolveCaptures()
        {
            float r2 = _config.CaptureRadiusMetres * _config.CaptureRadiusMetres;

            foreach (SimPlayer runner in _players)
            {
                if (runner.Role != PlayerRole.Runner || runner.Captured)
                {
                    continue;
                }

                foreach (SimPlayer hunter in _players)
                {
                    if (hunter.Role != PlayerRole.Hunter)
                    {
                        continue;
                    }

                    if ((hunter.TruePosition - runner.TruePosition).sqrMagnitude <= r2)
                    {
                        // Rule from the brief: captured runners become hunters.
                        runner.Captured = true;
                        runner.Role = PlayerRole.Hunter;
                        runner.HasReveal = false;
                        runner.SpeedMetresPerSecond = Lerp(2.0f, 2.6f, NextFloat());
                        PushEvent(hunter.Name + " captured " + runner.Name + ". " + runner.Name + " now hunts.");
                        break;
                    }
                }
            }

            int runnersLeft = 0;
            foreach (SimPlayer p in _players)
            {
                if (p.Role == PlayerRole.Runner && !p.Captured)
                {
                    runnersLeft++;
                }
            }

            if (runnersLeft == 0)
            {
                FinishSession(SessionOutcome.AllRunnersCaptured, "Every runner has been captured. Hunters win.");
            }
        }

        private void PublishReveal()
        {
            int revealed = 0;
            foreach (SimPlayer p in _players)
            {
                if (p.Role == PlayerRole.Runner && !p.Captured)
                {
                    p.RevealedPosition = p.TruePosition;
                    p.HasReveal = true;
                    p.RevealTime = _elapsed;
                    revealed++;
                }
            }

            _view.RevealsPublished++;
            PushEvent("Reveal " + _view.RevealsPublished + ": " + revealed + " runner position(s) published.");
        }

        private void FinishSession(SessionOutcome outcome, string message)
        {
            _view.Phase = SessionPhase.Ended;
            _view.Outcome = outcome;
            PushEvent(message);
            RebuildView();
            RaiseViewChanged();
        }

        private void Step(SimPlayer p, float dt, float half)
        {
            Vector2 dir = new Vector2(Mathf.Cos(p.Heading), Mathf.Sin(p.Heading));
            Vector2 next = p.TruePosition + dir * (p.SpeedMetresPerSecond * dt);

            // Keep inside the square play area by reflecting the heading at the edges.
            if (next.x < -half || next.x > half)
            {
                p.Heading = Mathf.PI - p.Heading;
                next.x = Mathf.Clamp(next.x, -half, half);
            }

            if (next.y < -half || next.y > half)
            {
                p.Heading = -p.Heading;
                next.y = Mathf.Clamp(next.y, -half, half);
            }

            p.TruePosition = next;
        }

        private SimPlayer Nearest(Vector2 from, PlayerRole role, bool excludeCaptured)
        {
            SimPlayer best = null;
            float bestD = float.MaxValue;
            foreach (SimPlayer p in _players)
            {
                if (p.Role != role || (excludeCaptured && p.Captured && role == PlayerRole.Runner))
                {
                    continue;
                }

                float d = (p.TruePosition - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ view

        private void RebuildView()
        {
            _view.ElapsedSeconds = _elapsed;
            _view.RemainingSeconds = Mathf.Max(0f, _config.DurationSeconds - _elapsed);
            _view.SecondsUntilNextReveal = Mathf.Max(0f, _nextRevealAt - _elapsed);
            _view.AreaHalfExtentMetres = _config.AreaHalfExtentMetres;

            _view.Players.Clear();
            foreach (SimPlayer p in _players)
            {
                var pv = new PlayerView
                {
                    Id = p.Id,
                    DisplayName = p.Name,
                    Role = p.Role,
                    IsLocalPlayer = p.IsLocal,
                    IsCaptured = p.Captured
                };

                if (p.Role == PlayerRole.Hunter)
                {
                    // Hunters are public: live position.
                    pv.KnownPosition = p.TruePosition;
                    pv.HasKnownPosition = true;
                    pv.KnownPositionTime = _elapsed;
                }
                else
                {
                    // Runners: only the last reveal leaves this class.
                    pv.KnownPosition = p.RevealedPosition;
                    pv.HasKnownPosition = p.HasReveal;
                    pv.KnownPositionTime = p.RevealTime;
                }

                _view.Players.Add(pv);
            }
        }

        private void PushEvent(string message)
        {
            string stamp = FormatClock(_elapsed);
            _view.RecentEvents.Insert(0, stamp + "  " + message);
            while (_view.RecentEvents.Count > MaxEvents)
            {
                _view.RecentEvents.RemoveAt(_view.RecentEvents.Count - 1);
            }
        }

        private void RaiseViewChanged()
        {
            ViewChanged?.Invoke(_view);
        }

        public static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        // ------------------------------------------------------------------ random helpers

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        private float RandomAngle()
        {
            return NextFloat() * Mathf.PI * 2f;
        }

        private Vector2 RandomPointInRadius(Vector2 centre, float radius)
        {
            float a = RandomAngle();
            float r = radius * Mathf.Sqrt(NextFloat());
            return centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        private Vector2 RandomPointInRing(Vector2 centre, float inner, float outer)
        {
            float a = RandomAngle();
            float r = Lerp(inner, outer, NextFloat());
            return centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
