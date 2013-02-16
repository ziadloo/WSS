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
		private List<IConnection> newlyConnected;
		private List<IConnection> newlyDisconnected;
		protected ILogger logger;
		private Queue<Frame> incomingFrames = new Queue<Frame>();
		private EventWaitHandle jobToDo = new AutoResetEvent(false);
		private EventWaitHandle exitEvent = new ManualResetEvent(false);
		private Thread dispatchThread;

		public Application()
		{
			connections = new List<IConnection>();
			newlyConnected = new List<IConnection>();
			newlyDisconnected = new List<IConnection>();
			dispatchThread = new Thread(new ThreadStart(dispatchFrames));
		}

		public void SetLogger(ILogger logger)
		{
			this.logger = logger;
		}
		
		public void AddConnection(IConnection client)
		{
			lock (((ICollection)newlyConnected).SyncRoot) {
				newlyConnected.Add(client);
			}
			jobToDo.Set();
		}

		public void RemoveConnection(IConnection client)
		{
			lock (((ICollection)newlyDisconnected).SyncRoot) {
				newlyDisconnected.Add(client);
			}
			jobToDo.Set();
		}
		
		public virtual void Start()
		{
			exitEvent.Reset();
			dispatchThread.Start();
		}
		
		public virtual void Stop()
		{
			exitEvent.Set();
		}
		
		public void EnqueueIncomingFrame(Frame frame)
		{
			lock (((ICollection)incomingFrames).SyncRoot) {
				incomingFrames.Enqueue(frame);
			}
			jobToDo.Set();
		}
		
		protected virtual void dispatchFrames()
		{
			WaitHandle[] exitOrNew = new WaitHandle[2];
			exitOrNew[0] = jobToDo;
			exitOrNew[1] = exitEvent;
	
			while (WaitHandle.WaitAny(exitOrNew) == 0)
			{
				int count = 0;
				lock (((ICollection)newlyConnected).SyncRoot) {
					count += newlyConnected.Count;
				}
				lock (((ICollection)newlyDisconnected).SyncRoot) {
					count += newlyDisconnected.Count;
				}
				lock (((ICollection)incomingFrames).SyncRoot) {
					count += incomingFrames.Count;
				}
				
				while (count > 0) {
					List<IConnection> newConnectionList = new List<IConnection>();
					lock (((ICollection)newlyConnected).SyncRoot) {
						foreach (IConnection con in newlyConnected) {
							newConnectionList.Add(con);
						}
						newlyConnected.Clear();
					}
					foreach (IConnection con in newConnectionList) {
						if (!connections.Contains(con)) {
							connections.Add(con);
							OnConnect(con);
						}
					}
					
					List<IConnection> removedConnectionList = new List<IConnection>();
					lock (((ICollection)newlyDisconnected).SyncRoot) {
						foreach (IConnection con in newlyDisconnected) {
							removedConnectionList.Add(con);
						}
						newlyDisconnected.Clear();
					}
					foreach (IConnection con in removedConnectionList) {
						if (connections.Contains(con)) {
							connections.Remove(con);
							OnDisconnect(con);
						}
					}
	
					Queue<Frame> frameQueue = new Queue<Frame>();
					lock (((ICollection)incomingFrames).SyncRoot) {
						while (incomingFrames.Count > 0) {
							frameQueue.Enqueue(incomingFrames.Dequeue());
						}
					}
					while (frameQueue.Count > 0) {
						OnData(frameQueue.Dequeue());
					}
					
					count = 0;
					lock (((ICollection)newlyConnected).SyncRoot) {
						count += newlyConnected.Count;
					}
					lock (((ICollection)newlyDisconnected).SyncRoot) {
						count += newlyDisconnected.Count;
					}
					lock (((ICollection)incomingFrames).SyncRoot) {
						count += incomingFrames.Count;
					}
				}
			}
		}

		public abstract void OnData(Frame frame);
		public abstract void OnConnect(IConnection client);
		public abstract void OnDisconnect(IConnection client);
	}
}
