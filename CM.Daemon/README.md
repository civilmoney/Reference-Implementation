About the CM.Daemon project
===========

The Civil Money Daemon is responsible for pulling in updates, checking binaries signatures, and just generally keeping the main CM.Server dotnet process up and running.

> **HINT:** You do not have to use the daemon at all for development/debugging purposes. Just run the CM.Server project in Visual Studio, or `dotnet CM.Server.dll` directly.

It expects that there is a settings.json file next to the CM.Daemon.dll.

