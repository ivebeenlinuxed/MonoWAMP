using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace WAMPServer
{
	public class WebSocketServer
	{
		public List<WebSocketClient> clients;

		public List<string> clientProtocols = new List<string>();

		public delegate void ClientConnectedDelegate (WebSocketClient c);
		public event ClientConnectedDelegate OnClientConnected;

		public WebSocketServer (IPEndPoint ep)
		{
			clients = new List<WebSocketClient> ();

			Socket listener = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try {
				listener.Bind(ep);
				listener.Listen(100);
				listener.BeginAccept(new AsyncCallback(this.AcceptCallback), listener);
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
			}
			Console.WriteLine("Server Ready...");
		}

		public virtual void AcceptCallback(IAsyncResult ar) {
			Console.WriteLine ("Accepting Client");
			Socket listener = (Socket)ar.AsyncState;
			Socket client = listener.EndAccept (ar);
			listener.BeginAccept (new AsyncCallback (this.AcceptCallback), listener);
			WebSocketClient c = new WebSocketClient (this, client);
			if (OnClientConnected != null) {
				OnClientConnected (c);
			}
			clients.Add (c);
		}

		public bool Publish() {
			return false;
		}
	}
}

