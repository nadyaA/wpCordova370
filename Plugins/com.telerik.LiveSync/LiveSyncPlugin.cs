using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Devices;
using SharpCompress.Archive.Zip;
using Telerik.Windows.Controls;
using WPCordovaClassLib;
using WPCordovaClassLib.Cordova.JSON;
using WPCordovaClassLib.CordovaLib;

#pragma warning disable 4014

namespace Telerik.BlackDragon.LiveSync
{
	internal class LiveSyncPlugin
	{
		private static readonly TimeSpan threeFingerHoldDuration = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan usbSyncRefreshDuration = TimeSpan.FromMilliseconds(100);
		private static readonly string reloadFilePath = Path.Combine(LiveSyncFolder, ".livesync.refresh");
		private static readonly string updateServerInfoFilePath = Path.Combine(LiveSyncFolder, ".livesync.serverinfo");

		private const string LiveSyncFolder = "LiveSync";
		private const string XapRootFolder = "www";

		private const string ErrorMessageTitle = "An error has occurred";
		private const string ErrorMessageContentFormat = "Please check your internet connection and try again.\n{0}";

		private const string LiveSyncNotConfiguredTitle = "LiveSync is not configured";
		private const string LiveSyncNotConfiguredContent = "LiveSync information is not set. Please, sync at least once before using three finger refresh.";

		private readonly CordovaView view;
		private readonly Lazy<DispatcherTimer> threeFingerTimer;
		private readonly DispatcherTimer usbSyncTimer = new DispatcherTimer { Interval = LiveSyncPlugin.usbSyncRefreshDuration };
		private readonly Lazy<ProgressDialog> progressDialog;
		private readonly Lazy<WebClient> webClient;

		private bool isLiveSyncing = false;
		private TaskCompletionSource<object> liveSyncingTask;

		private Uri LiveSyncStartPageUri
		{
			get
			{
				return new Uri(this.view.StartPageUri.OriginalString.Replace(LiveSyncPlugin.XapRootFolder, LiveSyncPlugin.LiveSyncFolder), UriKind.Relative);
			}
		}

		public LiveSyncPlugin(CordovaView view, ConfigHandler configHandler)
		{
			this.view = view;
			this.threeFingerTimer = new Lazy<DispatcherTimer>(() =>
			{
				var timer = new DispatcherTimer { Interval = LiveSyncPlugin.threeFingerHoldDuration };
				timer.Tick += this.OnThreeFingerTimerTick;
				return timer;
			});

			this.usbSyncTimer.Tick += this.OnUsbSyncTimerTick;
			this.usbSyncTimer.Start();
			
			this.progressDialog = new Lazy<ProgressDialog>(() =>
			{
				var dialog = new ProgressDialog();
				dialog.Canceled += this.OnDialogCanceled;
				return dialog;
			});

			this.webClient = new Lazy<WebClient>(() =>
			{
				var client = new WebClient();
				client.DownloadProgressChanged += this.OnDownloadProgressChanged;
				client.OpenReadCompleted += this.OnOpenReadCompleted;
				client.Headers["X-LiveSync-TargetPlatformVersion"] = System.Environment.OSVersion.Version.ToString(2);
				return client;
			});

			if (!ServerInfo.IsInitialized)
			{
				ServerInfo.Initialize(configHandler);
			}

			Touch.FrameReported += this.OnFrameReported;
		}

		public async Task LiveSync()
		{
			if (!ServerInfo.IsInitialized)
			{
				RadMessageBox.ShowAsync(LiveSyncNotConfiguredTitle, MessageBoxButtons.OK, LiveSyncNotConfiguredContent, vibrate: true);
			}
			else if (!this.isLiveSyncing)
			{
				this.isLiveSyncing = true;
				string errorMessage = null;
				try
				{
					this.progressDialog.Value.ShowAsync();
					await this.GetPackage();
					this.ReloadBrowser();
				}
				catch (TaskCanceledException)
				{
					// User canceled the download - do nothing
				}
				catch (Exception ex)
				{
					if (!LiveSyncPlugin.TryGetProjectMissingErrorMessage(ex, out errorMessage))
					{
						errorMessage = string.Format(ErrorMessageContentFormat, ex.Message);
					}
				}
				finally
				{
					this.progressDialog.Value.Dismiss();
					this.isLiveSyncing = false;
					if (!string.IsNullOrEmpty(errorMessage))
					{
						RadMessageBox.ShowAsync(errorMessage, ErrorMessageTitle, MessageBoxButtons.OK);
					}
				}
			}
		}

		public async Task NavigateToLiveSyncPage()
		{
			await LiveSyncPlugin.UnpackFilesFromXapIfNecessary();

			this.view.Browser.Navigate(this.LiveSyncStartPageUri);
		}

		private static bool TryGetProjectMissingErrorMessage(Exception ex, out string errorMessage)
		{
			var webException = ex as WebException;
			if (webException != null)
			{
				var response = webException.Response as HttpWebResponse;
				if (response != null)
				{
					var responseContent = new StreamReader(response.GetResponseStream()).ReadToEnd();
					try
					{
						errorMessage = JsonHelper.Deserialize<ErrorMessageResponse>(responseContent).ErrorMessage;
						return true;
					}
					catch
					{
					}
				}
			}

			errorMessage = string.Empty;
			return false;
		}

