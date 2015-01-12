using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using WPCordovaClassLib.Cordova.JSON;
using WPCordovaClassLib.CordovaLib;

namespace Telerik.BlackDragon.LiveSync
{
	internal static class ServerInfo
	{
		public const string HostPreferenceName = "Host";
		public const string LiveSyncTokenPreferenceName = "LiveSyncToken";

		private const string EndpointFormat = "{0}/mist/mobilepackage/files/wp8?token={1}";

		public static bool IsInitialized
		{
			get
			{
				return !string.IsNullOrEmpty(ServerInfo.Host) &&
					   !string.IsNullOrEmpty(ServerInfo.LiveSyncToken);
			}
		}

		public static string Host
		{
			get
			{
				string host;
				IsolatedStorageHelper.TryGetSetting(ServerInfo.HostPreferenceName, out host);
				return host ?? string.Empty;
			}
			set
			{
				IsolatedStorageHelper.StoreSetting(ServerInfo.HostPreferenceName, value);
			}
		}

		public static string LiveSyncToken
		{
			get
			{
				string liveSyncToken;
				IsolatedStorageHelper.TryGetSetting(ServerInfo.LiveSyncTokenPreferenceName, out liveSyncToken);
				return liveSyncToken ?? string.Empty;
			}
			set
			{
				IsolatedStorageHelper.StoreSetting(ServerInfo.LiveSyncTokenPreferenceName, value);
			}
		}

		public static string PackageEndpoint
		{
			get
			{
				if (string.IsNullOrEmpty(ServerInfo.Host) || string.IsNullOrEmpty(ServerInfo.LiveSyncToken))
				{
					throw new InvalidOperationException();
				}

				return string.Format(ServerInfo.EndpointFormat, ServerInfo.Host, HttpUtility.UrlEncode(ServerInfo.LiveSyncToken));	
			}
		}

		public static void Initialize(ConfigHandler handler)
		{
			TryUpdate(() => handler.GetPreference(ServerInfo.HostPreferenceName), () => handler.GetPreference(ServerInfo.LiveSyncTokenPreferenceName));
		}

		public static void Update(string json)
		{
			var serverInfo = JsonHelper.Deserialize<SerializedServerInfo>(json);
			TryUpdate(() => serverInfo.Host, () => serverInfo.LiveSyncToken);
		}

		private static void TryUpdate(Func<string> getHost, Func<string> getToken)
		{
			try
			{
				ServerInfo.Host = getHost();
				ServerInfo.LiveSyncToken = getToken();
			}
			catch
			{
				ServerInfo.Host = null;
				ServerInfo.LiveSyncToken = null;
			}
		}

		[DataContract]
		private class SerializedServerInfo
		{
			[DataMember(Name = "host")]
			public string Host { get; set; }

			[DataMember(Name = "liveSyncToken")]
			public string LiveSyncToken { get; set; }
		}
	}
}