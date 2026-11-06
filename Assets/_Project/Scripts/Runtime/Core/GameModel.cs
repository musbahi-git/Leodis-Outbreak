using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeodisOutbreak.Core
{
    /// <summary>Role of a player in a session. Captured runners become hunters.</summary>
    public enum PlayerRole
    {
        Runner,
        Hunter
    }

    /// <summary>Lifecycle of a game session.</summary>
    public enum SessionPhase
    {
        Lobby,
        Running,
        Ended
    }

    /// <summary>How a session finished. Undecided while the session is in progress.</summary>
    public enum SessionOutcome
    {
        Undecided,
        RunnersSurvived,
        AllRunnersCaptured,
        Aborted
    }

    /// <summary>
    /// Everything the client is allowed to know about one player.
    /// <para>
    /// For runners, <see cref="KnownPosition"/> is the position from the most recent reveal
    /// (stale by design). The client never receives a runner's live position; the backend
    /// decides what is revealed and when. Hunters are visible live because hunting is public.
    /// </para>
    /// </summary>
    [Serializable]
    public struct PlayerView
    {
        public string Id;
        public string DisplayName;
        public PlayerRole Role;
        public bool IsLocalPlayer;
        public bool IsCaptured;

        /// <summary>Position in local metres relative to the play-area origin (x = east, y = north).</summary>
        public Vector2 KnownPosition;

        /// <summary>True when <see cref="KnownPosition"/> is meaningful for this player.</summary>
        public bool HasKnownPosition;

        /// <summary>Session time (seconds since start) at which <see cref="KnownPosition"/> was published.</summary>
        public float KnownPositionTime;
    }

    /// <summary>
    /// Snapshot of a session as seen by this client. Produced by an <see cref="IGameBackend"/>
    /// and consumed by the UI. The UI must never compute game rules from it.
    /// </summary>
    [Serializable]
    public sealed class SessionView
    {
        public SessionPhase Phase = SessionPhase.Lobby;
        public SessionOutcome Outcome = SessionOutcome.Undecided;

        /// <summary>Seconds since the session started.</summary>
        public float ElapsedSeconds;

        /// <summary>Seconds until the session ends by time-out.</summary>
        public float RemainingSeconds;

        /// <summary>Seconds until hunters receive the next location reveal.</summary>
        public float SecondsUntilNextReveal;

        public int RevealsPublished;

        /// <summary>Half extent of the square play area in metres, centred on the origin.</summary>
        public float AreaHalfExtentMetres;

        public List<PlayerView> Players = new List<PlayerView>();
        public List<string> RecentEvents = new List<string>();

        public int CountRole(PlayerRole role)
        {
            int n = 0;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].Role == role && !Players[i].IsCaptured)
                {
                    n++;
                }
            }

            return n;
        }

        public PlayerView? LocalPlayer
        {
            get
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].IsLocalPlayer)
                    {
                        return Players[i];
                    }
                }

                return null;
            }
        }
    }

    /// <summary>Tunable settings for one session.</summary>
    [Serializable]
    public sealed class SessionConfig
    {
        [Min(30f)] public float DurationSeconds = 180f;
        [Min(5f)] public float RevealIntervalSeconds = 20f;
        [Range(1, 60)] public int RunnerCount = 6;
        [Range(1, 20)] public int HunterCount = 2;
        [Min(1f)] public float CaptureRadiusMetres = 12f;
        [Min(50f)] public float AreaHalfExtentMetres = 250f;
        public int RandomSeed = 0;
    }

    /// <summary>
    /// Source of truth for a session from the client's point of view.
    /// <para>
    /// The first implementation is <c>LocalDemoBackend</c>, which simulates players on the device.
    /// A networked implementation will later talk to a real server that owns hidden runner
    /// coordinates. The UI depends only on this interface so the swap does not touch UI code.
    /// </para>
    /// </summary>
    public interface IGameBackend
    {
        /// <summary>Raised whenever <see cref="View"/> has changed.</summary>
        event Action<SessionView> ViewChanged;

        SessionView View { get; }

        void StartSession(SessionConfig config);

        /// <summary>Ends the session early. Safe to call in any phase.</summary>
        void EndSession();

        /// <summary>Returns to the lobby, discarding the finished session.</summary>
        void ResetToLobby();

        /// <summary>Advance time. Called once per frame with unscaled delta time.</summary>
        void Tick(float deltaSeconds);
    }
}
