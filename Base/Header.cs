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
using System.Collections.Generic;

namespace Base
{
	public class Header
	{
		private Dictionary<string, string> entries = new Dictionary<string, string>();
		private Dictionary<string, string> cookies = new Dictionary<string, string>();
		private string url;

		public string URL
		{
			get { return url; }
		}

		public Header(string url)
		{
			this.url = url;
		}

		public void Set(string name, string value)
		{
			name = name.ToLower();
			if (entries.ContainsKey(name))
			{
				entries[name] = value;
			}
			else
			{
				entries.Add(name, value);
			}
		}

		public string Get(string name)
		{
			name = name.ToLower();
			if (entries.ContainsKey(name))
			{
				return entries[name];
			}
			else
			{
				return null;
			}
		}

		public void SetCookie(string name, string value)
		{
			if (cookies.ContainsKey(name))
			{
				cookies[name] = value;
			}
			else
			{
				cookies.Add(name, value);
			}
		}

		public string GetCookie(string name)
		{
			if (cookies.ContainsKey(name))
			{
				return cookies[name];
			}
			else
			{
				return null;
			}
		}

		public override string ToString()
		{
			string buffer = url + "\r\n";

			foreach (KeyValuePair<string, string> item in entries)
			{
				buffer += item.Key + ": " + item.Value + "\r\n";
			}

			if (cookies.Count > 0)
			{
				string[] cs = new string[cookies.Count];
				int i = 0;
				foreach (KeyValuePair<string, string> kvp in cookies)
				{
					cs[i] = kvp.Key + "=" + Uri.EscapeUriString(kvp.Value);
					i++;
				}
				buffer += "Cookie: " + string.Join(";", cs) + "\r\n";
			}

			return buffer + "\r\n";
		}

		public byte[] ToBytes()
		{
			return System.Text.Encoding.UTF8.GetBytes(ToString());
		}
	}
}

