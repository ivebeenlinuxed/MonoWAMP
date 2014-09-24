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
		public byte[] payloadData = new byte[0];


		public WebSocketFrame ()
		{

		}

		public static bool HaveFullPacket(byte[] stream) {
			ulong extraBytes = 2;
			if ((ulong)stream.Length < extraBytes) {
				return false;
			}


			if ((stream [1] & 0x80) > 0) {
				//Add a byte for the mask
				extraBytes += 4;
			}

			//TODO Packet too long error? WebSocketCloseStatus.MESSAGE_TOO_BIG
			if (((uint)stream [1] & 0x7F) == 126) {
				extraBytes += 2;
				if ((ulong)stream.Length < extraBytes) {
					return false;
				}
				
				byte[] packetSize = new byte[2];
				Array.Copy (stream, 2, packetSize, 0, 2);
				packetSize = ToNetworkByteOrder (packetSize);
				extraBytes += BitConverter.ToUInt16 (packetSize, 0);
			} else if (((uint)stream [1] & 0x7F) == 127) {
				extraBytes += 8;
				if ((ulong)stream.Length < extraBytes) {
					return false;
				}
				byte[] packetSize = new byte[8];
				Array.Copy (stream, 2, packetSize, 0, 8);
				packetSize = ToNetworkByteOrder (packetSize);
				extraBytes += BitConverter.ToUInt64 (packetSize, 0);
			} else {
				extraBytes += (uint)stream [1] & 0x7F;
			}



			if ((ulong)stream.Length < extraBytes) {
				return false;
			}
			return true;
		}

		private static byte[] ToNetworkByteOrder(byte[] input) {
			if (BitConverter.IsLittleEndian) {
				Array.Reverse (input);
			}
			return input;
		}

		public ulong Parse(byte[] frame) {
			byte buf;
			System.IO.MemoryStream stm = new System.IO.MemoryStream( frame );
			System.IO.BinaryReader rdr = new System.IO.BinaryReader( stm );
			buf = rdr.ReadByte ();
			//TODO chrome closes if RSV1-3 are set? Should we WebSocketCloseStatus.PROTOCOL_ERROR
			this.fin = (buf & 0x80) > 0;
			this.rsv1 = (buf & 0x40) > 0;
			this.rsv2 = (buf & 0x20) > 0;
			this.rsv3 = (buf & 0x10) > 0;
			this.opcode = (byte)(buf & 0x0F);
			buf = rdr.ReadByte ();
			this.mask = (buf & 0x80) > 0;
			this.payloadLength = (uint)buf & (0x7F);
			if (this.payloadLength == 126) {
				byte[] endianLength = rdr.ReadBytes(2);
				if (BitConverter.IsLittleEndian) {
					Array.Reverse (endianLength);
				}
				this.payloadLength = BitConverter.ToUInt16(endianLength, 0);
			} else if (this.payloadLength == 127) {
				byte[] endianLength = rdr.ReadBytes(8);
				if (BitConverter.IsLittleEndian) {
					Array.Reverse (endianLength);
				}
				this.payloadLength = BitConverter.ToUInt16(endianLength, 0);
			}
			if (this.mask) {
				this.maskingKey = rdr.ReadBytes (4);
				this.payloadData = this.ToggleDataMask (rdr.ReadBytes ((int)this.payloadLength));
			} else {
				this.payloadData = rdr.ReadBytes ((int)this.payloadLength);
			}

			return (ulong)stm.Position;
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

			byte[] packetLength = BitConverter.GetBytes ((long)payloadData.Length);
			if (BitConverter.IsLittleEndian) {
				Array.Reverse (packetLength);
			}
			if (payloadData.Length >= 65536) {
				//8 byte
				buf |= 0x7F;
				stream.WriteByte (buf);
				stream.Write (packetLength, 0, 8);
			} else if (payloadData.Length >= 126) {
				//2 byte
				buf |= 0x7E;
				stream.WriteByte (buf);
				stream.Write (packetLength, 6, 2);
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
			//TODO technically stream.Length could be a 64 bit value. Read, however, only accepts 32-bit arguments
			byte[] outbuf = new byte[stream.Length];
			stream.Read (outbuf, 0, (int)stream.Length);
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

