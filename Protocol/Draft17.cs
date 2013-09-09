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
using Base;
using System.Collections.Generic;

namespace WebSocketServer.Drafts
{
	public class Draft17 : Draft10
	{
		public override Header ParseHandshake(List<byte> buffer)
		{
			int bufferUsed = 0;
			Header h = _parseHandshake(buffer, ref bufferUsed);
			string v = h.Get("Sec-WebSocket-Version");
			int vv = Int32.Parse(v.Trim());
			if (vv != 13)
			{
				throw new Exception();
			}
			buffer.RemoveRange(0, bufferUsed);
			return h;
		}

		public override byte[] CreateResponseHandshake(Header header)
		{
			Header h = _createResponseHandshake(header);
			h.Set("Sec-WebSocket-Version", "13");
			return h.ToBytes();
		}
	}
}