		private static async Task UnpackFilesFromXapIfNecessary()
		{
			using (var store = IsolatedStorageFile.GetUserStoreForApplication())
			{
				if (store.DirectoryExists(LiveSyncPlugin.LiveSyncFolder))
				{
					var xapFolder = new DirectoryInfo(LiveSyncPlugin.XapRootFolder);
					// If the LiveSync folder is older, delete it and load the files from XAP
					if (store.GetLastWriteTime(LiveSyncPlugin.LiveSyncFolder).UtcDateTime < xapFolder.LastWriteTime.ToUniversalTime())
					{
						store.DeleteDirectoryRecursively(LiveSyncPlugin.LiveSyncFolder);
						await store.CopyFilesFromXap(LiveSyncPlugin.XapRootFolder, LiveSyncPlugin.LiveSyncFolder);
					}
				}
				else
				{
					await store.CopyFilesFromXap(LiveSyncPlugin.XapRootFolder, LiveSyncPlugin.LiveSyncFolder);
				}
			}
		}

		private static void ExtractFiles(Stream stream)
		{
			using (var store = IsolatedStorageFile.GetUserStoreForApplication())
			{
				var archive = ZipArchive.Open(stream);
				foreach (var entry in archive.Entries)
				{
					LiveSyncPlugin.CopyFile(entry, store);
				}
			}
		}

		private static void CopyFile(ZipArchiveEntry entry, IsolatedStorageFile store)
		{
			var targetFileName = Path.Combine(LiveSyncPlugin.LiveSyncFolder, entry.FilePath.Replace('/', Path.DirectorySeparatorChar));
			store.EnsureDirectoryExists(targetFileName);

			using (var stream = new IsolatedStorageFileStream(targetFileName, FileMode.Create, store))
			{
				entry.WriteTo(stream);
			}
		}

		private void ReloadBrowser()
		{
			try
			{
				this.view.Browser.InvokeScript("eval", "window.location.reload(true);");
			}
			catch
			{
				this.view.Browser.Navigate(this.LiveSyncStartPageUri);
			}
		}

		private Task GetPackage()
		{
			this.liveSyncingTask = new TaskCompletionSource<object>();
			this.webClient.Value.OpenReadAsync(new Uri(ServerInfo.PackageEndpoint, UriKind.Absolute));
			return this.liveSyncingTask.Task;
		}

		private void OnDialogCanceled(object sender, EventArgs e)
		{
			this.webClient.Value.CancelAsync();
		}

		private void OnOpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
		{
			if (e.Cancelled)
			{
				this.liveSyncingTask.SetCanceled();
			}
			else if (e.Error != null)
			{
				this.liveSyncingTask.SetException(e.Error);
			}
			else
			{
				try
				{
					LiveSyncPlugin.ExtractFiles(e.Result);
					this.liveSyncingTask.SetResult(null);
				}
				catch (Exception ex)
				{
					this.liveSyncingTask.SetException(ex);
				}
			}
		}

		private void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			this.progressDialog.Value.ReportProgress(e.ProgressPercentage);
		}

		private void OnUsbSyncTimerTick(object sender, EventArgs e)
		{
			if (!this.isLiveSyncing)
			{
				using (var store = IsolatedStorageFile.GetUserStoreForApplication())
				{
					this.CheckForServerInfoUpdate(store);
					this.CheckForContentUpdate(store);
				}
			}
		}

		private void CheckForServerInfoUpdate(IsolatedStorageFile store)
		{
			try
			{
				if (store.FileExists(updateServerInfoFilePath))
				{
					using (var sr = new StreamReader(store.OpenFile(updateServerInfoFilePath, FileMode.Open)))
					{
						ServerInfo.Update(sr.ReadToEnd());
					}

					store.DeleteFile(updateServerInfoFilePath);
				}
			}
			catch
			{
				// If the IDE is writing to the file, we won't be able to delete it - simply ignore the exception
			}
		}

		private void CheckForContentUpdate(IsolatedStorageFile store)
		{
			try
			{
				if (store.FileExists(reloadFilePath))
				{
					store.DeleteFile(reloadFilePath);
					VibrateController.Default.Start(TimeSpan.FromMilliseconds(500));
					this.ReloadBrowser();
				}
			}
			catch
			{
				// If the IDE is writing to the file, we won't be able to delete it - simply ignore the exception
			}
		}

		private void OnThreeFingerTimerTick(object sender, EventArgs e)
		{
			this.threeFingerTimer.Value.Stop();
			this.LiveSync();
		}

		private void OnFrameReported(object sender, TouchFrameEventArgs e)
		{
			if (!this.isLiveSyncing)
			{
				try
				{
					var points = e.GetTouchPoints(this.view);
					var touchCount = points.Where(p => p.Action != TouchAction.Up).Count();
					if (touchCount == 3)
					{
						if (!this.threeFingerTimer.Value.IsEnabled)
						{
							this.threeFingerTimer.Value.Start();
						}
					}
					else
					{
						this.threeFingerTimer.Value.Stop();
					}
				}
				catch
				{
					// In most cases an exception will occur if the cordova view is not in the visual tree. Ignore it and move along.					
				}
			}
		}

		[DataContract]
		private class ErrorMessageResponse
		{
			[DataMember(Name = "errorMessage")]
			public string ErrorMessage { get; set; }
		}
	}
}