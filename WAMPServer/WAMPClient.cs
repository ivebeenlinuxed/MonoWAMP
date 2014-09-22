using System;
using System.Net.Sockets;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Net;

namespace WAMPServer
{
	public class WAMPClient : WebSocketClient
	{
		public delegate void WAMPPacketHandler (WAMPClient c, JArray WAMPPacket);

		public List<WAMPRole> knownRoles = new List<WAMPRole> ();
		public string realm;
		public Dictionary<string, string> subscribedTopics = new Dictionary<string, string> ();
		public Dictionary<string, string> registeredProcedures = new Dictionary<string, string> ();

		public WAMPClient (WAMPServer server, Socket s) : base(server, s)
		{
			this.OnHandshakeRequest += this.ConfirmProtocol;
			this.OnPacketReceived += this.LaunchWAMPEvents;
		}

		protected void ConfirmProtocol(WebSocketClient c, Dictionary<string, string> responseHeaders) {
			if (c.clientProtocols.Contains ("wamp.2.json")) {
				responseHeaders ["Sec-WebSocket-Protocol"] = "wamp.2.json";
			}
		}

		private void LaunchWAMPEvents(WebSocketClient c, WebSocketFrame frame) {
			if (frame.opcode == (byte)WebSocketOpcode.CLOSE) {
				return;
			}
			JArray wampPacket = JArray.Parse(Encoding.UTF8.GetString(frame.payloadData));
			switch ((WAMPMessageType)((int)wampPacket [0])) {
			case WAMPMessageType.HELLO:
				this.OnWAMPHello (wampPacket);
				break;
			case WAMPMessageType.SUBSCRIBE:
				this.OnWAMPSubscribe (wampPacket);
				break;
				case WAMPMessageType.UNSUBSCRIBE:
				this.OnWAMPUnsubscribe (wampPacket);
				break;
			case WAMPMessageType.PUBLISH:
				this.OnWAMPPublish (wampPacket);
				break;
				case WAMPMessageType.CALL:
				this.OnWAMPCall (wampPacket);
				break;
			case WAMPMessageType.WELCOME:
				Console.WriteLine ("WAMP Server: Aborting Connection (Unexpected Message Type)");
				JArray array = new JArray ();
				array.Add (WAMPMessageType.ABORT);
				JObject reasons = new JObject ();
				reasons.Add ("message", "WAMP Server: Aborting Connection (Unexpected Message Type)");
				array.Add (reasons);
				array.Add ("wamp.error.UNEXPECTED_MESSAGE");
				this.Send (array.ToString ());
				break;
			}
		}

		protected virtual void OnWAMPCall(JArray message) {
			string invocationID = GenerateRandomID ();
			WAMPServer svr = (WAMPServer)this.server;


			//TODO should we take account of the realm?
			if (message.Count == 4) {
				svr.InvokeEvent ((string)message [3], invocationID);
			} else if (message.Count == 5) {
				svr.InvokeEvent ((string)message [3], invocationID, (JArray)message [4]);
			} else if (message.Count == 6) {
				svr.InvokeEvent ((string)message [3], invocationID, (JArray)message [4], (JObject)message [5]);
			}
			//TODO Add errors
		}

		protected virtual void OnWAMPPublish(JArray message) {
			string publishID = GenerateRandomID ();
			WAMPServer svr = (WAMPServer)this.server;

			//TODO should we take account of the realm?
			if (message.Count == 4) {
				svr.PublishEvent ((string)message [3], publishID);
			} else if (message.Count == 5) {
				svr.PublishEvent ((string)message [3], publishID, (JArray)message [4]);
			} else if (message.Count == 6) {
				svr.PublishEvent ((string)message [3], publishID, (JArray)message [4], (JObject)message [5]);
			}

			Console.WriteLine ("Publish request from {0}", ((IPEndPoint)this.clientSocket.RemoteEndPoint).Address.ToString());

			//TODO IF NOT - Haven't we already said we've published? Spec says send "Published" first - I suggest sending after publishing to all channels.
			if (((JObject)message [2]) ["acknowledge"] != null) {
				JArray packet = new JArray ();
				packet.Add (WAMPMessageType.PUBLISHED);
				packet.Add (message [1]);
				packet.Add (publishID);
				this.Send (packet.ToString ());
			}

		}

		private string GenerateRandomID() {
			RandomNumberGenerator random = RandomNumberGenerator.Create ();
			byte[] key = new byte[8];
			random.GetBytes (key);
			return System.Convert.ToBase64String (key);
		}

		protected virtual void OnWAMPRegister(JArray message) {
			string registrationID = GenerateRandomID ();
			JArray packet = new JArray ();
			packet.Add (WAMPMessageType.REGISTERED);
			packet.Add (message [1]);
			packet.Add (registrationID);
			//TODO don't ignore options of procedures
			this.registeredProcedures.Add ((string)message [3], registrationID);
			this.Send (packet.ToString ());
		}

		protected virtual void OnWAMPUnsubscribe(JArray message) {
			if (this.subscribedTopics.ContainsKey ((string)message [2])) {
				this.subscribedTopics.Remove ((string)message [2]);

			} else {
				//TODO We have no subscription, we should error
			}

			JArray packet = new JArray ();
			packet.Add (WAMPMessageType.UNSUBSCRIBED);
			packet.Add (message [1]);
			this.Send (packet.ToString ());
		}

		protected virtual void OnWAMPSubscribe(JArray message) {
			string subscriptionID = GenerateRandomID ();
			JArray packet = new JArray ();
			packet.Add (WAMPMessageType.SUBSCRIBED);
			packet.Add (message [1]);
			packet.Add (subscriptionID);
			Console.WriteLine("Subscribed {0}: {1}", (string)message[3], subscriptionID);
			this.subscribedTopics.Add (subscriptionID, (string)message [3]);


			this.Send (packet.ToString ());
		}

		protected virtual void OnWAMPHello(JArray message) {
			this.realm = (string)message [1];
			foreach (KeyValuePair<string, JToken> obj in (JObject)message[2]) {
				WAMPRole role = new WAMPRole ();
				role.role = obj.Key;
				role.features = new List<string> ();
				foreach (KeyValuePair<string, JToken> features in (JObject)obj.Value) {
					role.features.Add ((string)features.Key);
				}
				this.knownRoles.Add (role);
			}
			JArray array = new JArray ();
			array.Add (WAMPMessageType.WELCOME);
			RandomNumberGenerator random = RandomNumberGenerator.Create ();
			byte[] key = new byte[4];
			random.GetBytes (key);
			array.Add (System.Convert.ToBase64String (key));
			JObject data = new JObject ();
			JObject roles = new JObject ();
			roles.Add ("broker", new JArray());
			roles.Add ("dealer", new JArray());
			data.Add ("roles", roles);
			array.Add (data);
			this.Send (array.ToString ());
		}
	}
}

