using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace WAMPServer
{
	public class WebsocketServer
	{
		public List<WebsocketClient> clients;

		public WebsocketServer (IPEndPoint ep)
		{
			clients = new List<WebsocketClient> ();

			Socket listener = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try {
				listener.Bind(ep);
				listener.Listen(100);
				listener.BeginAccept(new AsyncCallback(this.AcceptCallback), listener);
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
			}
			Console.WriteLine("Server Ready...");
			while (true) {
				Console.WriteLine ("Server Alive");
				System.Threading.Thread.Sleep (5000);
			}
		}

		public void AcceptCallback(IAsyncResult ar) {
			Console.WriteLine ("Accepting Client");
			Socket listener = (Socket)ar.AsyncState;
			Socket client = listener.EndAccept (ar);
			listener.BeginAccept (new AsyncCallback (this.AcceptCallback), listener);
			WebsocketClient c = new WebsocketClient (client);
			clients.Add (c);
		}

		public bool Publish() {
			return false;
		}
	}
}

