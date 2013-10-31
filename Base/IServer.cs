using System;

namespace Base
{
	public interface IServer
	{
		void AddApplication(string name, Application app);
		void ReloadApplications();
		void ReloadConfig();
		void Reload();
		Application GetApplication(string name);
		string[] GetApplicationList();
		long GetTotalConnectionCount();
		long GetApplicationConnectionCount(string application_name);
	}
}
