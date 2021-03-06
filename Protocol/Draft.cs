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
using System.Text;
using System.Collections.Generic;
using Base;

namespace Protocol
{
	public abstract class Draft
	{
		protected string ReadOneLine(List<byte> buffer, int start = 0)
		{
			start = start < 0 ? 0 : start;
			if (buffer == null || buffer.Count - start < 2)
			{
				return null;
			}

			int end = start;
			for (int i=start+1; i<buffer.Count; i++)
			{
				if (buffer[i-1] == '\r' && buffer[i] == '\n')
				{
					end = i;
					break;
				}
			}
			if (end <= start)
			{
				return null;
			}
			else
			{
				ASCIIEncoding encoder = new ASCIIEncoding();
				return encoder.GetString(buffer.ToArray(), start, end - start - 1);
			}
		}

		#region server part of the protocol
		public abstract Header ParseClientRequestHandshake(List<byte> buffer);
		public abstract byte[] CreateServerResponseHandshake(Header header);
		public abstract Frame ParseClientFrameBytes(List<byte> buffer);
		public abstract byte[] CreateServerFrameBytes(Frame frame);
		#endregion

		#region client part of the protocol
		public abstract byte[] CreateClientRequestHandshake(string url, out string expectedAccept);
		public abstract Header ParseServerResponseHandshake(List<byte> buffer);
		public abstract byte[] CreateClientFrameBytes(Frame frame);
		public abstract Frame ParseServerFrameBytes(List<byte> buffer);
		#endregion
	}
}

