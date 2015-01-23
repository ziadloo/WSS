using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STA.Settings;
using System.IO;
using System.Reflection;

namespace Base
{
	public class Config : INIFile
	{
		static private Config singleton;
		static public Config Instance
		{
			get { return singleton; }
		}

		static Config()
		{
			if (FileContent == null)
			{
				singleton = new Config(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),  ConfigFileName), false);
			}
			else
			{
				singleton = new Config(FileContent);
			}
		}

		public static string ConfigFileName = "wss.ini";
		public static string FileContent = null;

		protected Config(string filename, bool lazy)
			: base(filename, lazy)
		{
		}

		protected Config(string content)
			: base(content)
		{
		}

		public static void Reload()
		{
			if (singleton != null)
			{
				if (FileContent != null)
				{
					singleton.Content = FileContent;
				}
				singleton.Refresh();
			}
		}
	}
}
