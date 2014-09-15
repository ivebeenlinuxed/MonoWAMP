using System;
using System.Net.Sockets;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Cryptography;

namespace WAMPServer
{
	public class WAMPClient : WebSocketClient
	{
		public delegate void WAMPPacketHandler (WAMPClient c, JArray WAMPPacket);

		public event WAMPPacketHandler OnWAMPAbort;
		public event WAMPPacketHandler OnWAMPChallenge;
		public event WAMPPacketHandler OnWAMPAuthenticate;
		public event WAMPPacketHandler OnWAMPGoodbye;
		public event WAMPPacketHandler OnWAMPHeartbeat;
		public event WAMPPacketHandler OnWAMPError;

		public List<WAMPRole> knownRoles = new List<WAMPRole>();
		public string realm;

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

		protected void OnWAMPSubscribe(JArray message) {
			JArray packet = new JArray ();
			packet.Add (WAMPMessageType.SUBSCRIBED);
			packet.Add (message [1]);
			RandomNumberGenerator random = RandomNumberGenerator.Create ();
			byte[] key = new byte[4];
			random.GetBytes (key);
			packet.Add (System.Convert.ToBase64String (key));
			this.Send (packet.ToString ());
		}

		protected void OnWAMPHello(JArray message) {
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

