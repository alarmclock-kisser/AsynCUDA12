using AsynCUDA12.Runtime;

namespace AsynCUDA12.Forms
{
	public partial class WindowMain : Form
	{
		internal readonly CudaService Cuda;



		public WindowMain(int deviceId = -1)
		{
			this.InitializeComponent();

			this.listBox_log.DataSource = CudaLogger.LogMessages;
			this.listBox_log.DataSourceChanged += (s, e) =>
			{
				if (this.listBox_log.Items.Count > 0)
				{
					this.listBox_log.SelectedIndex = this.listBox_log.Items.Count - 1;
					this.listBox_log.ClearSelected();
				}
			};
			this.listBox_log.DoubleClick += (s, e) =>
			{
				if (this.listBox_log.SelectedItem != null)
				{
					Clipboard.SetText(this.listBox_log.SelectedItem.ToString() ?? string.Empty);
				}
			};
			this.listBox_log.KeyDown += (s, e) =>
			{
				// Ctrl-Click to copy all
				if (e.Control)
				{
					this.CopyAllLogsSafe();
				}
			};

			CudaLogger.Log("WindowMain initialized");

			this.Cuda = new CudaService(deviceId);
		}

		private void CopyAllLogsSafe()
		{
			string text = string.Join(Environment.NewLine, CudaLogger.LogMessages);
			for (int i = 0; i < 3; i++)
			{
				try { Clipboard.SetText(text); return; }
				catch (Exception) { Thread.Sleep(50); }
			}
			// optional: Feedback anzeigen
		}
	}
}
