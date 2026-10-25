using System;
using System.Windows.Forms;

namespace DarkUI.Config
{
    public static class Consts
    {
        // These metrics were authored at 96 DPI. Scale them to the process
        // DPI so custom-drawn controls keep their proportions on scaled
        // displays (e.g. 4K at 200%). Evaluated once — single-DPI process.
        private static readonly int _scale = GetScale();

        private static int GetScale()
        {
            try
            {
                using var c = new Control();
                return Math.Max(1, (int)Math.Round(c.DeviceDpi / 96f));
            }
            catch
            {
                return 1;
            }
        }

        private static int S(int value) => value * _scale;

        public static int Padding => S(10);

        public static int ScrollBarSize => S(15);
        public static int ArrowButtonSize => S(15);
        public static int MinimumThumbSize => S(11);

        public static int CheckBoxSize => S(12);
        public static int RadioButtonSize => S(12);

        public static int ToolWindowHeaderSize => S(25);
        public static int DocumentTabAreaSize => S(24);
        public static int ToolWindowTabAreaSize => S(21);
        public static int MinimumRegionSize => 80;

        public static bool DisableSingleDocumentTab => true;
    }
}
