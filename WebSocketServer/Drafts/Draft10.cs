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

namespace WebSocketServer.Drafts
{
	public class Draft10 : Draft
	{
		#region implemented abstract members of WebSocketServer.Draft.Draft
		public override Header ParseHandshake(List<byte> buffer)
		{
			int bufferUsed = 0;
			Header h = _parseHandshake(buffer, ref bufferUsed);
			string v = h.Get("Sec-WebSocket-Version");
			int vv = Int32.Parse(v.Trim());
			if (vv != 7 && vv != 8) {
				throw new Exception();
			}
			buffer.RemoveRange(0, bufferUsed);
			return h;
		}

		public override byte[] CreateResponseHandshake(Header header)
		{
			Header h = _createResponseHandshake(header);
			return h.ToBytes();
		}

		public override Frame ParseFrameBytes(List<byte> buffer)
		{
			if (buffer.Count < 2) {
				return null;
			}
			
			byte opcode = (byte)(buffer[0] & 0x0F);
			bool isMasked = Convert.ToBoolean((buffer[1] & 0x80) >> 7);//(buffer[1] & 0x01) == 1;
			int payloadLength = (byte) (buffer[1] & 0x7F);
			Frame.OpCodeType op;
			
			switch (opcode) {
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
			
			if (payloadLength == 126) {
				mask = buffer.GetRange(4, 4).ToArray();
				payloadOffset = 8;
				byte[] temp = buffer.GetRange(2, 2).ToArray();
				Array.Reverse(temp);
				dataLength = BitConverter.ToUInt16(temp, 0) + payloadOffset;
			}
			else if (payloadLength == 127) {
				mask = buffer.GetRange(10, 4).ToArray();
				payloadOffset = 14;
				byte[] temp = buffer.GetRange(2, 8).ToArray();
				Array.Reverse(temp);
				dataLength = (int)(BitConverter.ToUInt64(temp, 0)) + payloadOffset;
			}
			else {
				mask = buffer.GetRange(2, 4).ToArray();
				payloadOffset = 6;
				dataLength = payloadLength + payloadOffset;
			}

			/**
			 * We have to check for large frames here. socket_recv cuts at 1024 bytes
			 * so if websocket-frame is > 1024 bytes we have to wait until whole
			 * data is transferd. 
			 */
			if (buffer.Count < dataLength) {
				return null;
			}
			
			if (isMasked) {
				int j;
				for (int i = payloadOffset; i < dataLength; i++) {
					j = i - payloadOffset;
					buffer[i] = (byte)(buffer[i] ^ mask[j % 4]);
				}
			}
			else {
				payloadOffset = payloadOffset - 4;
			}
			
			switch (op) {
				case Frame.OpCodeType.Binary: {
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(data);
				}
				
				case Frame.OpCodeType.Text: {
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(System.Text.Encoding.UTF8.GetString(data));
				}
				
				default: {
					byte[] data = buffer.GetRange(payloadOffset, dataLength - payloadOffset).ToArray();
					buffer.RemoveRange(0, dataLength);
					return new Frame(op, data);
				}
			}
		}

		public override byte[] CreateFrameBytes(Frame frame)
		{
			byte[] data = new byte[0];
			if (frame.Data != null) {
				data = frame.Data;
			}
			else if (frame.Message != null) {
				data = System.Text.Encoding.UTF8.GetBytes(frame.Message);
			}

			List<byte> frameBytes = new List<byte>();
			int payloadLength = data.Length;
			
			frameBytes.Add(0);
			switch (frame.OpCode) {
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
			if (payloadLength > 65535) {
				byte[] temp = BitConverter.GetBytes((long)payloadLength);
				Array.Reverse(temp);
				frameBytes.Add(0);
				frameBytes[1] = 127;
				for (int i = 0; i < 8; i++) {
					frameBytes.Add(0);
					frameBytes[i+2] = temp[i];
				}
				// most significant bit MUST be 0 (close connection if frame too big)
				if (frameBytes[2] > 127) {
					//$this->close(1004);
				}
			}
			else if (payloadLength > 125) {
				byte[] temp = BitConverter.GetBytes((Int16)payloadLength);
				frameBytes.Add(0);
				frameBytes[1] = 126;
				frameBytes.Add(0);
				frameBytes[2] = temp[1];
				frameBytes.Add(0);
				frameBytes[3] = temp[0];
			}
			else {
				frameBytes.Add(0);
				frameBytes[1] = (byte)(payloadLength);
			}
	
	
			// append payload to frame:
			for (int i = 0; i < payloadLength; i++) {		
				frameBytes.Add(data[i]);
			}
			
			return frameBytes.ToArray();
		}
		#endregion
		
		protected Header _parseHandshake(List<byte> buffer, ref int bufferUsed)
		{
			bufferUsed = 0;
			if (buffer.Count == 0) {
				return null;
			}
			
			string request = ReadOneLine(buffer);
			// check for valid http-header:
			Regex r = new Regex("^GET (\\S+) HTTP\\/1.1$");
			Match matches = r.Match(request);
			if (!matches.Success) {
				throw new Exception();
			}

			// check for valid application:
			bufferUsed += request.Length + 2;
			Header header = new Header(matches.Groups[1].ToString());

			// generate headers array:
			string line;
			r = new Regex("^(\\S+): (.*)$");
			while ((line = ReadOneLine(buffer, bufferUsed)) != null) {
				bufferUsed += line.Length + 2;
				line = line.TrimStart();
				matches = r.Match(line);
				if (matches.Success) {
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
                var rawAnswer = secKey.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                var hasher = SHA1.Create();
                secAccept = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(rawAnswer)));
			}

			Header h = new Header("HTTP/1.1 101 Switching Protocols");
			h.Set("Upgrade", "websocket");
			h.Set("Connection", "Upgrade");
			h.Set("Sec-WebSocket-Accept", secAccept);
			if (header.Get("sec-websocket-protocol") != null && header.Get("sec-websocket-protocol").Trim() != "") {
				h.Set("Sec-WebSocket-Protocol", header.URL.Substring(1));
			}
			
			return h;
		}
	}
}

