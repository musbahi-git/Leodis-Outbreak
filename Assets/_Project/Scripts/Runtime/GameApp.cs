using LeodisOutbreak.Core;
using LeodisOutbreak.Demo;
using LeodisOutbreak.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace LeodisOutbreak
{
    /// <summary>
    /// Entry point of the app. Owns the backend instance and binds the UI Toolkit document
    /// (Bootstrap.uxml) to it. One instance lives in the Bootstrap scene on the "App" object,
    /// created by the editor menu Leodis > Setup > Rebuild Bootstrap Scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameApp : MonoBehaviour
    {
        [Header("Demo session")]
        [SerializeField] private SessionConfig _demoConfig = new SessionConfig();

        [Header("Behaviour")]
        [Tooltip("Delay between tapping Start and the HUD appearing, so the 'Started' status is visible.")]
        [SerializeField, Range(0f, 2f)] private float _startTransitionSeconds = 0.7f;

        private IGameBackend _backend;
        private UIDocument _document;
        private VisualElement _root;
        private bool _bound;

        // Home
        private VisualElement _homeScreen;
        private Label _statusLabel;
        private Button _startButton;

        // HUD
        private VisualElement _hudScreen;
        private Label _hudPhase;
        private Label _hudRole;
        private Label _hudTimer;
        private Label _statRunners;
        private Label _statHunters;
        private Label _statReveal;
        private VisualElement _eventLog;
        private Button _endButton;
        private RadarView _radar;

        // End
        private VisualElement _endScreen;
        private Label _endResult;
        private Label _endSummary;
        private Button _homeButton;

        private VisualElement _safeArea;
        private ScreenSwitcher _screens;
        private Vector2Int _lastScreenSize;

        public IGameBackend Backend => _backend;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _document = GetComponent<UIDocument>();
            _backend = new LocalDemoBackend();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Start()
        {
            // UIDocument may build its tree after our OnEnable on first load; bind again if needed.
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (!_bound)
            {
                TryBind();
                return;
            }

            _backend.Tick(Time.unscaledDeltaTime);
            ApplySafeAreaIfChanged();
        }

        // ------------------------------------------------------------------ binding

        private void TryBind()
        {
            if (_bound || _document == null)
            {
                return;
            }

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                return;
            }

            _safeArea = _root.Q<VisualElement>("safe-area");

            _homeScreen = _root.Q<VisualElement>("home-screen");
            _statusLabel = _root.Q<Label>("status-label");
            _startButton = _root.Q<Button>("start-button");

            _hudScreen = _root.Q<VisualElement>("hud-screen");
            _hudPhase = _root.Q<Label>("hud-phase");
            _hudRole = _root.Q<Label>("hud-role");
            _hudTimer = _root.Q<Label>("hud-timer");
            _statRunners = _root.Q<Label>("stat-runners");
            _statHunters = _root.Q<Label>("stat-hunters");
            _statReveal = _root.Q<Label>("stat-reveal");
            _eventLog = _root.Q<VisualElement>("event-log");
            _endButton = _root.Q<Button>("end-button");

            _endScreen = _root.Q<VisualElement>("end-screen");
            _endResult = _root.Q<Label>("end-result");
            _endSummary = _root.Q<Label>("end-summary");
            _homeButton = _root.Q<Button>("home-button");

            if (_homeScreen == null || _statusLabel == null || _startButton == null || _hudScreen == null || _endScreen == null)
            {
                Debug.LogError("GameApp: Bootstrap.uxml is missing required elements. Re-run Leodis > Setup > Rebuild Bootstrap Scene.", this);
                enabled = false;
                return;
            }

            VisualElement radarHost = _root.Q<VisualElement>("radar-host");
            if (radarHost != null)
            {
                _radar = new RadarView();
                radarHost.Add(_radar);
            }

            _screens = new ScreenSwitcher(_homeScreen, _hudScreen, _endScreen);

            _startButton.clicked += OnStartClicked;
            if (_endButton != null)
            {
                _endButton.clicked += OnEndClicked;
            }

            if (_homeButton != null)
            {
                _homeButton.clicked += OnHomeClicked;
            }

            _backend.ViewChanged += OnViewChanged;
            _bound = true;

            _statusLabel.text = "Ready";
            _lastScreenSize = Vector2Int.zero;
            Render(_backend.View);
        }

        private void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            _startButton.clicked -= OnStartClicked;
            if (_endButton != null)
            {
                _endButton.clicked -= OnEndClicked;
            }

            if (_homeButton != null)
            {
                _homeButton.clicked -= OnHomeClicked;
            }

            _backend.ViewChanged -= OnViewChanged;
            _bound = false;
        }

        // ------------------------------------------------------------------ input

        private void OnStartClicked()
        {
            if (_backend.View.Phase != SessionPhase.Lobby)
            {
                return;
            }

            // First-milestone behaviour: the button changes the status label to "Started".
            _statusLabel.text = "Started";
            _statusLabel.AddToClassList("status-card__value--started");
            _startButton.SetEnabled(false);

            long delayMs = (long)(_startTransitionSeconds * 1000f);
            _root.schedule.Execute(() =>
            {
                _backend.StartSession(_demoConfig);
                _startButton.SetEnabled(true);
            }).StartingIn(delayMs);
        }

        private void OnEndClicked()
        {
            _backend.EndSession();
        }

        private void OnHomeClicked()
        {
            _backend.ResetToLobby();
        }

        // ------------------------------------------------------------------ rendering

        private void OnViewChanged(SessionView view)
        {
            Render(view);
        }

        private void Render(SessionView view)
        {
            switch (view.Phase)
            {
                case SessionPhase.Lobby:
                    _statusLabel.text = "Ready";
                    _statusLabel.RemoveFromClassList("status-card__value--started");
                    _screens.Show(_homeScreen);
                    break;

                case SessionPhase.Running:
                    RenderHud(view);
                    _screens.Show(_hudScreen);
                    break;

                case SessionPhase.Ended:
                    RenderHud(view);
                    RenderEnd(view);
                    _screens.Show(_endScreen);
                    break;
            }
        }

        private void RenderHud(SessionView view)
        {
            PlayerView? local = view.LocalPlayer;
            string role = local.HasValue ? local.Value.Role.ToString() : "Spectator";

            if (_hudRole != null)
            {
                _hudRole.text = "You are a " + role;
            }

            if (_hudPhase != null)
            {
                _hudPhase.text = view.Phase == SessionPhase.Running ? "HUNT IN PROGRESS" : "SESSION OVER";
            }

            if (_hudTimer != null)
            {
                _hudTimer.text = LocalDemoBackend.FormatClock(view.RemainingSeconds);
                _hudTimer.EnableInClassList("hud__timer--urgent", view.RemainingSeconds <= 30f && view.Phase == SessionPhase.Running);
            }

            if (_statRunners != null)
            {
                _statRunners.text = view.CountRole(PlayerRole.Runner).ToString();
            }

            if (_statHunters != null)
            {
                _statHunters.text = view.CountRole(PlayerRole.Hunter).ToString();
            }

            if (_statReveal != null)
            {
                _statReveal.text = Mathf.CeilToInt(view.SecondsUntilNextReveal) + " s";
            }

            if (_radar != null)
            {
                _radar.SetView(view, _demoConfig.RevealIntervalSeconds);
            }

            RenderEventLog(view);
        }

        private void RenderEventLog(SessionView view)
        {
            if (_eventLog == null)
            {
                return;
            }

            // Reuse existing labels to avoid allocations every frame.
            while (_eventLog.childCount < view.RecentEvents.Count)
            {
                var label = new Label();
                label.AddToClassList("event-log__line");
                _eventLog.Add(label);
            }

            while (_eventLog.childCount > view.RecentEvents.Count)
            {
                _eventLog.RemoveAt(_eventLog.childCount - 1);
            }

            for (int i = 0; i < view.RecentEvents.Count; i++)
            {
                var label = (Label)_eventLog[i];
                if (label.text != view.RecentEvents[i])
                {
                    label.text = view.RecentEvents[i];
                }

                label.EnableInClassList("event-log__line--latest", i == 0);
            }
        }

        private void RenderEnd(SessionView view)
        {
            if (_endResult == null || _endSummary == null)
            {
                return;
            }

            switch (view.Outcome)
            {
                case SessionOutcome.RunnersSurvived:
                    _endResult.text = "Runners survive";
                    break;
                case SessionOutcome.AllRunnersCaptured:
                    _endResult.text = "Hunters win";
                    break;
                case SessionOutcome.Aborted:
                    _endResult.text = "Session ended";
                    break;
                default:
                    _endResult.text = "Session over";
                    break;
            }

            int captured = 0;
            for (int i = 0; i < view.Players.Count; i++)
            {
                if (view.Players[i].IsCaptured)
                {
                    captured++;
                }
            }

            _endSummary.text =
                LocalDemoBackend.FormatClock(view.ElapsedSeconds) + " played  ·  " +
                captured + " captured  ·  " +
                view.CountRole(PlayerRole.Runner) + " still free  ·  " +
                view.RevealsPublished + " reveals";
        }

        // ------------------------------------------------------------------ safe area

        private void ApplySafeAreaIfChanged()
        {
            if (_safeArea == null)
            {
                return;
            }

            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _lastScreenSize)
            {
                return;
            }

            float panelWidth = _root.resolvedStyle.width;
            float panelHeight = _root.resolvedStyle.height;
            if (panelWidth <= 0f || panelHeight <= 0f || size.x <= 0 || size.y <= 0)
            {
                return; // Layout not ready yet; try again next frame.
            }

            _lastScreenSize = size;

            Rect safe = Screen.safeArea;
            float scaleX = panelWidth / size.x;
            float scaleY = panelHeight / size.y;

            _safeArea.style.paddingLeft = safe.xMin * scaleX;
            _safeArea.style.paddingRight = (size.x - safe.xMax) * scaleX;
            _safeArea.style.paddingTop = (size.y - safe.yMax) * scaleY;
            _safeArea.style.paddingBottom = safe.yMin * scaleY;
        }
    }
}
