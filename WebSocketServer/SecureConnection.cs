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
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using Base;

namespace WebSocketServer
{
	public class SecureConnection : Connection
	{
		private SslStream sslStream;
		private X509Certificate certificate;
		private bool constructed = false;
		private bool authenticated = false;

		public SecureConnection(TcpClient socket, Server server, X509Certificate certificate)
			: base(socket, server)
		{
			sslStream = new SslStream(socket.GetStream(), false);
			this.certificate = certificate;
			constructed = true;

			startRead();
		}

		protected override void startRead()
		{
			if (constructed)
			{
				if (!authenticated)
				{
					bool clientCertificateRequired = false;
					SslProtocols ssls = digestSslProtocols("ssl2 ssl3");
					bool checkCertificateRevocation = true;
					sslStream.BeginAuthenticateAsServer(
						certificate
						, clientCertificateRequired
						, ssls
						, checkCertificateRevocation
						, handleAsyncAuthenticate
						, sslStream
					);
				}
				else
				{
					sslStream.BeginRead(message, 0, message.Length, handleAsyncRead, sslStream);
				}
			}
		}

		private void handleAsyncAuthenticate(IAsyncResult res)
		{
			try
			{
				sslStream.EndAuthenticateAsServer(res);
				authenticated = true;
				startRead();
			}
			catch (Exception ex)
			{
				if (sslStream != null)
				{
					sslStream.Dispose();
					sslStream = null;
					((IConnection)this).Close();
				}

				((ILogger)server).error("Authenticating the client failed. Error message: " + ex.Message);
			}
		}

		private SslProtocols digestSslProtocols(string str)
		{
			SslProtocols ssls = SslProtocols.None;
			str = str.ToLower();

			if (str.Contains("ssl2"))
			{
				ssls |= SslProtocols.Ssl2;
			}

			if (str.Contains("ssl3"))
			{
				ssls |= SslProtocols.Ssl3;
			}

			if (str.Contains("tls"))
			{
				ssls |= SslProtocols.Tls;
			}

			return ssls;
		}
	}
}

