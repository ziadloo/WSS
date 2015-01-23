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
using System.Configuration.Install;
using System.ComponentModel;
using System.ServiceProcess;

namespace WSSDaemon
{
	[RunInstaller(true)]
	public class MSWindowsServiceInstaller : Installer
	{
		public MSWindowsServiceInstaller ()
		{
			ServiceProcessInstaller processInstaller = new ServiceProcessInstaller();
			System.ServiceProcess.ServiceInstaller serviceInstaller = new System.ServiceProcess.ServiceInstaller();
			
			//set the privileges
			processInstaller.Account = ServiceAccount.LocalSystem;
			
			serviceInstaller.DisplayName = "WSS";
			serviceInstaller.StartType = ServiceStartMode.Automatic;
			
			//must be the same as what was set in Program's constructor
			serviceInstaller.ServiceName = "WSS";
			
			this.Installers.Add(processInstaller);
			this.Installers.Add(serviceInstaller);
		}
	}
}

