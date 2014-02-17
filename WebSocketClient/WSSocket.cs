using System;
using System.Net;
using System.Text.RegularExpressions;
using Protocol;
using System.Net.Sockets;
using Base;
using System.Collections.Generic;

namespace WebSocketClient
{
	public class WSSocket
	{
		private bool connected = false;
		public bool Connected
		{
			get { return connected; }
		}

		public delegate void OnOpenHandler();
		private event OnOpenHandler _onOpen;
		public event OnOpenHandler OnOpen {
			add {
				_onOpen += value;
				if (connected)
				{
					value();
				}
			}
			remove {
				_onOpen -= value;
			}
		}
		public void ResetOnOpen() { _onOpen = null; }

		public delegate void OnCloseHandler();
		public event OnCloseHandler OnClose;
		public void ResetOnClose() { OnClose = null; }

		public delegate void OnMessageHandler(string message);
		public event OnMessageHandler OnMessage;
		public void ResetOnMessage() { OnMessage = null; }

		private string url;
		private string protocol = "ws";
		private string server = "192.168.0.10";
		private int port = 8080;
		private string path = "/EchoApplication";
		private System.Net.Sockets.Socket socket;
		protected List<byte> buffer = new List<byte>();
		protected byte[] message = new byte[4096];
		protected Draft draft = new Draft17();
		protected bool handshaked = false;
		protected Header header;
		protected string expectedAccept;

		public WSSocket(string url, bool autoconnect = true)
		{
			this.url = url;

			Regex regex = new Regex(@"^([^:]+)://([^/:]+)(:(\d+))?(/.*)?$");
			Match mtch = regex.Match(url.Trim());
			if (!mtch.Success)
			{
				throw new Exception ("Invalid URL: " + url);
			}

			protocol = mtch.Groups[1].Value;
			server = mtch.Groups[2].Value;
			if (String.IsNullOrEmpty(mtch.Groups[4].Value))
			{
				port = 8080;
			}
			else
			{
				port = Convert.ToInt32(mtch.Groups[4].Value);
			}
			path = mtch.Groups[5].Value;

			if (autoconnect)
			{
				IPHostEntry ipHostInfo = Dns.GetHostEntry(server);
				IPAddress ipAddress = ipHostInfo.AddressList[0];
				IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

				socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				socket.Connect(remoteEP);
				
				startReceive();
				sendHandShake();
			}
		}
		
		public void Send(string message)
		{
			if (handshaked)
			{
				Frame f = new Frame(message);
				byte[] b = draft.CreateClientFrameBytes(f);
				socket.BeginSend(b, 0, b.Length, SocketFlags.None, null, socket);
			}
		}

		protected virtual void sendHandShake()
		{
			byte[] hs = draft.CreateClientRequestHandshake(url, out expectedAccept);
			socket.BeginSend(hs, 0, hs.Length, SocketFlags.None, null, socket);
		}

		protected virtual void startReceive()
		{
			socket.BeginReceive(message, 0, message.Length, SocketFlags.None, handleAsyncReceive, socket);
		}

		protected void handleAsyncReceive(IAsyncResult res)
		{
			try
			{
				if (socket != null && socket.Connected)
				{
					int bytesReceived = socket.EndReceive(res);
					if (bytesReceived > 0)
					{
						byte[] temp = new byte[bytesReceived];
						Array.Copy(message, temp, bytesReceived);
						startReceive(); //listen for new connections again
						digestIncomingBuffer(temp);
						return;
					}
				}
			}
			catch (Exception)
			{
				//((ILogger)server).error("Receiving from server failed. Error message: " + ex.Message);
			}
			handshaked = false;
			if (OnClose != null)
			{
				OnClose();
			}
		}

		private bool digestIncomingBuffer(byte[] _buffer)
		{
			if (_buffer.Length == 0)
			{
				//the client has disconnected from the server
				return false;
			}
			
			//message has successfully been received
			try
			{
				for (int i=0; i<_buffer.Length; i++)
				{
					buffer.Add(_buffer[i]);
				}
				if (!handshaked)
				{
					try
					{
						header = draft.ParseServerResponseHandshake(buffer);
						if (header.Get("sec-websocket-accept") != expectedAccept)
						{
							//Error
						}
						handshaked = true;
						if (_onOpen != null)
						{
							_onOpen();
						}
					}
					catch
					{
					}
				}
				if (handshaked)
				{
					Frame f;
					while ((f = draft.ParseServerFrameBytes(buffer)) != null)
					{
						if (f.OpCode == Frame.OpCodeType.Close)
						{
							break;
						}
						else if (f.OpCode == Frame.OpCodeType.Ping)
						{
							if (handshaked)
							{
								Frame res = new Frame(Frame.OpCodeType.Pong);
								byte[] b = draft.CreateClientFrameBytes(res);
								socket.BeginSend(b, 0, b.Length, SocketFlags.None, null, socket);
							}
						}
						else if (f.OpCode == Frame.OpCodeType.Pong)
						{

						}
						else if (f.OpCode == Frame.OpCodeType.Text)
						{
							if (OnMessage != null)
							{
								OnMessage(f.Message);
							}
						}
					}
				}
			}
			catch
			{
			}
			
			return true;
		}

		public void Open()
		{
			if (!connected)
			{
				IPHostEntry ipHostInfo = Dns.GetHostEntry(server);
				IPAddress ipAddress = ipHostInfo.AddressList[0];
				IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

				socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				socket.Connect(remoteEP);

				startReceive();
				sendHandShake();
			}
		}
	
		public void Close()
		{
			try
			{
				connected = false;
				socket.Close();
				socket = null;
			}
			catch {}
		}
	}
}

