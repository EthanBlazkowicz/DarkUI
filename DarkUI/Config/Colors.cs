using System.Drawing;

namespace DarkUI.Config
{
    public static class Colors
    {
        private static Theme T => ThemeManager.Active;

        public static Color GreyBackground => T.GreyBackground;
        public static Color HeaderBackground => T.HeaderBackground;
        public static Color BlueBackground => T.BlueBackground;
        public static Color DarkBlueBackground => T.DarkBlueBackground;
        public static Color DarkBackground => T.DarkBackground;
        public static Color MediumBackground => T.MediumBackground;
        public static Color LightBackground => T.LightBackground;
        public static Color LighterBackground => T.LighterBackground;
        public static Color LightestBackground => T.LightestBackground;
        public static Color LightBorder => T.LightBorder;
        public static Color DarkBorder => T.DarkBorder;
        public static Color LightText => T.LightText;
        public static Color DisabledText => T.DisabledText;
        public static Color SelectionText => T.GetSelectionText();
        public static Color BlueHighlight => T.BlueHighlight;
        public static Color BlueSelection => T.BlueSelection;
        public static Color GreyHighlight => T.GreyHighlight;
        public static Color GreySelection => T.GreySelection;
        public static Color DarkGreySelection => T.DarkGreySelection;
        public static Color DarkBlueBorder => T.DarkBlueBorder;
        public static Color LightBlueBorder => T.LightBlueBorder;
        public static Color ActiveControl => T.ActiveControl;
        public static Color MenuItemToggledOnFill => T.MenuItemToggledOnFill;
        public static Color MenuItemToggledOnBorder => T.MenuItemToggledOnBorder;
        public static Color StatusSuccess => T.StatusSuccess;
        public static Color StatusWarning => T.StatusWarning;
        public static Color StatusError => T.StatusError;
    }
}
