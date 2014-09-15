using System;

namespace WAMPServer
{
	public struct WebsocketOpcode
	{
		CONTINUATION,
		TEXT,
		BINARY,
		CLOSE,
		PING,
		PONG
	}
}

