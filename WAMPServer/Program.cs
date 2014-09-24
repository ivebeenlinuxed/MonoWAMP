using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;
//using Mono.Options;
using System.Collections.Generic;
//using Mono.Options;

namespace WAMPServer
{
	class MainClass
	{
		public static Dictionary<string, string> args = new Dictionary<string, string>();

		public static void Main (string[] args)
		{
			Console.WriteLine ("Boiler WebSocket Server");
			/*
			 * TODO implement options
			 * 
			OptionSet options = new OptionSet () {
				{"b|boiler=", "the location of {BOILER} framework", (string v) => {MainClass.args.Add("boiler", v);} }
			};

			try {
				options.Parse (args);
			} catch (OptionException e) {
				Console.WriteLine (e.Message);
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			*/

			//TODO change to config file
			IPEndPoint ip = new IPEndPoint(IPAddress.Any, 8282);

			//TODO add TLS
			WAMPServer wamps = new WAMPServer (ip);

			//TODO change to config file
			IPEndPoint ep = new IPEndPoint (IPAddress.Any, 8181);
			Socket s = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try {
				s.Bind(ep);
				s.Listen(100);
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
			}

			//TODO very rudimentary. We should really develop a WAMP client for PHP to publish through the proper WAMP channels
			while (true) {
				Console.WriteLine ("Waiting for request...");
				Socket client = s.Accept();
				Console.WriteLine ("Server Request");
				MemoryStream stream = new MemoryStream ();
				byte[] buf = new byte[1024];
				int recvBytes;
				while ((recvBytes = client.Receive(buf)) > 0) {
					stream.Write (buf, 0, recvBytes);
				}
				stream.Seek (0, SeekOrigin.Begin);
				byte[] message = new byte[stream.Length];
				stream.Read (message, 0, (int)stream.Length);
				JObject sentMessage = JObject.Parse(Encoding.UTF8.GetString(message));
				Console.WriteLine ("Server Message: " + (string)sentMessage["channel"]);
				wamps.PublishEvent ((string)sentMessage ["channel"], "", new JArray (), (JObject)sentMessage ["data"]);
				client.Close ();
			}



			//TODO send WebSocketCloseStatus.GOING_AWAY to clients still connected

		}
	}
}
