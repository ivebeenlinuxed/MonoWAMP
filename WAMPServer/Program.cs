using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;

namespace WAMPServer
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Hello World!");
			IPEndPoint ip = new IPEndPoint(IPAddress.Any, 8080);
			WAMPServer wamps = new WAMPServer (ip);


			Socket s = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IPEndPoint ep = new IPEndPoint (IPAddress.Any, 8181);
			try {
				s.Bind(ep);
				s.Listen(100);
			} catch (Exception e) {
				Console.WriteLine (e.ToString ());
			}
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





		}
	}
}
