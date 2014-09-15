using System;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace WAMPServer
{
	public class WebsocketClient
	{
		public Socket clientSocket;
		private byte[] buffer = new byte[BUFFER_SIZE];
		private const int BUFFER_SIZE = 1024;
		private StringBuilder bufferStrBuild;
		public string uri;
		public List<string> clientProtocols = new List<string>();

		Dictionary<string, string> handshake = null;

		public WebsocketClient (Socket s)
		{
			this.bufferStrBuild = new StringBuilder();
			clientSocket = s;
			s.BeginReceive( this.buffer, 0, BUFFER_SIZE, 0,
			                     new AsyncCallback(this.ReadCallback), this);
		}

		public void ReadCallback(IAsyncResult ar) {
			Console.WriteLine ("Receiving Data");
			String content = String.Empty;

			int bytesRead = this.clientSocket.EndReceive (ar);
			if (bytesRead > 0) {
				bufferStrBuild.Append(Encoding.ASCII.GetString(
					buffer,0,bytesRead));
				content = this.bufferStrBuild.ToString();
				if (bytesRead < BUFFER_SIZE) {
					Console.WriteLine ("Got {0} bytes of data: {1}", content.Length, content);
					if (handshake == null) {
						this.DoHandshake (content);
					}
					this.bufferStrBuild.Clear();
				}
				clientSocket.BeginReceive (this.buffer, 0, BUFFER_SIZE, 0,
				                new AsyncCallback (this.ReadCallback), this);
			}
		}

		public void DoHandshake(string content) {
			string line = String.Empty;
			handshake = new Dictionary<string, string> ();
			StringReader reader = new StringReader(content);
			line = reader.ReadLine();
			uri = line.Split (' ') [1];
			while ((line = reader.ReadLine()) != "") {
				if (line == "") {
					return;
				}
				this.handshake.Add (line.Split (new Char[]{':'}) [0].Trim (), line.Split (new Char[]{':'}) [1].Trim ());
			}
			SHA1 keyhash = SHA1.Create ();
			string key = this.handshake["Sec-WebSocket-Key"]+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

			string keyaccept = System.Convert.ToBase64String(keyhash.ComputeHash (Encoding.UTF8.GetBytes(key)));

			Dictionary<string, string> returnHeaders = new Dictionary<string, string>();
			returnHeaders ["Sec-WebSocket-Accept"] = keyaccept;
			if (handshake.ContainsKey ("Sec-WebSocket-Protocol")) {
				clientProtocols.AddRange(handshake["Sec-WebSocket-Protocol"].Replace(", ", ",").Split(new Char[]{','}));
				returnHeaders["Sec-WebSocket-Protocol"] = clientProtocols.ToArray()[0];
			}

			//TODO Raise an event


			string sendString = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n";
			foreach (KeyValuePair<string, string> kvp in returnHeaders) {
				sendString += kvp.Key + ": " + kvp.Value + "\r\n";
			}
			sendString += "\r\n";

			this.Send (sendString);

		}

		private void Send(String data) {
			// Convert the string data to byte data using ASCII encoding.
			byte[] byteData = Encoding.UTF8.GetBytes(data);

			// Begin sending the data to the remote device.
			clientSocket.BeginSend(byteData, 0, byteData.Length, 0,
			                  new AsyncCallback(this.SendCallback), this);
		}

		private void SendCallback(IAsyncResult ar) {
			try {
				// Retrieve the socket from the state object.

				// Complete sending the data to the remote device.
				int bytesSent = clientSocket.EndSend(ar);
				Console.WriteLine("Sent {0} bytes to client.", bytesSent);
			} catch (Exception e) {
				Console.WriteLine(e.ToString());
			}
		}
	}
}

