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

namespace Base
{
	public interface IConnection
	{
		string IP { get; }
		int Port { get; }
		int ConnectionId { get; }
		bool Connected { get; }
		bool IsABridge { get; }
		Application Application { get; }
		IServer Server { get; }
		void Send(Frame frame);
		void Close(bool SayBye = true);
		object Extra { get; set; }
		Session Session { get; set; }
	}
}

