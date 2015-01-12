using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Phone.Shell;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WPCordovaClassLib.Cordova.Commands
{
	public class SocketLogger
	{
		private readonly object lockObject = new object();

		private const int Port = 6510;
		private const string ConnectedNotification = "Connected";

		private readonly Queue<string> messageQueue;

		private StreamSocketListener connectionListener;
		private StreamSocket socket;
		private DataWriter outputWriter;

		private bool IsConnected
		{
			get
			{
				return this.socket != null;
			}
		}

		public SocketLogger()
		{
			this.messageQueue = new Queue<string>();

			PhoneApplicationService.Current.Activated += (s, e) => this.OnActivated();
			PhoneApplicationService.Current.Deactivated += (s, e) => this.OnDeativated();
			PhoneApplicationService.Current.Closing += (s, e) => this.OnDeativated();
		}

		public void Log(string message)
		{
			lock (this.lockObject)
			{
				if (this.IsConnected)
				{
					this.Send(message);
				}
				else
				{
					this.messageQueue.Enqueue(message);
				}
			}
		}
  
		public async Task BindPort()
		{
			if (this.connectionListener == null)
			{
				this.connectionListener = new StreamSocketListener();
				this.connectionListener.ConnectionReceived += this.OnConnectionReceived;
				await this.connectionListener.BindServiceNameAsync(Port.ToString());
			}
		}

		private void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
		{
			this.DisposeConnection();

			this.socket = args.Socket;
			this.outputWriter = new DataWriter(this.socket.OutputStream)
			{
				UnicodeEncoding = UnicodeEncoding.Utf16LE
			};
		
			this.Send(ConnectedNotification);

			this.ProcessMessagesFromQueue();
		}
  
		private async Task ProcessMessagesFromQueue()
		{
			while (this.messageQueue.Count > 0)
			{
				var message = this.messageQueue.Dequeue();
				await this.Send(message);
			}
		}
  
		private async Task OnActivated()
		{
			await this.BindPort();
		}

		private void DisposeConnection()
		{
			if (this.socket != null)
			{
				this.socket.Dispose();
				this.socket = null;
			}

			if (this.outputWriter != null)
			{
				this.outputWriter.Dispose();
				this.outputWriter = null;
			}
		}

		private void OnDeativated()
		{
			if (this.connectionListener != null)
			{
				this.connectionListener.Dispose();
				this.connectionListener = null;
			}

			this.DisposeConnection();
		}

		private async Task Send(string message)
		{
			if (this.outputWriter != null)
			{
				this.outputWriter.WriteString(message);
				await this.outputWriter.StoreAsync();
			}
		}
	}
}