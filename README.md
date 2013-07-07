WSS
===

WSS: A WebSocket Server written in C# and .Net (Mono)

This project implementes a WebSocket server with C# and .Net. Personaly I have tested it with Mono 2.10.8.1 in Ubuntu 12.04
but I believe it should work in other OSs as well. The reason why I decided to write my own WebSocket server instead of
using other open source projects (like [Alchemy](https://github.com/Olivine-Labs/Alchemy-Websockets-Client-Library) or
[Fleck](https://github.com/statianzo/Fleck)) was that they are too complicated. In contrast I tried to implement this
project as simple as possible. WSS's design is inspired by the design of project
[php-websocket](https://github.com/nicokaiser/php-websocket) which means it lets you develop your own applications and
introduce them to the server without the need of recompiling it, thanks to reflection. Right now it supports two versions
of the WebSocket protocol including hybi-10 and byhi-17 (RFC 6455). Some of the code to encode and decode the protocol has
been borrowed from the mentioned projects and also [Java-WebSocket](https://github.com/TooTallNate/Java-WebSocket).

How to use:
-----------

If you want to test the functionality, I've made a project named "Console" which you can run. Using this project
you can run the server as a Console application. Pressing the Esc will terminate the server.

In production, you should run the server as a Daemon (in Linux) or a Service (in Windows). Please refer to the appropriate
section in this file in order to learn how to do this.

Installing the project as a Daemon in Lunix:
--------------------------------------------

	0. For these steps to work you will need "mono-service", so install that first.

	1. Copy the release files to some location. Don't forget the "Applications" folder and its files. e.g. /usr/bin or /usr/sbin
	
	2. Copy the "wssd" script file to /etc/init.d. You can find it in the project's root. You also need to amend some paths in it.
	
	3. In a shell type: sudo /etc/init.d/wssd start

Installing the project as a Service in Windows:
-----------------------------------------------

	1. Copy the release files to some folder, perhaps c:\Program Files\WSS.
	
	2. Run a cmd.exe from start menu. For the next step you need to run the command as an administrator, so right click on cmd and click "Run as administrator".
	
	3. In a cmd type: installutil c:\Program Files\WSS\WSSDaemon.exe. You might need to find the path to your installutil first!

Things to be done:
------------------

	1. I haven't found the time to read the configurations in a file. As a result the server listens to 8080 port unless you change it manually.

	2. The secure protocol wss:// is not supported yet.

