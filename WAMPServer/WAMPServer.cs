using System;
using System.Net;
using System.Net.Sockets;

namespace WAMPServer
{
	public delegate void WAMPMessageHandler(WebSocketClient client, WebSocketFrame frame);

	public class WAMPServer : WebSocketServer
	{
		public delegate void WAMPClientConnectedDelegate (WAMPClient c);
		public event WAMPClientConnectedDelegate OnWAMPClientConnected;

		public WAMPServer (IPEndPoint ep) : base(ep)
		{

		}

		public override void AcceptCallback(IAsyncResult ar) {
			Console.WriteLine ("Accepting Client");
			Socket listener = (Socket)ar.AsyncState;
			Socket client = listener.EndAccept (ar);
			listener.BeginAccept (new AsyncCallback (this.AcceptCallback), listener);
			WAMPClient c = new WAMPClient (this, client);
			if (OnWAMPClientConnected != null) {
				OnWAMPClientConnected (c);
			}
			clients.Add (c);
		}
	}
}

