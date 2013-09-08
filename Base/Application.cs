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
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

namespace Base
{
	public abstract class Application
	{
		protected List<IConnection> connections;
		protected ILogger logger;
		protected string version = "1.0";

		public string Version
		{
			get { return version; }
		}

		protected bool isStarted = false;
		public bool IsStarted
		{
			get { return isStarted; }
		}

		public Application()
		{
			connections = new List<IConnection>();
		}

		public void SetLogger(ILogger logger)
		{
			this.logger = logger;
		}

		public void AddConnection(IConnection client)
		{
			bool newConnection = false;
			lock (((ICollection)connections).SyncRoot)
			{
				if (!connections.Contains(client))
				{
					newConnection = true;
					connections.Add(client);
				}
			}
			if (newConnection)
			{
				OnConnect(client);
			}
		}

		public void RemoveConnection(IConnection client)
		{
			bool found = false;
			lock (((ICollection)connections).SyncRoot)
			{
				if (connections.Contains(client))
				{
					found = true;
					connections.Remove(client);
				}
			}
			if (found)
			{
				OnDisconnect(client);
			}
		}

		public virtual void Start()
		{
			isStarted = true;
		}

		public virtual void Stop()
		{
			isStarted = false;
		}

		public void EnqueueIncomingFrame(Frame frame)
		{
			OnData(frame);
		}

		public abstract void OnData(Frame frame);
		public abstract void OnConnect(IConnection client);
		public abstract void OnDisconnect(IConnection client);
	}
}
