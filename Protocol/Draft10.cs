 /**
 *   Copyright 2013 Mehran Ziadloo
 *   WSS: A WebSocket Server written in C# and .Net (Mono)
 *   (https://github.com/ziadloo/WSS)
 *
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 *
 *       http://www.apache.org/licenses/LICENSE-2.0
 *
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 **/

using System;
using System.Collections.Generic;
using Base;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace Protocol
{
	public class Draft10 : Draft
	{
		protected static readonly string rfc_guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		#region implemented abstract members of Draft for server
		public override Header ParseClientRequestHandshake(List<byte> buffer)
		{
			int bufferUsed = 0;
			Header h = _parseClientHandshake(buffer, ref bufferUsed);
			string v = h.Get("Sec-WebSocket-Version");
			int vv = Int32.Parse(v.Trim());
			if (vv != 7 && vv != 8)
			{
				throw new Exception();
			}
			buffer.RemoveRange(0, bufferUsed);
			return h;
		}

		public override byte[] CreateServerResponseHandshake(Header header)
		{
			Header h = _createResponseHandshake(header);
			return h.ToBytes();
		}

		public override Frame ParseClientFrameBytes(List<byte> buffer)
		{
			if (buffer.Count < 2)
			{
				return null;
			}
			
			byte opcode = (byte)(buffer[0] & 0x0F);
			bool isMasked = Convert.ToBoolean((buffer[1] & 0x80) >> 7);//(buffer[1] & 0x01) == 1;
			int payloadLength = (byte) (buffer[1] & 0x7F);
			Frame.OpCodeType op;
			
			switch (opcode)
			{
				case 1:
					op = Frame.OpCodeType.Text;
					break;
			
				case 2:
					op = Frame.OpCodeType.Binary;
					break;
				
				case 8:
					op = Frame.OpCodeType.Close;
					break;
				
				case 9:
					op = Frame.OpCodeType.Ping;
					break;
				
				case 10:
					op = Frame.OpCodeType.Pong;
					break;
				
				default:
					// Close connection on unknown opcode:
					op = Frame.OpCodeType.Close;
					break;
			}
			
			byte[] mask;
			int payloadOffset;
			int dataLength;
			
			if (payloadLength == 126)
			{
				mask = buffer.GetRange(4, 4).ToArray();
				payloadOffset = 8;
				byte[] temp = buffer.GetRange(2, 2).ToArray();
				Array.Reverse(temp);
				dataLength = BitConverter.ToUInt16(temp, 0) + payloadOffset;
			}
			else if (payloadLength == 127)
			{
				mask = buffer.GetRange(10, 4).ToArray();
				payloadOffset = 14;
				byte[] temp = buffer.GetRange(2, 8).ToArray();
				Array.Reverse(temp);
				dataLength = (int)(BitConverter.ToUInt64(temp, 0)) + payloadOffset;
			}
			else
			{
				mask = buffer.GetRange(2, 4).ToArray();
				payloadOffset = 6;
				dataLength = payloadLength + payloadOffset;
			}

			/**
			 * We have to check for large frames here. socket_recv cuts at 1024 bytes
			 * so if websocket-frame is > 1024 bytes we have to wait until whole
			 * data is transferd. 
			 */
			if (buffer.Count < dataLength)
			{
				return null;
			}
			
			if (isMasked)
			{
				int j;
				for (int i = payloadOffset; i < dataLength; i++)
				{
					j = i - payloadOffset;
					buffer[i] = (byte)(buffer[i] ^ mask[j % 4]);
				}
			}
			else
			{
				payloadOffset = payloadOffset - 4;
			}
			
			switch (op)
			{
				case Frame.OpCodeType.Binary:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(data);
				}
				
				case Frame.OpCodeType.Text:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(System.Text.Encoding.UTF8.GetString(data));
				}
				
				default:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(op, data);
				}
			}
		}

		public override byte[] CreateServerFrameBytes(Frame frame)
		{
			byte[] data = new byte[0];
			if (frame.Data != null)
			{
				data = frame.Data;
			}
			else if (frame.Message != null)
			{
				data = System.Text.Encoding.UTF8.GetBytes(frame.Message);
			}

			List<byte> frameBytes = new List<byte>();
			int payloadLength = data.Length;
			
			frameBytes.Add(0);
			switch (frame.OpCode)
			{
				case Frame.OpCodeType.Text:
					// first byte indicates FIN, Text-Frame (10000001):
					frameBytes[0] = 129;
					break;			
			
				case Frame.OpCodeType.Close:
					// first byte indicates FIN, Close Frame(10001000):
					frameBytes[0] = 136;
					break;
			
				case Frame.OpCodeType.Ping:
					// first byte indicates FIN, Ping frame (10001001):
					frameBytes[0] = 137;
					break;
			
				case Frame.OpCodeType.Pong:
					// first byte indicates FIN, Pong frame (10001010):
					frameBytes[0] = 138;
					break;
			}
			
			// set mask and payload length (using 1, 3 or 9 bytes) 
			if (payloadLength > 65535)
			{
				byte[] temp = BitConverter.GetBytes((long)payloadLength);
				Array.Reverse(temp);
				frameBytes.Add(0);
				frameBytes[1] = 127;
				for (int i = 0; i < 8; i++)
				{
					frameBytes.Add(0);
					frameBytes[i+2] = temp[i];
				}
				// most significant bit MUST be 0 (close connection if frame too big)
				if (frameBytes[2] > 127)
				{
					//$this->close(1004);
				}
			}
			else if (payloadLength > 125)
			{
				byte[] temp = BitConverter.GetBytes((Int16)payloadLength);
				frameBytes.Add(0);
				frameBytes[1] = 126;
				frameBytes.Add(0);
				frameBytes[2] = temp[1];
				frameBytes.Add(0);
				frameBytes[3] = temp[0];
			}
			else
			{
				frameBytes.Add(0);
				frameBytes[1] = (byte)(payloadLength);
			}
	
	
			// append payload to frame:
			for (int i = 0; i < payloadLength; i++)
			{
				frameBytes.Add(data[i]);
			}
			
			return frameBytes.ToArray();
		}
		#endregion

		#region implemented abstract members of Draft for client

		public override byte[] CreateClientRequestHandshake(string url, out string expectedAccept)
		{
			Header header = _createRequestHandshake(url, out expectedAccept);
			return header.ToBytes();
		}

		public override Header ParseServerResponseHandshake(List<byte> buffer)
		{
			int bufferUsed = 0;
			Header h = _parseServerHandshake(buffer, ref bufferUsed);
			string v = h.Get("Sec-WebSocket-Version");
			int vv = Int32.Parse(v.Trim());
			if (vv != 7 && vv != 8)
			{
				throw new Exception();
			}
			buffer.RemoveRange(0, bufferUsed);
			return h;
		}

		public override byte[] CreateClientFrameBytes(Frame frame)
		{
			byte[] data = new byte[0];
			if (frame.Data != null)
			{
				data = frame.Data;
			}
			else if (frame.Message != null)
			{
				data = System.Text.Encoding.UTF8.GetBytes(frame.Message);
			}

			List<byte> frameBytes = new List<byte>();
			int payloadLength = data.Length;

			frameBytes.Add(0);
			if (payloadLength < 126)
			{
				frameBytes.Add((byte)payloadLength);
			}
			else if (payloadLength < 65536)
			{
				frameBytes.Add((byte)126);
				frameBytes.Add((byte)(payloadLength / 256));
				frameBytes.Add((byte)(payloadLength % 256));
			}
			else
			{
				frameBytes.Add((byte)127);

				int left = payloadLength;
				int unit = 256;
				byte[] fragment = new byte[10];

				for (int i = 9; i > 1; i--)
				{
					fragment[i] = (byte)(left % unit);
					left = left / unit;

					if (left == 0)
					{
						break;
					}
				}

				for (int i = 2; i < 10; i++)
				{
					frameBytes.Add(fragment[i]);
				}
			}

			//Set FIN
			frameBytes[0] = (byte)((byte)frame.OpCode | 0x80);

			//Set mask bit
			frameBytes[1] = (byte)(frameBytes[1] | 0x80);

			//Mask
			byte[] mask = new byte[4];
			for (int i = 0; i < 4; i++)
			{
				mask[i] = (byte)(DateTime.Now.Ticks % 256);
				frameBytes.Add(mask[i]);
			}

			for (var i = 0; i < payloadLength; i++)
			{
				frameBytes.Add((byte)(data[i] ^ mask[i % 4]));
			}

			return frameBytes.ToArray();
		}

		public override Frame ParseServerFrameBytes(List<byte> buffer)
		{
			if (buffer.Count < 2)
			{
				return null;
			}

			// FIN
			Frame.FinType fin = (buffer[0] & 0x80) == 0x80 ? Frame.FinType.Final : Frame.FinType.More;
			// RSV1
			Frame.RsvType rsv1 = (buffer[0] & 0x40) == 0x40 ? Frame.RsvType.On : Frame.RsvType.Off;
			// RSV2
			Frame.RsvType rsv2 = (buffer[0] & 0x20) == 0x20 ? Frame.RsvType.On : Frame.RsvType.Off;
			// RSV3
			Frame.RsvType rsv3 = (buffer[0] & 0x10) == 0x10 ? Frame.RsvType.On : Frame.RsvType.Off;
			// Opcode
			Frame.OpCodeType opcode = (Frame.OpCodeType)(buffer[0] & 0x0f);
			// MASK
			var isMasked = (buffer[1] & 0x80) == 0x80;
			// Payload len
			var payloadLength = (byte)(buffer[1] & 0x7f);
			var extLength = payloadLength < 126 ? 0 : payloadLength == 126 ? 2 : 8;

			if ((opcode == Frame.OpCodeType.Close || opcode == Frame.OpCodeType.Ping || opcode == Frame.OpCodeType.Pong)
		    && payloadLength > 125)
			{
				throw new Exception("The payload length of a control frame must be 125 bytes or less.");
				//return createCloseFrame(CloseStatusCode.INCONSISTENT_DATA, "The payload length of a control frame must be 125 bytes or less.", Mask.UNMASK);
			}

			if (extLength > 0 && buffer.Count < extLength)
			{
				throw new Exception("'Extended Payload Length' of a frame cannot be read from the data stream.");
				//return createCloseFrame(CloseStatusCode.ABNORMAL, "'Extended Payload Length' of a frame cannot be read from the data stream.", Mask.UNMASK);
			}

			//Mask
			byte[] mask = new byte[0];
			int payloadOffset = 2;
			int dataLength;

			if (isMasked)
			{
				if (buffer.Count <= payloadOffset + 4)
				{
					throw new Exception("Unfinished buffer");
				}
				mask = buffer.GetRange(payloadOffset, 4).ToArray();
				payloadOffset += 4;
			}

			if (payloadLength < 126)
			{
				dataLength = payloadLength;
			}
			else if (payloadLength == 126)
			{
				byte[] temp = buffer.GetRange(payloadOffset, 2).ToArray();
				payloadOffset += 2;
				Array.Reverse(temp);
				dataLength = BitConverter.ToUInt16(temp, 0);
			}
			else
			{
				byte[] temp = buffer.GetRange(payloadOffset, 8).ToArray();
				payloadOffset += 8;
				Array.Reverse(temp);
				dataLength = (int)(BitConverter.ToUInt64(temp, 0));
			}

			/**
			 * We have to check for large frames here. socket_recv cuts at 1024 bytes
			 * so if websocket-frame is > 1024 bytes we have to wait until whole
			 * data is transferd. 
			 */
			if (buffer.Count < dataLength + payloadOffset)
			{
				return null;
			}

			Frame f;
			switch (opcode)
			{
				case Frame.OpCodeType.Binary:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength).ToArray();
					if (isMasked)
					{
						for (int i = 0; i < data.Length; i++)
						{
							data[i] = (byte)(data[i] ^ mask[i % 4]);
						}
					}
					buffer.RemoveRange(0, dataLength + payloadOffset);
					f = new Frame(data);
					break;
				}

				case Frame.OpCodeType.Text:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength).ToArray();
					if (isMasked)
					{
						for (int i = 0; i < data.Length; i++)
						{
							data[i] = (byte)(data[i] ^ mask[i % 4]);
						}
					}
					buffer.RemoveRange(0, dataLength + payloadOffset);
					f = new Frame(System.Text.Encoding.UTF8.GetString(data));
					break;
				}

				default:
				{
					byte[] data = buffer.GetRange(payloadOffset, dataLength).ToArray();
					if (isMasked)
					{
						for (int i = 0; i < data.Length; i++)
						{
							data[i] = (byte)(data[i] ^ mask[i % 4]);
						}
					}
					buffer.RemoveRange(0, dataLength + payloadOffset);
					f = new Frame(opcode, data);
					break;
				}
			}

			f.Fin = fin;
			f.Rsv1 = rsv1;
			f.Rsv2 = rsv2;
			f.Rsv3 = rsv3;

			return f;
		}

		#endregion
		
		protected Header _parseClientHandshake(List<byte> buffer, ref int bufferUsed)
		{
			bufferUsed = 0;
			if (buffer.Count == 0)
			{
				return null;
			}
			
			string request = ReadOneLine(buffer);
			// check for valid http-header:
			Regex r = new Regex("^GET (\\S+) HTTP\\/1.1$");
			Match matches = r.Match(request);
			if (!matches.Success)
			{
				throw new Exception();
			}

			// check for valid application:
			bufferUsed += request.Length + 2;
			Header header = new Header(matches.Groups[1].ToString());

			// generate headers array:
			string line;
			r = new Regex("^(\\S+): (.*)$");
			while ((line = ReadOneLine(buffer, bufferUsed)) != null)
			{
				bufferUsed += line.Length + 2;
				line = line.TrimStart();
				matches = r.Match(line);
				if (matches.Success)
				{
					header.Set(matches.Groups[1].ToString(), matches.Groups[2].ToString());
				}
			}
			
			return header;
		}
		
		protected Header _parseServerHandshake(List<byte> buffer, ref int bufferUsed)
		{
			bufferUsed = 0;
			if (buffer.Count == 0)
			{
				return null;
			}

			string request = ReadOneLine(buffer);
			// check for valid http-header:
			Regex r = new Regex("^HTTP/1.1 .*$");
			Match matches = r.Match(request);
			if (!matches.Success)
			{
				throw new Exception();
			}

			// check for valid application:
			bufferUsed += request.Length + 2;
			Header header = new Header(matches.Groups[0].ToString());

			// generate headers array:
			string line;
			r = new Regex("^(\\S+): (.*)$");
			while ((line = ReadOneLine(buffer, bufferUsed)) != null)
			{
				bufferUsed += line.Length + 2;
				line = line.TrimStart();
				matches = r.Match(line);
				if (matches.Success)
				{
					header.Set(matches.Groups[1].ToString(), matches.Groups[2].ToString());
				}
			}

			return header;
		}
		
		protected Header _createResponseHandshake(Header header)
		{
			string secKey = header.Get("sec-websocket-key");
			string secAccept = "";
			{
				var rawAnswer = secKey.Trim() + rfc_guid;
				secAccept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(rawAnswer)));
			}

			Header h = new Header("HTTP/1.1 101 Switching Protocols");
			h.Set("Upgrade", "websocket");
			h.Set("Connection", "Upgrade");
			h.Set("Sec-WebSocket-Accept", secAccept);
			if (header.Get("sec-websocket-protocol") != null && header.Get("sec-websocket-protocol").Trim() != "")
			{
				h.Set("Sec-WebSocket-Protocol", header.URL.Substring(1));
			}

			return h;
		}
		
		protected Header _createRequestHandshake(string url, out string expectedAccept)
		{
			Regex regex = new Regex(@"^([^:]+)://([^/:]+)(:(\d+))?(/.*)?$");
			Match mtch = regex.Match(url.Trim());
			if (!mtch.Success)
			{
				throw new Exception ("Invalid URL: " + url);
			}

			string protocol = mtch.Groups[1].Value;
			string server = mtch.Groups[2].Value;
			string port = mtch.Groups[4].Value;
			string path = mtch.Groups[5].Value;

			string secKey = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));
			expectedAccept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(secKey + rfc_guid)));

			Header header = new Header(String.Format("GET {0} HTTP/1.1", string.IsNullOrEmpty(path) ? "/" : path));
			header.Set("Upgrade", "WebSocket");
			header.Set("Connection", "Upgrade");
			header.Set("Sec-WebSocket-Version", "13");
			header.Set("Sec-WebSocket-Key", secKey);
			if (string.IsNullOrEmpty(port))
			{
				header.Set("Host", server);
			}
			else
			{
				header.Set("Host", server + ":" + port);
			}
			header.Set("Origin", server);
			return header;
		}
	}
}

