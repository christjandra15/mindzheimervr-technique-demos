using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Mindzheimer.Portfolio
{
    public enum TrackerState { Inactive, Hover, Calibrated }

    [System.Serializable]
    public class TrackerPointEvent : UnityEvent<PortfolioTrackerPoint> { }

    /// <summary>
    /// A single tracker marker on the humanoid silhouette. Handles hover /
    /// click visuals and exposes an event so the builder's demo sequence
    /// (or a real user) can drive calibration state.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PortfolioTrackerPoint : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public string Label { get; private set; }
        public TrackerState State { get; private set; } = TrackerState.Inactive;

        public TrackerPointEvent OnClicked = new TrackerPointEvent();

        private Image dotImage;
        private Image glowImage;
        private PortfolioUITheme theme;
        private TMP_Text tooltip;

        public void Init(string label, PortfolioUITheme uiTheme, Image dot, Image glow, TMP_Text tooltipText)
        {
            Label     = label;
            theme     = uiTheme;
            dotImage  = dot;
            glowImage = glow;
            tooltip   = tooltipText;

            if (tooltip != null) tooltip.gameObject.SetActive(false);
            ApplyVisual();
        }

        public void SetCalibrated(bool calibrated)
        {
            State = calibrated ? TrackerState.Calibrated : TrackerState.Inactive;
            ApplyVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (State != TrackerState.Calibrated) State = TrackerState.Hover;
            if (tooltip != null)
            {
                tooltip.text = Label;
                tooltip.gameObject.SetActive(true);
            }
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (State != TrackerState.Calibrated) State = TrackerState.Inactive;
            if (tooltip != null) tooltip.gameObject.SetActive(false);
            ApplyVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SetCalibrated(State != TrackerState.Calibrated);
            OnClicked?.Invoke(this);
        }

        private void ApplyVisual()
        {
            if (dotImage == null || theme == null) return;

            Color c;
            switch (State)
            {
                case TrackerState.Calibrated: c = theme.trackerActive; break;
                case TrackerState.Hover:      c = theme.trackerHover;  break;
                default:                      c = theme.trackerInactive; break;
            }
            dotImage.color = c;

            if (glowImage != null)
                glowImage.color = new Color(c.r, c.g, c.b, State == TrackerState.Inactive ? 0f : 0.55f);
        }
    }
}
