using System;
using System.Diagnostics;
using System.Linq;

namespace WPCordovaClassLib.Cordova.Commands
{
	public class AppBuilderOutput : BaseCommand
	{
		private readonly SocketLogger logger;

		public AppBuilderOutput()
		{
			this.logger = new SocketLogger();
			this.logger.BindPort();
		}

		public void logLevel(string options)
		{
			var message = ParseMessage(options);

			Debug.WriteLine(message);

			this.logger.Log(message);
		}
		
		private static string ParseMessage(string options)
		{
			var args = JSON.JsonHelper.Deserialize<string[]>(options);
			var level = args[0];
			var message = args[1];

			return "LOG".Equals(level, StringComparison.OrdinalIgnoreCase) ? message : string.Format("{0}: {1}", level, message);
		}
	}
}
