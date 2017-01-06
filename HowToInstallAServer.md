How to install a Civil Money Server
===========

The more servers, the stronger and more resilient the Civil Money network can become.
If you are in possession of a Windows or Ubuntu server with a permanent IP address and 
reliable high bandwidth network connection, please feel free to install an instance in order to help out. 

>We apologise that the following installation instructions are available in English only.

### Ubuntu Linux 

We have an apt repository to make setup fairly simple, however each Ubuntu version has a slightly different
.NET Core repository list.

```
# 1. We need HTTPS and the appropriate .NET Core list from Microsoft.
$ sudo apt-get install apt-transport-https
```
#### 16.4 LTS
```
$ sudo sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ xenial main" > /etc/apt/sources.list.d/dotnetdev.list'
```

#### 16.10 LTS
```
$ sudo sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ yakkety main" > /etc/apt/sources.list.d/dotnetdev.list'
```
*For other/future versions, check out [https://www.microsoft.com/net/core](https://www.microsoft.com/net/core) for installation instructions.*
```
# 2. Add .NET Core keys from Microsoft. 
$ sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893

# 3. Configure Civil Money list and keys
$ sudo echo "deb [arch=amd64] https://update.civil.money/api/get-repo/ stable main" | sudo tee -a /etc/apt/sources.list
$ sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 9DAB6D5065655BBC96ADA0855D7FBDB485BE2421

# 4. Update and install
$ sudo apt-get update
$ sudo apt-get install civilmoney

# 5. Start the supervisor service
$ sudo systemctl restart supervisor

# 6. Make sure the server is running OK
$ sudo tail -f /var/log/civilmoney.out.log
```

**Please always check** the log output after 1 minute to make sure you have a Predecessor (which implies that inbound connections are working through your NAT.)

> **HINT:** You should be able to visit https://127-0-0-1.untrusted-server.com:8000 and see an instance of the Civil Money website running off of your server. Replace "127-0-0-1" with the external IP address integers of your server when testing for external connectivity.


#### Other Linux/Unix Distros/Mac OSX
If you're a savvy unix administrator or running something like a Mac server, you can follow Microsoft's [.NET Core](https://www.microsoft.com/net/core) setup instructions and download/extract the standard [Civil Money binary](https://update.civil.money/api/get-repo/civilmoney_1.2.zip) and run `dotnet CM.Daemon.dll` directly.

### Windows
Windows setup is pretty straight forward, but you need to install the .NET Core 1.1 prerequisite.

1. Install the [.NET Core SDK 1.1](https://go.microsoft.com/fwlink/?LinkID=835014)
2. Download the [Civil Money binary](https://update.civil.money/api/get-repo/civilmoney_1.2.zip) and unzip the contents into a folder location on your server.
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
    "Seeds": "seed1.civil.money,seed2.civil.money,seed3.civil.money,seed4.civil.money",
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
| Port | If another web server (Apache, IIS, etc) is not installed and already using port 443, you may run the service under `root` on Linux or `LocalSystem` on Windows in order to bind 443, and then change the port in the configuration to 443. For Linux you need to edit `/etc/supervisor/conf.d/civilmoney.conf`, change its user string to `root` and then `systemctl restart supervisor`. For Windows, assuming IIS has not bound the port already, simply changing the port to 443 should be all you need. You will receive start-up errors if the server is unable to bind. |
| Seeds | A comma delimited list of known well behaved peers. To eliminate a single point of failure, the Civil Money community may establish seed listings online which are based on IPs. |
| DataFolder | A path to a suitable folder for data storage. By default data is kept in a folder beside the `CM.Daemon.dll`. |
| AuthoritativePfxCertificate | Only used by official Civil Money seeds. |
| AuthoritativePfxPassword | Only used by official Civil Money seeds. |
| EnableAuthoritativeDomainFeatures | Only used by official Civil Money seeds. Enabling this will not have any impact on the network, however your server will do some periodic unnecessary extra work and also attempt to act as an (unused) DNS server for `*.untrusted-domain.com`. |
| EnablePort80Redirect | Only used by official Civil Money seeds. When enabled the server will attempt to bind the regular HTTP port 80 and redirect all requests over to 443 for SSL. |

