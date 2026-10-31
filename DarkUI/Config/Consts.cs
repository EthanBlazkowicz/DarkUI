namespace DarkUI.Config
{
    /// <summary>
    /// Fixed pixel metrics authored at 96 DPI. These are design constants:
    /// custom-drawn controls scale them per control with
    /// <see cref="DpiHelper.Scale(System.Windows.Forms.Control, int)"/> at the
    /// point of use, so each control tracks its own live DPI. Never derive a
    /// process-wide scale factor here — a frozen ratio is wrong at every DPI
    /// except the one it was captured at and goes stale when a window moves
    /// between monitors.
    /// </summary>
    public static class Consts
    {
        public const int Padding = 10;

        public const int ScrollBarSize = 15;
        public const int ArrowButtonSize = 15;
        public const int MinimumThumbSize = 11;

        public const int CheckBoxSize = 12;
        public const int RadioButtonSize = 12;

        public const int ToolWindowHeaderSize = 25;
        public const int DocumentTabAreaSize = 24;
        public const int ToolWindowTabAreaSize = 21;
        public const int MinimumRegionSize = 80;

        public static bool DisableSingleDocumentTab => true;
    }
}
