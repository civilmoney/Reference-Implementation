How to install a Civil Money Server
===========

The more servers, the stronger and more resilient the Civil Money network can become.
If you are in possession of a Windows or Ubuntu server with a permanent IP address and 
reliable high bandwidth network connection, please feel free to install an instance in order to help out. 

>We apologise that the following installation instructions are available in English only.

### Linux 

Here's how to spin up a Civil Money server on Linux.


1. Install .NET Core 2.0 or higher
Head over to [Microsoft's .NET Core website](https://www.microsoft.com/net/learn/get-started/linuxubuntu) for getting it onto your particular linux distro.

2. Download and unzip Civil Money
    ```
    $ mkdir /var/civilmoney && cd "$_"
    $ curl https://update.civil.money/api/get-repo/civilmoney_1.4.zip -o civilmoney_1.4.zip
    $ unzip civilmoney_1.4.zip
    ```
3. Do a test run to make sure everything works
    ```
    $ cd /var/civilmoney && dotnet CM.Daemon.dll
    ```
    If all's gone well it should download an update and join the distributed hash table. Press Ctrl + C to stop it.

    **Please always check** the log output after 1 minute to make sure you have a Predecessor (which implies that inbound connections are working through your NAT.)

    > **HINT:** You should be able to visit https://127-0-0-1.untrusted-server.com:8000 and see an instance of the Civil Money website running off of your server. Replace "127-0-0-1" with the external IP address integers of your server when testing for external connectivity.


4. To install as a service under systemd under a low privelege user
    ```
    $ sudo useradd --home /var/civilmoney --gid nogroup -m --shell /bin/false civilmoney
    $ sudo chown -R civilmoney:nogroup /var/civilmoney
    $ sudo dotnet CM.Daemon.dll --install user:civilmoney
    $ sudo systemctl enable civilmoney.service
    $ sudo systemctl start civilmoney
    ```
    To view status/logs
    ```
    $ sudo systemctl status civilmoney
    $ journalctl -fu civilmoney 
    ```

### Windows
Windows setup is pretty straight forward.

1. Install the [.NET Core SDK 2.0](https://www.microsoft.com/net/download/windows) or higher.
2. Download the [Civil Money binary](https://update.civil.money/api/get-repo/civilmoney_1.4.zip) and unzip the contents into a folder location on your server.
3. Open an elevated command prompt:
```
> cd <your unzipped folder location>
```
If you prefer to NOT run the server as a Windows Service just yet, or want to try it out temporarily to make sure things are configured OK, you can simply run:
```
> dotnet CM.Daemon.dll
```
If you're ready to install Civil Money as a permanent Windows Service, simply run:
```
> dotnet CM.Daemon.dll --install
```
To uninstall the Windows Service you can run:
```
> dotnet CM.Daemon.dll --uninstall
```
4. Check the log. When running as a background Windows Service you'll want to check on the log to make sure that everything is OK. A rolling log window is written to the `log.txt` file next to the CM.Daemon.dll. This file contains the application's log output in "newest first" order for convenience.

**Please always check** the log output after 1 minute to make sure you have a Predecessor (which implies that inbound connections are working through your NAT.) 

> **HINT:** You should be able to visit https://127-0-0-1.untrusted-server.com:8000 and see an instance of the Civil Money website running off of your server. Replace "127-0-0-1" with the external IP address integers of your server when testing for external connectivity.


### Customising the configuration

All settings are in the `settings.json` file which is side-by-side the `CM.Daemon.dll`. On Linux, if installed through apt, this will be under `/var/civilmoney`.

The default settings look like this:
```
{
  "Settings": {
    "Port": 8000,
	"IP": "0.0.0.0",
    "Seeds": "seed1.civil.money,seed2.civil.money",
    "DataFolder": "cm-data",
    "AuthoritativePfxCertificate": "",
    "AuthoritativePfxPassword": "",
    "EnableAuthoritativeDomainFeatures": false,
    "EnablePort80Redirect": false
  }
}

```
| Field | Description     |
|-------|---------|
| Port | If another web server (Apache, IIS, etc) is not installed and already using port 443, you can do `sudo setcap cap_net_bind_service=ep /usr/share/dotnet/dotnet` to allow .NET Core to bind the port on Linux or for Windows simply simply set the service to run under `LocalSystem`, and then change the port in the configuration to 443. You will receive start-up errors if the server is unable to bind. |
| IP | Bind to a specific IP. Or 0.0.0.0 for any/all. |
| Seeds | A comma delimited list of known well behaved peers. To eliminate a single point of failure, the Civil Money community may establish seed listings online which are based on IPs. |
| DataFolder | A path to a suitable folder for data storage. By default data is kept in a folder beside the `CM.Daemon.dll`. |
| AuthoritativePfxCertificate | Only used by official Civil Money seeds. |
| AuthoritativePfxPassword | Only used by official Civil Money seeds. |
| EnableAuthoritativeDomainFeatures | Only used by official Civil Money seeds. Enabling this will not have any impact on the network, however your server will do some periodic unnecessary extra work and also attempt to act as an (unused) DNS server for `*.untrusted-domain.com`. |
| EnablePort80Redirect | Only used by official Civil Money seeds. When enabled the server will attempt to bind the regular HTTP port 80 and redirect all requests over to 443 for SSL. |

