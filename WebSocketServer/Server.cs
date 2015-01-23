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
using Protocol;
using System.Threading.Tasks;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Collections.Concurrent;

namespace WebSocketServer
{
	public class Server : Application, ILogger, IServer
	{
		private ConcurrentDictionary<string, Application> applications;
		private TcpListener tcpListener;
		private TcpListener tcpSecureListener;
		private bool _checkOrigin = false;
		private Draft[] drafts;
		private bool exitEvent = false;
		private ConcurrentDictionary<IConnection, DateTime> lastPongs = new ConcurrentDictionary<IConnection, DateTime>();
		private Thread pingPongThread;
		
		public Draft[] Drafts
		{
			get { return drafts; }
		}
		
		public bool CheckOrigin
		{
			get { return _checkOrigin; }
		}

		public bool checkOrigin(string origin)
		{
			return true;
		}

		public Server(ILogger logger)
		{
			this.logger = logger;
			applications = new ConcurrentDictionary<string, Application>();

			((IServer)this).ReloadApplications();

			drafts = new Draft[2];
			drafts[0] = new Draft10();
			drafts[1] = new Draft17();
		}

		public void OnPonged(IConnection connection)
		{
#if LOG_PP
			logger.log("Pong recieved for: " + f.Connection.IP.ToString());
#endif
			DateTime previousTime = DateTime.Now;
			lastPongs.TryGetValue(connection, out previousTime);
			lastPongs.TryUpdate(connection, DateTime.Now, previousTime);
		}

		private void keepingConnectionAliveWithPingPong()
		{
			while (true)
			{
				List<IConnection> toBeRemoved = new List<IConnection>();
				foreach (KeyValuePair<IConnection, DateTime> kvp in lastPongs)
				{
					if ((DateTime.Now - kvp.Value).TotalMilliseconds > Config.Instance.GetValue("Server", "PongMaxWaitMiliSec", 5000))
					{
						toBeRemoved.Add(kvp.Key);
					}
				}
				foreach (IConnection c in toBeRemoved)
				{
					c.Close();
				}

				foreach (KeyValuePair<string, Application> kvp in applications) {
					kvp.Value.PingConnections();
				}

				Thread.Sleep(Config.Instance.GetValue("Server", "PingPongThreadSleepMiliSec", 1000));
			}
		}

		#region implemented abstract members of Base.IServer

		void IServer.AddApplication(string name, Application app)
		{
			app.SetLogger(logger);
			app.Server = (IServer)this;
			applications.TryAdd(name, app);
		}

		void IServer.ReloadApplications()
		{
			string serverFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			string applicationfolder = Path.Combine(serverFolder, Config.Instance.GetValue("Server", "plugin_folder_name", "Applications"));

			List<string> foundApplications = new List<string>();
			if (Directory.Exists(applicationfolder))
			{
				Type abstractType = typeof(Application);
				string[] dllpaths = Directory.GetFiles(applicationfolder, "*.dll");
				foreach (string dllpath in dllpaths)
				{
					logger.log("Found dll: " + dllpath);
					Assembly dll = Assembly.LoadFile(dllpath);
					Type[] types = dll.GetExportedTypes();
					foreach (Type type in types)
					{
						if (abstractType.IsAssignableFrom(type))
						{
							string name = Path.GetFileNameWithoutExtension(dllpath);
							foundApplications.Add(name);

							try {
								Application oldApp = null;
								logger.log("Trying to instantiate application: " + name);
								Application obj = (Application)Activator.CreateInstance(type);
								if (!applications.TryGetValue(name, out oldApp))
								{
									obj.SetLogger(logger);
									obj.Server = (IServer)this;
									applications.TryAdd(name, obj);
								}
								else
								{
									if (obj.Version != oldApp.Version)
									{
										obj.SetLogger(logger);
										applications[name] = obj;
										if (oldApp.IsStarted)
										{
											oldApp.Stop();
											obj.Start();
										}
									}
								}
							}
							catch (Exception ex) {
								logger.error("Failed to add application: " + name + ", error message: " + ex.Message);
							}
							break;
						}
					}
				}
			}

			List<string> applicationsToRemove = new List<string>();
			foreach (KeyValuePair<string, Application> kvp in applications)
			{
				if (!foundApplications.Contains(kvp.Key))
				{
					applicationsToRemove.Add(kvp.Key);
				}
			}
			foreach (string appName in applicationsToRemove)
			{
				Application app = null;
				applications.TryRemove(appName, out app);
				if (app != null && app.IsStarted)
				{
					app.Stop();
				}
			}
		}

		void IServer.ReloadConfig()
		{
			Config.Reload();
		}

		void IServer.Reload()
		{
			((IServer)this).ReloadApplications();
			((IServer)this).ReloadConfig();
		}

		Application IServer.GetApplication(string path)
		{
			Application app = null;
			applications.TryGetValue(path, out app);
			return app;
		}

