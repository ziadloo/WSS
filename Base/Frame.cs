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

namespace Base
{
	public class Frame
	{
		public enum OpCodeType
		{
			Continue = 0x0,
			Text = 0x1,
			Binary = 0x2,
			Close = 0x8,
			Ping = 0x9,
			Pong = 0xA
		}
		
		private string message;
		private byte[] data;
		private OpCodeType opCode;
		private IConnection connection;
		
		public string Message
		{
			get { return message; }
		}
		
		public byte[] Data
		{
			get { return data; }
		}
		
		public OpCodeType OpCode
		{
			get { return opCode; }
		}
		
		public IConnection Connection
		{
			get { return connection; }
			set { connection = value; }
		}
		
		public Frame(string msg)
		{
			message = msg;
			opCode = OpCodeType.Text;
		}
		
		public Frame(byte[] d)
		{
			data = d;
			opCode = OpCodeType.Binary;
		}
		
		public Frame(OpCodeType op, byte[] d = null)
		{
			data = d;
			opCode = op;
		}
	}
}
