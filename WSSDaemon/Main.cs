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
using System.ServiceProcess;
using System.Diagnostics;
using Base;
using WebSocketServer;

namespace WSSDaemon
{
	class MainClass : ServiceBase, ILogger
	{
		private System.Diagnostics.EventLog eventLog;
		private Server server;
		
		public static void Main (string[] args)
		{
			Console.WriteLine ("Hello World!");
		}
		
		public MainClass()
		{
			this.ServiceName = "MehranService";
			this.eventLog = new EventLog();
			this.eventLog.Source = this.ServiceName;
			this.eventLog.Log = "Application";

			((System.ComponentModel.ISupportInitialize)(this.eventLog)).BeginInit();
			if (!EventLog.SourceExists(this.eventLog.Source))
			{
				EventLog.CreateEventSource(this.eventLog.Source, this.eventLog.Log);
			}
			((System.ComponentModel.ISupportInitialize)(this.eventLog)).EndInit();
			
			server = new Server(this);
		}
		
		protected override void OnStart(string[] args)
		{
			base.OnStart(args);
			
			//TODO: place your start code here
			eventLog.WriteEntry("Starting MehranService...", EventLogEntryType.Information);
			
			server.Start();
		}
		
		protected override void OnStop()
		{
			base.OnStop();
			
			//TODO: clean up any variables and stop any threads
			eventLog.WriteEntry("Stopping MehranService...", System.Diagnostics.EventLogEntryType.Information);
			
			server.Stop();
		}

		#region ILogger implementation
		void ILogger.log(string msg)
		{
			eventLog.WriteEntry(msg, EventLogEntryType.Information);
		}

		void ILogger.warn(string msg)
		{
			eventLog.WriteEntry(msg, EventLogEntryType.Warning);
		}

		void ILogger.error(string msg)
		{
			eventLog.WriteEntry(msg, EventLogEntryType.Error);
		}

		void ILogger.info(string msg)
		{
			eventLog.WriteEntry(msg, EventLogEntryType.Information);
		}
		#endregion
	}
}