		string[] IServer.GetApplicationList()
		{
			return applications.Keys.ToArray();
		}

		long IServer.GetTotalConnectionCount()
		{
			long sum = 0;
			foreach (KeyValuePair<string, Application> kvp in applications)
			{
				sum += kvp.Value.GetConnectionCount();
			}
			return sum;
		}

		long IServer.GetApplicationConnectionCount(string application_name)
		{
			Application app = null;
			applications.TryGetValue(application_name, out app);
			if (app != null)
			{
				return app.GetConnectionCount ();
			}
			else
			{
				return 0;
			}
		}

		#endregion

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
			foreach (var pair in applications)
			{
				try
				{
					pair.Value.Start();
					logger.log("#WSS# Application `" + pair.Key + "` is started successfully.");
				}
				catch (Exception ex)
				{
					logger.error(ex.Message);
					logger.error("Couldn't start the application: `" + pair.Key + "`.");
				}
			}
			exitEvent = false;

			bool nonsecure = Config.Instance.GetValue("Server", "nonsecure_connection", false);
			if (nonsecure)
			{
				if (Config.Instance.GetValue("Server", "nonsecure_host", "localhost") == "*")
				{
					tcpListener = new TcpListener(IPAddress.Any, Config.Instance.GetValue("Server", "nonsecure_port", 8080));
				}
				else
				{
					IPHostEntry host = Dns.GetHostEntry(Config.Instance.GetValue("Server", "nonsecure_host", "localhost"));
					tcpListener = new TcpListener(host.AddressList[0], Config.Instance.GetValue("Server", "nonsecure_port", 8080));
				}
				tcpListener.Start();
				startAccept();
			}

			bool secure = Config.Instance.GetValue("Server", "secure_connection", true);
			if (secure)
			{
				if (Config.Instance.GetValue("Server", "secure_host", "localhost") == "*")
				{
					tcpSecureListener = new TcpListener(IPAddress.Any, Config.Instance.GetValue("Server", "secure_port", 8080));
				}
				else
				{
					IPHostEntry host = Dns.GetHostEntry(Config.Instance.GetValue("Server", "secure_host", "localhost"));
					tcpSecureListener = new TcpListener(host.AddressList[0], Config.Instance.GetValue("Server", "secure_port", 8081));
				}
				tcpSecureListener.Start();
				startSecureAccept();
			}

			if (Config.Instance.GetValue("Server", "PingPongClients", 1) == 1)
			{
				pingPongThread = new Thread(keepingConnectionAliveWithPingPong);
				pingPongThread.Start();
			}

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
				logger.error("Begining accepting a client failed. Error message: " + ex.Message);
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
				logger.error("Accepting a client failed. Error message: " + ex.Message);
			}
		}

		private void startSecureAccept()
		{
			try
			{
				if (!exitEvent)
				{
					tcpSecureListener.BeginAcceptTcpClient(handleAsyncSecureConnection, tcpSecureListener);
				}
			}
			catch (Exception ex)
			{
				logger.error("Begining accepting a client failed. Error message: " + ex.Message);
			}
		}

		private void handleAsyncSecureConnection(IAsyncResult res)
		{
			try
			{
				if (!exitEvent)
				{
					startSecureAccept(); //listen for new connections again
					TcpClient client = tcpSecureListener.EndAcceptTcpClient(res);
					if (client != null)
					{
						string certificate_filename = Config.Instance.GetValue("Server", "certification_filename", "certificate.cer");
						string certification_password = Config.Instance.GetValue("Server", "certification_password", "1234");
						X509Certificate2 x509 = new X509Certificate2(certificate_filename, certification_password);
						new SecureConnection(client, this, x509);
					}
				}
			}
			catch (Exception ex)
			{
				logger.error("Accepting a client failed. Error message: " + ex.Message);
			}
		}

		public override void Stop()
		{
			foreach (var pair in applications)
			{
				try
				{
					pair.Value.Stop();
					logger.log("#WSS# Application `" + pair.Key + "` is stopped successfully.");
				}
				catch (Exception ex)
				{
					logger.error(ex.Message);
					logger.error("Couldn't stop the application: `" + pair.Key + "`.");
				}
			}
			exitEvent = true;

			base.Stop();

			if (tcpListener != null)
			{
				tcpListener.Stop();
			}
			if (tcpSecureListener != null)
			{
				tcpSecureListener.Stop();
			}

			if (pingPongThread != null)
			{
				pingPongThread.Abort();
				pingPongThread = null;
			}
			lastPongs.Clear();
		}

		public override void OnData(Frame frame)
		{
		}

		public override void OnConnect(IConnection client)
		{
			lastPongs.TryAdd(client, DateTime.Now);
		}

		public override void OnDisconnect(IConnection client)
		{
			DateTime temp;
			lastPongs.TryRemove(client, out temp);
		}
		#endregion
	}
}

