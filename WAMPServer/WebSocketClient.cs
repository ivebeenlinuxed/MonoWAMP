using System;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace WAMPServer
{
	public delegate void HandshakeResponseHandler(WebSocketClient client, Dictionary<string, string> responseHeaders);
	public delegate void PacketRecievedHandler(WebSocketClient client, WebSocketFrame frame);
	public delegate void HandshakeCompleteHander(WebSocketClient client);

	public class WebSocketClient
	{
		public Socket clientSocket;
		private byte[] buffer = new byte[BUFFER_SIZE];
		private const int BUFFER_SIZE = 1024;
		public string uri;
		public List<string> clientProtocols;

		public event HandshakeResponseHandler OnHandshakeRequest;
		public event HandshakeCompleteHander OnHandshakeComplete;

		//Put our byte fragements together here
		private MemoryStream packetStream;

		//A packet is received
		public event PacketRecievedHandler OnPacketReceived;
		protected WebSocketServer server;

		Dictionary<string, string> handshake = null;

		public WebSocketClient (WebSocketServer server, Socket s)
		{
			this.server = server;
			clientSocket = s;
			s.BeginReceive( this.buffer, 0, BUFFER_SIZE, 0,
			                     new AsyncCallback(this.ReadCallback), this);

			this.OnPacketReceived += new PacketRecievedHandler(delegate (WebSocketClient client, WebSocketFrame frame) {
				if (frame.opcode == (byte)WebSocketOpcode.CLOSE) {
					client.clientSocket.Close();
				}
			});

			this.OnPacketReceived += new PacketRecievedHandler(delegate (WebSocketClient client, WebSocketFrame frame) {
				if (frame.opcode == (byte)WebSocketOpcode.PING) {
					WebSocketFrame f = new WebSocketFrame();
					f.opcode = (byte)WebSocketOpcode.PONG;
					f.payloadData = frame.payloadData;
					client.Send(f);
				}
			});


		}



		private void ReadCallback(IAsyncResult ar) {
			//TODO DoS? What happens if packet is too long?
			Console.WriteLine ("Receiving Data");
			if (packetStream == null || !packetStream.CanWrite) {
				packetStream = new MemoryStream ();
			}

			int bytesRead = this.clientSocket.EndReceive (ar);
			if (bytesRead > 0) {
				packetStream.Write (buffer, 0, bytesRead);
				Console.WriteLine ("Got {0} bytes of data", bytesRead);

				byte[] buf = packetStream.ToArray ();

				//Handshake first...
				if (handshake == null) {
					string strHandshake = Encoding.UTF8.GetString (buf);
					//End of an HTTP header
					if (strHandshake.EndsWith ("\r\n\r\n")) {
						Console.WriteLine ("Got Handshake");
						this.DoHandshake (strHandshake);
					}
					packetStream.Close ();
					packetStream = new MemoryStream ();
				} else {
					while (WebSocketFrame.HaveFullPacket (buf)) {
						Console.WriteLine ("Got a Packet");
						WebSocketFrame frame = new WebSocketFrame ();
						//Read bytes off the stream
						ulong readBytes = frame.Parse (buf);

						if (OnPacketReceived != null) {
							OnPacketReceived (this, frame);
						}



						//Pull off unread bytes
						byte[] newbuf = new byte[(ulong)buf.Length - readBytes];
						Array.Copy (buf, (int)readBytes, newbuf, 0, newbuf.Length);
						buf = newbuf;


						//Refresh the stream
						packetStream.Close ();
						packetStream = new MemoryStream ();
						packetStream.Write (newbuf, 0, newbuf.Length);

					}
				}

				if (clientSocket.Connected) {
					clientSocket.BeginReceive (this.buffer, 0, BUFFER_SIZE, 0,
					                           new AsyncCallback (this.ReadCallback), this);
				}
			}

		}

		private void DoHandshake(string content) {
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
			clientProtocols = new List<string>();
			if (handshake.ContainsKey ("Sec-WebSocket-Protocol")) {
				clientProtocols.AddRange(handshake["Sec-WebSocket-Protocol"].Replace(", ", ",").Split(new Char[]{','}));
			}

			//TODO Raise an event
			if (OnHandshakeRequest != null) {
				OnHandshakeRequest (this, returnHeaders);
			}
			string sendString = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n";
			foreach (KeyValuePair<string, string> kvp in returnHeaders) {
				sendString += kvp.Key + ": " + kvp.Value + "\r\n";
			}
			sendString += "\r\n";

			byte[] byteData = Encoding.UTF8.GetBytes(sendString);
			clientSocket.BeginSend(byteData, 0, byteData.Length, 0,
			                      new AsyncCallback(this.SendHandshakeCallback), this);
		}

		private void SendHandshakeCallback(IAsyncResult ar) {
			try {
				// Retrieve the socket from the state object.

				// Complete sending the data to the remote device.
				int bytesSent = clientSocket.EndSend(ar);
				Console.WriteLine("Sent {0} bytes to client.", bytesSent);
			} catch (Exception e) {
				Console.WriteLine(e.ToString());
			}
			if (OnHandshakeComplete != null) {
				OnHandshakeComplete (this);
			}
		}

		/**
		 * Send plain text data
		*/
		public void Send(String data) {
			// Convert the string data to byte data using ASCII encoding.
			byte[] byteData = Encoding.UTF8.GetBytes(data);

			//TODO what happens if frame is bigger than 2^64? Fragementation!
			WebSocketFrame frame = new WebSocketFrame ();
			frame.payloadData = byteData;
			frame.opcode = (byte)WebSocketOpcode.TEXT;
			byteData = frame.Encode ();

			if (!clientSocket.Connected) {
				return;
			}

			// Begin sending the data to the remote device.
			clientSocket.BeginSend(byteData, 0, byteData.Length, 0,
			                  new AsyncCallback(this.SendCallback), this);
		}

		/**
		 * Send binary data
		 */
		public void Send(byte[] data) {
			//TODO what happens if frame is bigger than 2^64? Fragementation! Where do we want to handle that?
			WebSocketFrame frame = new WebSocketFrame ();
			frame.payloadData = data;
			frame.opcode = (byte)WebSocketOpcode.BINARY;
			byte[] byteData = frame.Encode ();

			//TODO What about deflate compression header?
			if (!clientSocket.Connected) {
				return;
			}

			// Begin sending the data to the remote device.
			clientSocket.BeginSend(byteData, 0, byteData.Length, 0,
			                       new AsyncCallback(this.SendCallback), this);
		}

		public void Send(WebSocketFrame frame) {
			byte[] byteData = frame.Encode ();

			//TODO What about deflate compression header?
			if (!clientSocket.Connected) {
				return;
			}

			// Begin sending the data to the remote device.
			clientSocket.BeginSend(byteData, 0, byteData.Length, 0,
			                       new AsyncCallback(this.SendCallback), this);

		}

		private void SendCallback(IAsyncResult ar) {
			try {
				// Complete sending the data to the remote device.
				int bytesSent = clientSocket.EndSend(ar);
				Console.WriteLine("Sent {0} bytes to client.", bytesSent);
			} catch (Exception e) {
				Console.WriteLine(e.ToString());
			}
		}

		public void Close(WebSocketCloseStatus status) {
			WebSocketFrame frame = new WebSocketFrame ();
			frame.opcode = (byte)WebSocketOpcode.CLOSE;
			frame.payloadData = BitConverter.GetBytes((int)status);
			byte[] byteData = frame.Encode ();
			clientSocket.Send (byteData);
			//TODO what happens if we don't ever send a reply? We don't close?
		}

		/**
		 * Close the connection
		 */
		public void Close() {
			this.Close(WebSocketCloseStatus.NORMAL);
		}
	}
}

