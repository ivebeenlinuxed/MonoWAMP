using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

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

		public void PublishEvent(string topic, string publishID, JArray args, JObject dict) {
			foreach (WAMPClient c in this.clients) {
				Dictionary<string, string> cpyTopics = c.subscribedTopics;
				foreach (KeyValuePair<string, string> kvp in cpyTopics) {
					if (kvp.Value == topic) {
						JArray packet = new JArray ();
						packet.Add (WAMPMessageType.EVENT);
						packet.Add (kvp.Key);
						packet.Add (publishID);
						packet.Add (new JObject ());
						packet.Add (args);
						packet.Add (dict);
						if (c.clientSocket.Connected) {
							Console.WriteLine ("Publishing {0} to {1}", topic, ((IPEndPoint)c.clientSocket.RemoteEndPoint).Address.ToString ());
						}
						c.Send (packet.ToString ());
					}
				}
			}
		}

		public void PublishEvent(string topic, string publishID,  JArray args) {
			foreach (WAMPClient c in clients) {
				if (c.subscribedTopics.ContainsKey(topic)) {
					Console.WriteLine("Publishing to {0}", ((IPEndPoint)c.clientSocket.RemoteEndPoint).Address.ToString());
					JArray packet = new JArray();
					packet.Add (WAMPMessageType.EVENT);
					packet.Add (c.subscribedTopics[topic]);
					packet.Add (publishID);
					packet.Add (new JObject());
					packet.Add (args);
					c.Send(packet.ToString());
				}
			}
		}

		public void PublishEvent(string topic, string publishID) {
			foreach (WAMPClient c in clients) {
				if (c.subscribedTopics.ContainsKey(topic)) {
					JArray packet = new JArray();
					packet.Add (WAMPMessageType.EVENT);
					packet.Add (c.subscribedTopics[topic]);
					packet.Add (publishID);
					packet.Add (new JObject());
					c.Send(packet.ToString());
				}
			}

		}

		public void InvokeEvent(string methodID, string invocationID, JArray args, JObject dict) {
			
		}

		public void InvokeEvent(string methodID, string invocationID, JArray args) {

		}

		public void InvokeEvent(string methodID, string invocationID) {

		}
	}
}

