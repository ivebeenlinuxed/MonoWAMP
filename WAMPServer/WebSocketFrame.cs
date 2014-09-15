using System;
using System.IO;

namespace WAMPServer
{
	public class WebSocketFrame
	{
		public bool fin = true;
		public bool rsv1;
		public bool rsv2;
		public bool rsv3;
		public byte opcode;
		public bool mask;
		public ulong payloadLength;
		public byte[] maskingKey;
		public byte[] payloadData;


		public WebSocketFrame ()
		{

		}

		public void Parse(byte[] frame) {
			byte buf;
			System.IO.MemoryStream stm = new System.IO.MemoryStream( frame );
			System.IO.BinaryReader rdr = new System.IO.BinaryReader( stm );
			buf = rdr.ReadByte ();
			this.fin = (buf & 0x80) > 0;
			this.rsv1 = (buf & 0x40) > 0;
			this.rsv2 = (buf & 0x20) > 0;
			this.rsv3 = (buf & 0x10) > 0;
			this.opcode = (byte)(buf & 0x0F);
			buf = rdr.ReadByte ();
			this.mask = (buf & 0x80) > 0;
			this.payloadLength = (uint)buf & (0x7F);
			if (this.payloadLength == 126) {
				this.payloadLength = BitConverter.ToUInt16(rdr.ReadBytes(2), 0);
			} else if (this.payloadLength == 127) {
				this.payloadLength = BitConverter.ToUInt64(rdr.ReadBytes(2), 0);
			}
			if (this.mask) {
				this.maskingKey = rdr.ReadBytes(4);
			}

			this.payloadData = this.ToggleDataMask(rdr.ReadBytes ((int)this.payloadLength));
		}

		public byte[] Encode() {
			byte buf = 0x00;
			MemoryStream stream = new MemoryStream ();
			if (this.fin) {
				buf |= 0x80;
			}

			if (this.rsv1) {
				buf |= 0x40;
			}

			if (this.rsv2) {
				buf |= 0x20;
			}

			if (this.rsv3) {
				buf |= 0x10;
			}

			buf |= (byte)(this.opcode & 0x0F);

			stream.WriteByte (buf);

			buf = 0x00;
			buf |= (byte)(this.mask? 0x80 : 0x00);
			if (payloadData.Length >= (Math.Pow(2, 16))) {
				//4 byte
				buf |= 0x7E;
				stream.WriteByte (buf);
				stream.Write(BitConverter.GetBytes((ulong)payloadData.Length), 0, 8);
			} else if (payloadData.Length >= 126) {
				//2 byte
				buf |= 0x7F;
				stream.WriteByte (buf);
				stream.Write(BitConverter.GetBytes((ushort)payloadData.Length), 0, 2);

			} else {
				buf |= (byte)payloadData.Length;
				stream.WriteByte (buf);
			}

			if (this.mask) {
				stream.Write (this.maskingKey, 0, 4);
				stream.Write (this.ToggleDataMask (this.payloadData), 0, this.payloadData.Length);
			} else {
				stream.Write (this.payloadData, 0, this.payloadData.Length);
			}

			stream.Seek (0, SeekOrigin.Begin);
			byte[] outbuf = new byte[stream.Length];
			while ((stream.Length - stream.Position) > 0) {
				long toEnd = stream.Length - stream.Position;
				stream.Read (outbuf, 0, toEnd<1024? (int)toEnd : 1024);
			}
			return outbuf;
		}

		public byte[] ToggleDataMask(byte[] data) {
			byte[] unmaskedData = new byte[data.Length];
			for (int i=0; i<data.Length; i++) {
				unmaskedData [i] = (byte)((int)data[i] ^ (this.maskingKey[i%4]));
			}
			return unmaskedData;
		}

		public bool IsContinuation() {
			return this.opcode == 0;
		}

		public bool IsText() {
			return this.opcode == 0x01;
		}

		public bool IsBinary() {
			return this.opcode == 0x02;
		}

		public bool IsClose() {
			return this.opcode == 0x08;
		}

		public bool IsPing() {
			return this.opcode == 0x90;
		}

		public bool IsPong() {
			return this.opcode == 0x0A;
		}
	}
}

