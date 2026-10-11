using DarkUI.Icons;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Forms
{
    public partial class DarkMessageBox : DarkDialog
    {
        #region Field Region

        private string _message;
        private int _maximumWidth = 350;

        #endregion

        #region Property Region

        [Description("Determines the maximum width of the message box when it autosizes around the displayed message.")]
        [DefaultValue(350)]
        public int MaximumWidth
        {
            get { return _maximumWidth; }
            set
            {
                _maximumWidth = value;
                CalculateSize();
            }
        }

        #endregion

        #region Constructor Region

        public DarkMessageBox()
        {
            InitializeComponent();
            this.TopMost = true;
        }

        public DarkMessageBox(string message, string title, DarkMessageBoxIcon icon, DarkDialogButton buttons)
            : this()
        {
            Text = title;
            _message = message;

            DialogButtons = (MessageBoxButtons)buttons;
            SetIcon(icon);
        }

        public DarkMessageBox(string message)
            : this(message, null, DarkMessageBoxIcon.None, DarkDialogButton.OK)
        { }

        public DarkMessageBox(string message, string title)
            : this(message, title, DarkMessageBoxIcon.None, DarkDialogButton.OK)
        { }

        public DarkMessageBox(string message, string title, DarkDialogButton buttons)
            : this(message, title, DarkMessageBoxIcon.None, buttons)
        { }

        public DarkMessageBox(string message, string title, DarkMessageBoxIcon icon)
            : this(message, title, icon, DarkDialogButton.OK)
        { }

        #endregion

        #region Static Method Region

        public static DialogResult ShowInformation(string message, string caption, DarkDialogButton buttons = DarkDialogButton.OK)
        {
            return ShowDialog(message, caption, DarkMessageBoxIcon.Information, buttons);
        }

        public static DialogResult ShowWarning(string message, string caption, DarkDialogButton buttons = DarkDialogButton.OK)
        {
            return ShowDialog(message, caption, DarkMessageBoxIcon.Warning, buttons);
        }

        public static DialogResult ShowError(string message, string caption, DarkDialogButton buttons = DarkDialogButton.OK)
        {
            return ShowDialog(message, caption, DarkMessageBoxIcon.Error, buttons);
        }
        public static DialogResult DialogYesNo(string message, string caption, DarkDialogButton buttons = DarkDialogButton.YesNoCancel)
        {
            return ShowDialog(message, caption, DarkMessageBoxIcon.None, buttons);
        }

        public static DialogResult DialogYesNoCancel(string message, string caption, DarkDialogButton buttons = DarkDialogButton.YesNo)
        {
            return ShowDialog(message, caption, DarkMessageBoxIcon.None, buttons);
        }

        private static DialogResult ShowDialog(string message, string caption, DarkMessageBoxIcon icon, DarkDialogButton buttons)
        {
            using (var dlg = new DarkMessageBox(message, caption, icon, buttons))
            {
                var result = dlg.ShowDialog();
                return result;
            }
        }

        #endregion

        #region Method Region

        private void SetIcon(DarkMessageBoxIcon icon)
        {
            // Plays the matching sound when the dialog opens (DarkForm.OnLoad).
            NotificationIcon = icon switch
            {
                DarkMessageBoxIcon.Information => MessageBoxIcon.Information,
                DarkMessageBoxIcon.Warning => MessageBoxIcon.Warning,
                DarkMessageBoxIcon.Error => MessageBoxIcon.Error,
                _ => MessageBoxIcon.None
            };

            switch (icon)
            {
                case DarkMessageBoxIcon.None:
                    picIcon.Visible = false;
                    lblText.Left = 10;
                    btnCopy.Visible = false;
                    break;
                case DarkMessageBoxIcon.Information:
                    picIcon.Image = MessageBoxIcons.info;
                    btnCopy.Visible = false;
                    break;
                case DarkMessageBoxIcon.Warning:
                    picIcon.Image = MessageBoxIcons.warning;
                    btnCopy.Visible = true;
                    break;
                case DarkMessageBoxIcon.Error:
                    picIcon.Image = MessageBoxIcons.error;
                    btnCopy.Visible = true;
                    break;
            }
        }

        private void CalculateSize()
        {
            const int baseWidth = 260;
            const int baseHeight = 124;
            const int contentMargin = 25;
            const int contentTop = 9;

            var width = baseWidth;
            var height = baseHeight;

            // Reset form back to original size
            Size = new Size(width, height);

            // Measure the message so we know whether it needs to wrap. A message that
            // already spans multiple lines (explicit line breaks) must never be treated
            // as a single line, otherwise the dialog will not grow to fit the text.
            lblText.Text = string.Empty;
            lblText.AutoSize = true;
            lblText.Text = _message;

            var singleLineHeight = lblText.Font.Height + 2;
            var isMultiLine = lblText.Height > singleLineHeight;

            // Set the minimum dialog size to whichever is bigger - the original size or the buttons.
            var footerWidth = TotalButtonSize + 15;
            if (btnCopy.Visible)
                footerWidth += btnCopy.Width + 15;
            var minWidth = Math.Max(width, footerWidth);

            // Calculate the total size of the message
            var totalWidth = lblText.Right + contentMargin;

            // Make sure we're not making the dialog bigger than the maximum size.
            // Only a genuine single line may use the compact layout.
            if (totalWidth < _maximumWidth && !isMultiLine)
            {
                // Width is smaller than the maximum width.
                // This means we can have a single-line message box.
                // Move the label to accomodate this.
                width = totalWidth;
                lblText.Top = picIcon.Top + (picIcon.Height / 2) - (lblText.Height / 2);
            }
            else
            {
                // Width is larger than the maximum width, or the message is multi-line.
                // Change the label size, wrap it and grow the dialog so the text never
                // overlaps the footer buttons.
                width = _maximumWidth;
                var offsetHeight = height - picIcon.Height;
                lblText.AutoUpdateHeight = true;
                lblText.Width = width - lblText.Left - contentMargin;
                lblText.Top = contentTop;
                height = offsetHeight + lblText.Height;
            }

            // Force the width to the minimum width
            if (width < minWidth)
                width = minWidth;

            // Set the new size of the dialog
            Size = new Size(width, height);
        }

        #endregion

        #region Event Handler Region

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            CalculateSize();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_message))
                return;

            try
            {
                Clipboard.SetText(_message);
                btnCopy.Text = "Copied";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                btnCopy.Text = "Failed";
            }
        }

        #endregion
    }
}
