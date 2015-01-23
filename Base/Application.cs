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
using System.Threading.Tasks;

namespace Base
{
	public abstract class Application
	{
		protected ILogger logger;
		protected string version = "1.0";

		protected List<IConnection> connections;
		private List<IConnection> connectionsToBeAdded;
		private List<IConnection> connectionsToBeRemoved;

		private Thread connectionWorker_Thread;
		private EventWaitHandle[] connectionWorker_Eventhandlers = new EventWaitHandle[2];
		private ManualResetEvent connectionWorker_ExitSignal = new ManualResetEvent(false);
		private AutoResetEvent connectionWorker_ProduceSignal = new AutoResetEvent(false);
		private ManualResetEvent connectionWorker_DoneSignal = new ManualResetEvent(true);

		public string Version
		{
			get { return version; }
		}

        protected bool isStarted = false;
		public bool IsStarted
		{
			get { return isStarted; }
		}

		public IServer Server
		{
			get;
			set;
		}

		public Application()
		{
			connections = new List<IConnection>();
			connectionsToBeAdded = new List<IConnection>();
			connectionsToBeRemoved = new List<IConnection>();
			connectionWorker_Eventhandlers [0] = connectionWorker_ExitSignal;
			connectionWorker_Eventhandlers [1] = connectionWorker_ProduceSignal;
		}

		public virtual void SetLogger(ILogger logger)
		{
			this.logger = logger;
		}

		public void AddConnection(IConnection client)
		{
			lock (((ICollection)connectionsToBeAdded).SyncRoot) {
				if (!connectionsToBeAdded.Contains (client)) {
					connectionsToBeAdded.Add (client);
					connectionWorker_ProduceSignal.Set ();
				}
			}
		}

		public void RemoveConnection(IConnection client)
		{
			lock (((ICollection)connectionsToBeRemoved).SyncRoot) {
				if (!connectionsToBeRemoved.Contains (client)) {
					connectionsToBeRemoved.Add (client);
					connectionWorker_ProduceSignal.Set ();
				}
			}
		}

		public virtual void Start()
		{
			if (!isStarted) {
				isStarted = true;
				connectionWorker_ProduceSignal.Reset();
				connectionWorker_ExitSignal.Reset();
				connectionWorker_DoneSignal.Reset();
				connectionWorker_Thread = new Thread(connectionWorker);
				connectionWorker_Thread.Start();
			}
		}

		public virtual void Stop()
		{
			if (isStarted) {
				isStarted = false;
				lock (((ICollection)connections).SyncRoot) {
					lock (((ICollection)connectionsToBeRemoved).SyncRoot) {
						foreach (IConnection c in connections) {
							if (!connectionsToBeRemoved.Contains(c)) {
								connectionsToBeRemoved.Add (c);
							}
						}
					}
				}
				connectionWorker_ProduceSignal.Set();
				Task.Run(async delegate 
					{ 
						await Task.Delay(1000); 
						connectionWorker_ExitSignal.Set();
					}
				);
				connectionWorker_DoneSignal.WaitOne();
			}
		}

		public void EnqueueIncomingFrame(Frame frame)
		{
            OnData(frame);
		}

		public long GetConnectionCount()
		{
			lock (((ICollection)connections).SyncRoot) {
				return connections.Count;
			}
		}

		private void connectionWorker()
		{
			while (WaitHandle.WaitAny(connectionWorker_Eventhandlers) == 1) {
				List<IConnection> _connectionsToBeAdded;
				List<IConnection> _connectionsToBeRemoved;

				lock (((ICollection)connectionsToBeAdded).SyncRoot) {
					_connectionsToBeAdded = new List<IConnection>(connectionsToBeAdded);
					connectionsToBeAdded.Clear();
				}
				lock (((ICollection)connectionsToBeRemoved).SyncRoot) {
					_connectionsToBeRemoved = new List<IConnection>(connectionsToBeRemoved);
					connectionsToBeRemoved.Clear();
				}

				lock (((ICollection)connections).SyncRoot) {
					foreach (IConnection client in connections) {
						if (!client.Connected && !_connectionsToBeRemoved.Contains(client)) {
							_connectionsToBeRemoved.Add(client);
						}
					}
				}

				if (_connectionsToBeAdded.Count > 0) {
					List<IConnection> newConnections = new List<IConnection>();
					lock (((ICollection)connections).SyncRoot) {
						foreach (IConnection client in _connectionsToBeAdded) {
							if (!connections.Contains(client)) {
								connections.Add(client);
								newConnections.Add(client);
							}
						}
					}
					foreach (IConnection client in newConnections) {
						OnConnect(client);
					}
				}
				if (_connectionsToBeRemoved.Count > 0) {
					List<IConnection> lostConnections = new List<IConnection>();
					lock (((ICollection)connections).SyncRoot) {
						foreach (IConnection client in _connectionsToBeRemoved) {
							if (connections.Contains(client)) {
								connections.Remove(client);
								lostConnections.Add(client);
							}
						}
					}
					foreach (IConnection client in lostConnections) {
						OnDisconnect(client);
					}
					foreach (IConnection client in lostConnections) {
						client.Close();
					}
				}
			}

			connectionWorker_DoneSignal.Set();
		}

		public void PingConnections()
		{
			lock (((ICollection)connections).SyncRoot) {
				foreach (IConnection c in connections) {
					try {
#if LOG_PP
						logger.log("Sending ping to: " + c.IP.ToString());
#endif
						c.Send (new Frame (Frame.OpCodeType.Ping));
					} catch (Exception) {
					}
				}
			}
		}

		public abstract void OnData(Frame frame);
		public abstract void OnConnect(IConnection client);
		public abstract void OnDisconnect(IConnection client);
	}
}
