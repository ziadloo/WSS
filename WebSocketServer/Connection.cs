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
using WebSocketServer.Drafts;
using System.Threading.Tasks;

namespace WebSocketServer
{
	public class Connection : IConnection
	{
		private TcpClient socket;
		private NetworkStream socketStream;
		private bool connected = false;
		private Server server;
		private Application application;
		private Header header;
		private Draft draft;
		private List<byte> buffer = new List<byte>();
		
		private byte[] message = new byte[4096];
		
		public Connection(TcpClient socket, Server server)
		{
			this.socket = socket;
			this.server = server;
			socketStream = socket.GetStream();
				
			startRead();
		}
		
		private void startRead()
		{
			socketStream.BeginRead(message, 0, message.Length, handleAsyncRead, socketStream);
		}
		
		private void handleAsyncRead(IAsyncResult res)
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
		
		private bool digestIncomingMessage(byte[] _buffer)
		{
			if (_buffer.Length == 0) {
				//the client has disconnected from the server
				return false;
			}
			
			//message has successfully been received
			try {
				for (int i=0; i<_buffer.Length; i++) {
					buffer.Add(_buffer[i]);
				}
				if (draft == null) {
					foreach (Draft d in server.Drafts) {
						try {
							header = d.ParseHandshake(buffer);
							draft = d;
							
							if (header.URL == "/") {
								application = server;
							}
							else {
								string appName = header.URL.Substring(1);
								application = server.getApplication(appName);
								if (application == null) {
									((ILogger)server).log("Invalid application: " + header.URL);
									sendHttpResponse(404);
									((IConnection)this).Close();
								}
							}
							
							byte[] b = d.CreateResponseHandshake(header);
							socketStream.Write(b, 0, b.Length);
							socketStream.Flush();
							((ILogger)server).log("Handshake sent");
                            connected = true;
							application.AddConnection(this);
							server.AddConnection(this);
						}
						catch {
						}
					}
				}
				if (draft != null) {
					Frame f;
					while ((f = draft.ParseFrameBytes(buffer)) != null) {
						if (f.OpCode == Frame.OpCodeType.Close) {
							((IConnection)this).Close();
							break;
						}
						if (application != null) {
                            f.Connection = this;
							application.EnqueueIncomingFrame(f);
						}
					}
				}
			}
			catch {
			}
			
			return true;
		}
		
		public void sendHttpResponse(int httpStatusCode = 400)
		{
			string httpHeader = "HTTP/1.1 ";
			switch (httpStatusCode) {
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
			byte[] b = draft.CreateFrameBytes(frame);
			if (b != null) {
				socketStream.BeginWrite(b, 0, b.Length, null, null);
			}
		}

		string IConnection.IP {
			get {
				return ((IPEndPoint)(socket.Client.RemoteEndPoint)).Address.ToString();
			}
		}

		int IConnection.Port {
			get {
				return ((IPEndPoint)(socket.Client.RemoteEndPoint)).Port;
			}
		}

		bool IConnection.Connected {
			get {
				return connected && socket.Connected;
			}
		}
		
		void IConnection.Close()
		{
            if (connected)
            {
				connected = false;
				((ILogger)server).log("Connection is closed");
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
		#endregion
	}
	
	class ReadBuffer
	{
		public int BytesRead = 0;
		public byte[] Buffer = new byte[4096];
	}
}
