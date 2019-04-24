#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// The help page covers basic contact info as well as instructions for contributing or setting
    /// up a server.
    /// </summary>
    internal class HelpPage : Page {

        public override string Title {
            get {
                return SR.TITLE_HELP;
            }
        }

        public override string Url {
            get {
                return "/help";
            }
        }

        public override void Build() {
            Element.ClassName = "helppage";
            Element.H1(SR.TITLE_HELP);
            Element.Div("para", SR.LABEL_HELP_INTRO);
            Element.Div("para").Button("english@civil.money", (e) => {
                Window.Open("mailto:english@civil.money");
            });

            Element.Div("para regret", SR.LABEL_HELP_IN_ENGLISH_ONLY);

            Element.H1(SR.TITLE_SOURCE_CODE);
            Element.Div("para", SR.LABEL_SOURCE_CODE_INTRO);
            var buttons = Element.Div("");
            buttons.Button("Read the API", "/api");
            buttons.Span("&nbsp;");
            buttons.Button("Open on GitHub", (e) => {
                Window.Open("https://github.com/civilmoney/Reference-Implementation");
            });

            //Element.H1(SR.TITLE_ABOUT);
            //Element.Div("para", SR.HTML_ABOUT);

            Element.H1(SR.TITLE_GET_INVOLVED);
            Element.Div("para", SR.LABEL_GET_INVOLVED_INTRO);
            Element.Div("para").Button("hello@civil.money", (e) => {
                Window.Open("mailto:hello@civil.money");
            });

            Element.H1(SR.TITLE_INSTALL_A_SERVER);
            // To generate this HTML we're using:
            // http://dillinger.io/
            Element.Div("markdown", @"
<p>The more servers, the stronger and more resilient the Civil Money network can become.<br>
If you are in possession of a Windows or Ubuntu server with a permanent IP address and<br>
reliable high bandwidth network connection, please feel free to install an instance in order to help out.</p>
<blockquote>
<p>We apologise that the following installation instructions are available in English only.</p>
</blockquote>
<h3><a id=""Linux_9""></a>Linux</h3>
<p>Here’s how to spin up a Civil Money server on Linux.</p>
<ol>
<li>
<p>Install .NET Core 2.0 or higher<br>
Head over to <a href=""https://www.microsoft.com/net/learn/get-started/linuxubuntu"">Microsoft’s .NET Core website</a> for getting it onto your particular linux distro.</p>
</li>
<li>
<p>Download and unzip Civil Money</p>
<pre><code>$ mkdir /var/civilmoney &amp;&amp; cd &quot;$_&quot;
$ curl https://update.civil.money/api/get-repo/civilmoney_1.4.zip -o civilmoney_1.4.zip
$ unzip civilmoney_1.3.zip
</code></pre>
</li>
<li>
<p>Do a test run to make sure everything works</p>
<pre><code>$ cd /var/civilmoney &amp;&amp; dotnet CM.Daemon.dll
</code></pre>
<p>If all’s gone well it should download an update and join the distributed hash table. Press Ctrl + C to stop it.</p>
<p><strong>Please always check</strong> the log output after 1 minute to make sure you have a Predecessor (which implies that inbound connections are working through your NAT.)</p>
<blockquote>
<p><strong>HINT:</strong> You should be able to visit <a href=""https://127-0-0-1.untrusted-server.com:8000"">https://127-0-0-1.untrusted-server.com:8000</a> and see an instance of the Civil Money website running off of your server. Replace “127-0-0-1” with the external IP address integers of your server when testing for external connectivity.</p>
</blockquote>
</li>
</ol>
<ol start=""4"">
<li>To install as a service under systemd under a low privelege user<pre><code>$ sudo useradd --home /var/civilmoney --gid nogroup -m --shell /bin/false civilmoney
$ sudo chown -R civilmoney:nogroup /var/civilmoney
$ sudo dotnet CM.Daemon.dll --install user:civilmoney
$ sudo systemctl enable civilmoney.service
$ sudo systemctl start civilmoney
</code></pre>
To view status/logs<pre><code>$ sudo systemctl status civilmoney
$ journalctl -fu civilmoney 
</code></pre>
</li>
</ol>
<h3><a id=""Windows_48""></a>Windows</h3>
<p>Windows setup is pretty straight forward.</p>
<ol>
<li>Install the <a href=""https://www.microsoft.com/net/download/windows"">.NET Core SDK 2.0</a> or higher.</li>
<li>Download the <a href=""https://update.civil.money/api/get-repo/civilmoney_1.4.zip"">Civil Money binary</a> and unzip the contents into a folder location on your server.</li>
<li>Open an elevated command prompt:</li>
</ol>
<pre><code>&gt; cd &lt;your unzipped folder location&gt;
</code></pre>
<p>If you prefer to NOT run the server as a Windows Service just yet, or want to try it out temporarily to make sure things are configured OK, you can simply run:</p>
<pre><code>&gt; dotnet CM.Daemon.dll
</code></pre>
<p>If you’re ready to install Civil Money as a permanent Windows Service, simply run:</p>
<pre><code>&gt; dotnet CM.Daemon.dll --install
</code></pre>
<p>To uninstall the Windows Service you can run:</p>
<pre><code>&gt; dotnet CM.Daemon.dll --uninstall
</code></pre>
<ol start=""4"">
<li>Check the log. When running as a background Windows Service you’ll want to check on the log to make sure that everything is OK. A rolling log window is written to the <code>log.txt</code> file next to the CM.Daemon.dll. This file contains the application’s log output in “newest first” order for convenience.</li>
</ol>
<p><strong>Please always check</strong> the log output after 1 minute to make sure you have a Predecessor (which implies that inbound connections are working through your NAT.)</p>
<blockquote>
<p><strong>HINT:</strong> You should be able to visit <a href=""https://127-0-0-1.untrusted-server.com:8000"">https://127-0-0-1.untrusted-server.com:8000</a> and see an instance of the Civil Money website running off of your server. Replace “127-0-0-1” with the external IP address integers of your server when testing for external connectivity.</p>
</blockquote>
<h3><a id=""Customising_the_configuration_76""></a>Customising the configuration</h3>
<p>All settings are in the <code>settings.json</code> file which is side-by-side the <code>CM.Daemon.dll</code>. On Linux, if installed through apt, this will be under <code>/var/civilmoney</code>.</p>
<p>The default settings look like this:</p>
<pre><code>{
  &quot;Settings&quot;: {
    &quot;Port&quot;: 8000,
    &quot;Seeds&quot;: &quot;seed1.civil.money,seed2.civil.money&quot;,
    &quot;DataFolder&quot;: &quot;cm-data&quot;,
    &quot;AuthoritativePfxCertificate&quot;: &quot;&quot;,
    &quot;AuthoritativePfxPassword&quot;: &quot;&quot;,
    &quot;EnableAuthoritativeDomainFeatures&quot;: false,
    &quot;EnablePort80Redirect&quot;: false
  }
}

</code></pre>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Field</th>
<th>Description</th>
</tr>
</thead>
<tbody>
<tr>
<td>Port</td>
<td>If another web server (Apache, IIS, etc) is not installed and already using port 443, you can do <code>sudo setcap cap_net_bind_service=ep /usr/share/dotnet/dotnet</code> to allow .NET Core to bind the port on Linux or for Windows simply simply set the service to run under <code>LocalSystem</code>, and then change the port in the configuration to 443. You will receive start-up errors if the server is unable to bind.</td>
</tr>
<tr>
<td>Seeds</td>
<td>A comma delimited list of known well behaved peers. To eliminate a single point of failure, the Civil Money community may establish seed listings online which are based on IPs.</td>
</tr>
<tr>
<td>DataFolder</td>
<td>A path to a suitable folder for data storage. By default data is kept in a folder beside the <code>CM.Daemon.dll</code>.</td>
</tr>
<tr>
<td>AuthoritativePfxCertificate</td>
<td>Only used by official Civil Money seeds.</td>
</tr>
<tr>
<td>AuthoritativePfxPassword</td>
<td>Only used by official Civil Money seeds.</td>
</tr>
<tr>
<td>EnableAuthoritativeDomainFeatures</td>
<td>Only used by official Civil Money seeds. Enabling this will not have any impact on the network, however your server will do some periodic unnecessary extra work and also attempt to act as an (unused) DNS server for <code>*.untrusted-domain.com</code>.</td>
</tr>
<tr>
<td>EnablePort80Redirect</td>
<td>Only used by official Civil Money seeds. When enabled the server will attempt to bind the regular HTTP port 80 and redirect all requests over to 443 for SSL.</td>
</tr>
</tbody>
</table>
");
        }
    }
}