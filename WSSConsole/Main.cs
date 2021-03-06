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
using WebSocketServer;
using System.Threading;

namespace WSSConsole
{
	class MainClass : ILogger
	{
		public static void Main(string[] args)
		{
			MainClass main = new MainClass();
			Server server = new Server(main);

			var echo = new EchoApplication.Echo();
			echo.SetLogger(main);
			((IServer)server).AddApplication("EchoApplication", echo);

			server.Start();
			Console.WriteLine("Press Esc to exit...");
			while (Console.ReadKey(true).Key != ConsoleKey.Escape);
			server.Stop();
		}

		#region ILogger implementation
		void ILogger.log(string msg)
		{
			Console.WriteLine("[LOG] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + msg);
		}

		void ILogger.warn(string msg)
		{
			Console.WriteLine("[WARN] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + msg);
		}

		void ILogger.error(string msg)
		{
			Console.WriteLine("[ERROR] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + msg);
		}

		void ILogger.info(string msg)
		{
			Console.WriteLine("[INFO] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + msg);
		}
		#endregion
	}
}
