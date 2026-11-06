using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LeodisOutbreak.UI
{
    /// <summary>
    /// Shows exactly one full-screen panel at a time with a USS-driven fade/slide transition.
    /// Hidden screens are set to display:none after their transition so they cannot be tapped.
    /// </summary>
    public sealed class ScreenSwitcher
    {
        private const string HiddenClass = "screen--hidden";

        private readonly List<VisualElement> _screens = new List<VisualElement>();
        private VisualElement _current;

        public ScreenSwitcher(params VisualElement[] screens)
        {
            foreach (VisualElement screen in screens)
            {
                if (screen == null)
                {
                    continue;
                }

                _screens.Add(screen);
                screen.RegisterCallback<TransitionEndEvent>(OnTransitionEnd);

                if (screen.ClassListContains(HiddenClass))
                {
                    screen.style.display = DisplayStyle.None;
                }
                else
                {
                    _current = screen;
                }
            }
        }

        public VisualElement Current => _current;

        public void Show(VisualElement target)
        {
            if (target == null || target == _current)
            {
                return;
            }

            foreach (VisualElement screen in _screens)
            {
                if (screen == target)
                {
                    continue;
                }

                if (!screen.ClassListContains(HiddenClass))
                {
                    screen.AddToClassList(HiddenClass);
                }
            }

            _current = target;
            target.style.display = DisplayStyle.Flex;

            // Let layout run once with the hidden style so the transition has a start value.
            target.schedule.Execute(() => target.RemoveFromClassList(HiddenClass)).StartingIn(16);
        }

        private static void OnTransitionEnd(TransitionEndEvent evt)
        {
            var screen = evt.currentTarget as VisualElement;
            if (screen != null && screen.ClassListContains(HiddenClass))
            {
                screen.style.display = DisplayStyle.None;
            }
        }
    }
}
