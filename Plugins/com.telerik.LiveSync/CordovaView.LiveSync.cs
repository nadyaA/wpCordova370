using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.Phone.Controls;
using Telerik.BlackDragon.LiveSync;

namespace WPCordovaClassLib
{
	public partial class CordovaView : ISupportInitialize
	{
		private LiveSyncPlugin liveSyncPlugin;

		public void BeginInit()
		{
			if (this.liveSyncPlugin == null)
			{
				this.liveSyncPlugin = new LiveSyncPlugin(this, this.configHandler);
			}
		}

		public void EndInit()
		{
			this.Browser.Navigating -= this.CordovaBrowser_Navigating;
			this.Browser.Navigating += this.OnLiveSyncNavigating;
		}

		private async void OnLiveSyncNavigating(object sender, NavigatingEventArgs e)
		{
			if (e.Uri.OriginalString.Equals(this.StartPageUri.OriginalString, StringComparison.OrdinalIgnoreCase))
			{
				e.Cancel = true;
				await this.liveSyncPlugin.NavigateToLiveSyncPage();
			}
			else
			{
				this.CordovaBrowser_Navigating(sender, e);
			}
		}
	}
}