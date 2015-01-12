using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Telerik.Windows.Controls;

namespace Telerik.BlackDragon.LiveSync
{
	public partial class ProgressDialog : UserControl
	{
		private static readonly string[] buttonTitles = new string[] { "Cancel" };
		private const string MessageBoxTitle = "LiveSyncing";

		public event EventHandler Canceled;

		public ProgressDialog()
		{
			this.InitializeComponent();
		}

		public void ReportProgress(double progress)
		{
			this.Progress.Value = progress;
		}

		public async Task<MessageBoxClosedEventArgs> ShowAsync()
		{
			var result = await RadMessageBox.ShowAsync(buttonTitles, MessageBoxTitle, this, checkBoxContent: null, isCheckBoxChecked: false, vibrate: true);
			if (result.Result != DialogResult.Programmatic)
			{
				this.RaiseCanceled();				
			}

			this.ResetToInitialState();
			return result;
		}

		public void Dismiss()
		{
			RadMessageBox.Dismiss();
		}

		private void ResetToInitialState()
		{
			var parent = this.Parent as ContentControl;
			if (parent != null)
			{
				parent.Content = null;
			}
			this.ReportProgress(0);
		}

		private void RaiseCanceled()
		{
			var handler = this.Canceled;
			if (handler != null)
			{
				handler(this, EventArgs.Empty);
			}
		}
	}
}