namespace AsynCUDA12.Forms
{
    partial class WindowMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.listBox_log = new ListBox();
			this.SuspendLayout();
			// 
			// listBox_log
			// 
			this.listBox_log.FormattingEnabled = true;
			this.listBox_log.HorizontalScrollbar = true;
			this.listBox_log.ItemHeight = 15;
			this.listBox_log.Location = new Point(740, 530);
			this.listBox_log.Name = "listBox_log";
			this.listBox_log.Size = new Size(512, 259);
			this.listBox_log.TabIndex = 0;
			// 
			// WindowMain
			// 
			this.AutoScaleDimensions = new SizeF(7F, 15F);
			this.AutoScaleMode = AutoScaleMode.Font;
			this.ClientSize = new Size(1264, 801);
			this.Controls.Add(this.listBox_log);
			this.MaximumSize = new Size(1280, 840);
			this.MinimumSize = new Size(1280, 840);
			this.Name = "WindowMain";
			this.Text = "AsynCUDA12 (Forms)";
			this.ResumeLayout(false);
		}

		#endregion

		private ListBox listBox_log;
	}
}
