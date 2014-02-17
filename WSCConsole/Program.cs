using System;
using WebSocketClient;

namespace WSCConsole
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			WSSocket client = new WSSocket("ws://192.168.0.81:8181/EchoApplication", false);

			client.OnOpen += delegate() {
				Console.WriteLine ("Connection Opened");
			};
			client.OnClose += delegate() {
				Console.WriteLine ("Connection Closed");
			};
			client.OnMessage += delegate(string message) {
				Console.WriteLine("Received: " + message);
			};

			client.Open();

			Console.WriteLine("Press Esc to exit...");

			ConsoleKeyInfo key;
			string msg = "";
			do
			{
				key = Console.ReadKey();
				if (key.Key == ConsoleKey.Escape)
				{
					break;
				}
				else if (key.Key == ConsoleKey.Enter)
				{
					client.Send(msg);
					msg = "";
				}
				else
				{
					msg += key.KeyChar;
				}
			}
			while (true);
		}
	}
}
