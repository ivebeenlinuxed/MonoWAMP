using System;
using System.Net;

namespace WAMPServer
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Hello World!");
			IPEndPoint ip = new IPEndPoint(IPAddress.Any, 8080);
			WAMPServer wamps = new WAMPServer (ip);
			
			while (true) {
				//Console.WriteLine ("Server Alive");
				System.Threading.Thread.Sleep (5000);
			}

		}
	}
}
