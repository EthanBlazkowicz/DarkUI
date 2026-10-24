using System;
using System.Windows.Forms;

namespace DarkUI.Config
{
    /// <summary>
    /// DPI helpers for the fixed pixel metrics used by custom-drawn controls
    /// (item heights, paddings). Text already scales with the font, but these
    /// constants would otherwise stay at 96-DPI size on scaled displays.
    /// </summary>
    internal static class DpiHelper
    {
        public static int Scale(Control control, int value)
            => (int)Math.Round(value * control.DeviceDpi / 96f);
    }
}
