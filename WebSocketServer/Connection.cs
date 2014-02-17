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
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Protocol;
using System.Threading.Tasks;

namespace WebSocketServer
{
	public class Connection : IConnection
	{
		protected TcpClient socket;
		protected NetworkStream socketStream;
		protected bool connected = false;
		protected Server server;
		protected Application application;
		protected Header header;
		protected Draft draft;
		protected List<byte> buffer = new List<byte>();
		protected byte[] message = new byte[4096];
		protected object extra = null;

		public Connection(TcpClient socket, Server server)
		{
			this.socket = socket;
			this.server = server;
			socketStream = socket.GetStream();
				
			startRead();
		}

		protected virtual void startRead()
		{
			socketStream.BeginRead(message, 0, message.Length, handleAsyncRead, socketStream);
		}

		protected void handleAsyncRead(IAsyncResult res)
		{
			try
			{
				if (socket.Connected)
				{
					int bytesRead = socketStream.EndRead(res);
					if (bytesRead > 0)
					{
						byte[] temp = new byte[bytesRead];
						Array.Copy(message, temp, bytesRead);
						startRead(); //listen for new connections again
						digestIncomingMessage(temp);
						return;
					}
				}
				((IConnection)this).Close();
			}
			catch (Exception ex)
			{
				((ILogger)server).error("Reading from client failed. Removing the client from list. Error message: " + ex.Message);

				if (application != null)
				{
					application.RemoveConnection(this);
				}
				if (application != server)
				{
					server.RemoveConnection(this);
				}
			}
		}

		private bool digestIncomingMessage(byte[] _buffer)
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
				if (draft == null)
				{
					foreach (Draft d in server.Drafts)
					{
						try
						{
							header = d.ParseClientRequestHandshake(buffer);
							draft = d;
							
							if (header.URL == "/")
							{
								application = server;
							}
							else
							{
								//Extracting application's name
								Regex regex = new Regex("/?([^/\\?\\*\\+\\\\]+)[/\\?]?.*");
								Match mtch = regex.Match(header.URL);
								string appName = mtch.Groups[1].Value;

								application = ((IServer)server).GetApplication(appName);
								if (application == null)
								{
									((ILogger)server).error("Invalid application: " + appName + ", URL: " + header.URL);
									sendHttpResponse(404);
									((IConnection)this).Close();
									return false;
								}
							}
							
							byte[] b = d.CreateServerResponseHandshake(header);
							socketStream.Write(b, 0, b.Length);
							socketStream.Flush();
#if LOGGER
							((ILogger)server).log("Handshake was sent to:" + ((IConnection)this).IP.ToString());
#endif
							connected = true;
							application.AddConnection(this);
							server.AddConnection(this);

							break;
						}
						catch
						{
						}
					}
				}
				if (draft != null)
				{
					Frame f;
					while ((f = draft.ParseClientFrameBytes(buffer)) != null)
					{
                        f.Connection = this;
                        if (f.OpCode == Frame.OpCodeType.Close)
                        {
                            ((IConnection)this).Close();
                            break;
                        }
                        else if (f.OpCode == Frame.OpCodeType.Ping)
                        {
                            server.Ping(f);
                        }
                        else if (f.OpCode == Frame.OpCodeType.Pong)
                        {
                            server.Pong(f);
                        }
                        if (application != null)
                        {
                            application.EnqueueIncomingFrame(f);
                        }
					}
				}
			}
			catch
			{
			}
			
			return true;
		}

		public void sendHttpResponse(int httpStatusCode = 400)
		{
			string httpHeader = "HTTP/1.1 ";
			switch (httpStatusCode)
			{
				case 400:
					httpHeader += "400 Bad Request";
				break;
			
				case 401:
					httpHeader += "401 Unauthorized";
				break;
			
				case 403:
					httpHeader += "403 Forbidden";
				break;
			
				case 404:
					httpHeader += "404 Not Found";
				break;
			
				case 501:
					httpHeader += "501 Not Implemented";
				break;
			}
			httpHeader += "\r\n";
			
			byte[] b = System.Text.Encoding.UTF8.GetBytes(httpHeader);
			socketStream.Write(b, 0, b.Length);
			socketStream.Flush();
		}

		#region IConnection implementation
		void IConnection.Send(Frame frame)
		{
			try
			{
				byte[] b = draft.CreateServerFrameBytes(frame);
				if (b != null)
				{
					socketStream.BeginWrite(b, 0, b.Length, null, null);
				}
			}
			catch (Exception ex)
			{
				((ILogger)server).error("Writing to client failed. Removing the client from list. Error message: " + ex.Message);

				if (application != null)
				{
					application.RemoveConnection(this);
				}
				if (application != server)
				{
					server.RemoveConnection(this);
				}
			}
		}

		string IConnection.IP
		{
			get
			{
				return ((IPEndPoint)(socket.Client.RemoteEndPoint)).Address.ToString();
			}
		}

		int IConnection.Port
		{
			get
			{
				return ((IPEndPoint)(socket.Client.RemoteEndPoint)).Port;
			}
		}

		bool IConnection.Connected
		{
			get
			{
				return connected && socket.Connected;
			}
		}
		
		bool IConnection.IsABridge
		{
			get
			{
				return false;
			}
		}
		
		Application IConnection.Application
		{
			get { return application; }
		}

		IServer IConnection.Server
		{
			get { return server; }
		}

		void IConnection.Close()
		{
			if (connected)
			{
				connected = false;
#if LOGGER
				((ILogger)server).log("Connection is closed, "  + ((IConnection)this).IP.ToString());
#endif
				socket.Close();
				if (application != null)
				{
					application.RemoveConnection(this);
				}
				if (application != server)
				{
					server.RemoveConnection(this);
				}
			}
		}
		
		object IConnection.Extra
		{
			get { return extra; }
			set { extra = value; }
		}
		#endregion
	}
}
