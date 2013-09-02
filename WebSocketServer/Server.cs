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
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Net;
using WebSocketServer.Drafts;
using System.Threading.Tasks;
using System.Collections;

namespace WebSocketServer
{
	public class Server : Application, ILogger
	{
		private Dictionary<string, Application> applications;
		private TcpListener tcpListener;
		private bool _checkOrigin = false;
		private Draft[] drafts;
		private bool exitEvent = false;
		
		public Draft[] Drafts
		{
			get { return drafts; }
		}

		public Server(ILogger l)
		{
			logger = l;
			applications = new Dictionary<string, Application>();
			
			string serverFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string applicationfolder = Path.Combine(serverFolder, "Applications");
			if (Directory.Exists(applicationfolder))
			{
				Type abstractType = typeof(Application);
				string[] dllpaths = Directory.GetFiles(applicationfolder, "*.dll");
				foreach (string dllpath in dllpaths)
				{
					Assembly dll = Assembly.LoadFile(dllpath);
					Type[] types = dll.GetExportedTypes();
					foreach (Type type in types)
					{
						if (abstractType.IsAssignableFrom(type))
						{
							Application obj = (Application)Activator.CreateInstance(type);
							obj.SetLogger(logger);
							applications.Add(Path.GetFileNameWithoutExtension(dllpath), obj);
						}
					}
				}
			}

			drafts = new Draft[2];
			drafts[0] = new Draft10();
			drafts[1] = new Draft17();
		}
		
		public Application getApplication(string path)
		{
			lock (((ICollection)applications).SyncRoot)
			{
				if (applications.ContainsKey(path))
				{
					return applications[path];
				}
				else
				{
					return null;
				}
			}
		}

		public bool CheckOrigin
		{
			get { return _checkOrigin; }
		}

		public bool checkOrigin(string origin)
		{
			return true;
		}

		#region ILogger implementation
		void ILogger.log(string msg)
		{
			logger.log(msg);
		}

		void ILogger.warn(string msg)
		{
			logger.warn(msg);
		}

		void ILogger.error(string msg)
		{
			logger.error(msg);
		}

		void ILogger.info(string msg)
		{
			logger.info(msg);
		}
		#endregion

		#region implemented abstract members of Base.Application
		public override void Start()
		{
			lock (((ICollection)applications).SyncRoot)
			{
				foreach (Application app in applications.Values)
				{
					app.Start();
				}
			}
			exitEvent = false;
			tcpListener = new TcpListener(IPAddress.Any, 8080);
			
			tcpListener.Start();
			startAccept();
			
			base.Start();
		}

		private void startAccept()
		{
			try
			{
				if (!exitEvent)
				{
					tcpListener.BeginAcceptTcpClient(handleAsyncConnection, tcpListener);
				}
			}
			catch (Exception ex)
			{
				int count = 0;
				lock (((ICollection)connections).SyncRoot)
				{
					count = connections.Count;
				}
				logger.error("Begining accepting a client failed. Number of connections: " + count.ToString() + ". Error message: " + ex.Message);
			}
		}

		private void handleAsyncConnection(IAsyncResult res)
		{
			try
			{
				if (!exitEvent)
				{
					startAccept(); //listen for new connections again
					TcpClient client = tcpListener.EndAcceptTcpClient(res);
					if (client != null)
					{
						new Connection(client, this);
					}
				}
			}
			catch (Exception ex)
			{
				int count = 0;
				lock (((ICollection)connections).SyncRoot)
				{
					count = connections.Count;
				}
				logger.error("Accepting a client failed. Number of connections: " + count.ToString() + ". Error message: " + ex.Message);
			}
		}
		
		public override void Stop()
		{
			lock (((ICollection)connections).SyncRoot)
			{
				foreach (IConnection con in connections)
				{
					con.Close();
				}
			}
			lock (((ICollection)applications).SyncRoot)
			{
				foreach (Application app in applications.Values)
				{
					app.Stop();
				}
			}
			exitEvent = true;
			base.Stop();
			tcpListener.Stop();
		}

		public override void OnData(Frame frame)
		{
		}

		public override void OnConnect(IConnection client)
		{
		}

		public override void OnDisconnect(IConnection client)
		{
		}
		#endregion
	}
}

