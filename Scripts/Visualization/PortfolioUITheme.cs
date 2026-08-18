using UnityEngine;

namespace Mindzheimer.Portfolio
{
    /// <summary>
    /// Central colour palette for the portfolio tracker-calibration UI.
    /// Create one via Assets > Create > Portfolio > UI Theme, tweak the
    /// swatches in the Inspector, and drag it onto
    /// PortfolioCalibrationUIBuilder.theme. Swap these for your real
    /// portfolio brand colours whenever you have them.
    /// </summary>
    [CreateAssetMenu(fileName = "PortfolioUITheme", menuName = "Portfolio/UI Theme")]
    public class PortfolioUITheme : ScriptableObject
    {
        [Header("Backgrounds")]
        public Color background   = new Color32(0x0B, 0x0F, 0x14, 0xFF);
        public Color sidebarPanel = new Color32(0x10, 0x15, 0x1C, 0xFF);
        public Color cardPanel    = new Color32(0x17, 0x1F, 0x2A, 0xFF);
        public Color cardPanelAlt = new Color32(0x1C, 0x25, 0x32, 0xFF);

        [Header("Accent")]
        public Color accentPrimary = new Color32(0x34, 0xE2, 0xC4, 0xFF); // mint/teal
        public Color accentHover   = new Color32(0x22, 0xB8, 0xA0, 0xFF);
        public Color accentSoft    = new Color32(0x34, 0xE2, 0xC4, 0x33);

        [Header("Text")]
        public Color textPrimary   = new Color32(0xF5, 0xF7, 0xFA, 0xFF);
        public Color textSecondary = new Color32(0x8C, 0xA0, 0xB3, 0xFF);
        public Color textOnAccent  = new Color32(0x08, 0x12, 0x10, 0xFF);

        [Header("Tracker Points")]
        public Color trackerInactive = new Color32(0x3A, 0x4A, 0x59, 0xFF);
        public Color trackerHover    = new Color32(0x22, 0xB8, 0xA0, 0xFF);
        public Color trackerActive   = new Color32(0x34, 0xE2, 0xC4, 0xFF);

        [Header("Body Silhouette")]
        public Color bodyFill    = new Color32(0x1E, 0x28, 0x34, 0xFF);
        public Color bodyOutline = new Color32(0x2A, 0x38, 0x47, 0xFF);

        private static PortfolioUITheme _default;

        /// <summary>Fallback theme used if no asset is assigned in the Inspector.</summary>
        public static PortfolioUITheme Default
        {
            get
            {
                if (_default == null)
                    _default = CreateInstance<PortfolioUITheme>();
                return _default;
            }
        }
    }
}
