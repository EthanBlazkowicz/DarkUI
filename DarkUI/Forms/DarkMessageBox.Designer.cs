using DarkUI.Controls;

namespace DarkUI.Forms
{
    partial class DarkMessageBox
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.picIcon = new System.Windows.Forms.PictureBox();
            this.lblText = new DarkLabel();
            this.btnCopy = new DarkButton();
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // picIcon
            // 
            this.picIcon.Location = new System.Drawing.Point(10, 10);
            this.picIcon.Name = "picIcon";
            this.picIcon.Size = new System.Drawing.Size(32, 32);
            this.picIcon.TabIndex = 3;
            this.picIcon.TabStop = false;
            //
            // lblText
            //
            this.lblText.Location = new System.Drawing.Point(50, 9);
            this.lblText.Name = "lblText";
            this.lblText.Size = new System.Drawing.Size(185, 15);
            this.lblText.TabIndex = 4;
            this.lblText.Text = "Something something something";
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(10, 10);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Padding = new System.Windows.Forms.Padding(5);
            this.btnCopy.Size = new System.Drawing.Size(75, 26);
            this.btnCopy.TabIndex = 5;
            this.btnCopy.Text = "Copy";
            this.btnCopy.Visible = false;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // DarkMessageBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(244, 86);
            this.Controls.Add(this.lblText);
            this.Controls.Add(this.picIcon);
            this.pnlFooter.Controls.Add(this.btnCopy);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DarkMessageBox";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Message box";
            this.Controls.SetChildIndex(this.picIcon, 0);
            this.Controls.SetChildIndex(this.lblText, 0);
            ((System.ComponentModel.ISupportInitialize)(this.picIcon)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox picIcon;
        private DarkLabel lblText;
        private DarkButton btnCopy;
    }
}
