#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

namespace CM.Javascript {

    /// <summary>
    /// Provides an in-app API reference for visitors.
    /// </summary>
    internal class ApiPage : Page {

        public override string Title {
            get {
                return "The Civil Money API";
            }
        }

        public override string Url {
            get {
                return "/api";
            }
        }

        public override void Build() {
            Element.ClassName = "apipage markdown";

            // To generate this HTML we're using:
            // http://dillinger.io/

            Element.InnerHTML = @"
<h1><a id=""About_Civil_Money_0""></a>The Civil Money API</h1>
<p><b>Reference implementation source code is available on <a href=""https://github.com/civilmoney/Reference-Implementation"">GitHub</a>.</b></p>
<p>Civil Money is an open source debt-free monetary framework which aims to become a unified global currency that can guide us towards a more civilised society. It includes features such as:</p>
<ul>
<li>A generous universal basic income.</li>
<li>A democratic voting process for any fundamental changes to the system.</li>
<li>A low barrier to entry.</li>
<li>Seeding based on regional productivity (inverse taxation.)</li>
<li>Transparent transactions and accountability.</li>
<li>Implicit dispute resolution.</li>
<li>A consensus-based scalable distributed P2P architecture.</li>
<li>An efficient and easy to work with messaging format.</li>
<li>End-to-end TLS between all peers and user clients.</li>
</ul>
<h1><a id=""General_Inspirations_and_Design_Guidelines_14""></a>General Inspirations and Design Guidelines</h1>
<h3><a id=""1_Money_is_basically_valueless_17""></a>1. Money is basically valueless.</h3>
<p>We need to stop thinking about money as some mystical/scarce resource - it’s not. These days it’s just <a href=""http://money.howstuffworks.com/currency6.htm"">SQL data</a>.
<a href=""http://positivemoney.org/issues/debt/"">97% of money in circulation</a> is created endogenously by banks when they extend
loans and credit. It used to be that <a href=""https://en.wikipedia.org/wiki/Reserve_requirement"">reserve requirements</a> were placed upon this system to stop things getting out of hand, but that is no longer true today in most countries. If that isn’t absurd enough, derivative “number games” are played shuffling all of the generated SQL data (debt) around, to the general
detriment and counter-production of society.</p>
<p>Civil Money is a new fiat for people who are ready to try a completely new monetary system. A truly unified global effort to rebuild communities, given the past 200 years of economic adolescence leading current civilisation astray.</p>
<h3><a id=""2_You_are_the_bank_25""></a>2. You are the bank.</h3>
<p>Any person can extend credit to any other person simply by accepting
their Civil Money payment. Even if the buyer’s balance is in the negative, or their credit score is low. Centralised banking institutions as well as loans,
are unnecessary by design. There is no financial motivation for a seller to decline a customer’s payment. You always get paid either way. The only factor at play here is whether or not the customer, be it a person or business entity, appears
genuinely deserving of your goods or services.</p>
<h3><a id=""3_Minimal_barrier_to_entry_31""></a>3. Minimal barrier to entry.</h3>
<p>The only barrier to entry is <em>temporary</em> access to the internet. Meaning any reasonably modern desktop or mobile web browser. Civil Money should work just as well for a remote community in Kenya sharing a single smartphone as it will a person standing at a point of sale terminal.</p>
<p>We do not restrict the creation of new accounts through governmental oversights, or require any forms of identification such as birth certificates etc. Firstly, many developing nations in the world simply have no such data or processing capabilities in place. Secondly, it would place a strong importance on the shear existence of every particular account - <em>“this is me, if I lose this, I’m screwed.”</em> Thirdly, we need to avoid storage of anything that can be used for identity theft.</p>
<p>It is better if the monetary system is designed such that individual accounts are not so important in the big picture. A brand new account is just as good as an old one for essential day-to-day purchases. If you develop amnesia and forget your pass phrase, it’s not the end of the world. Make a new account, set your income eligibility as “Health Problem”, write down your pass phrase, move on with your life.</p>
<h3><a id=""4_We_assume_most_accounts_will_act_in_good_faith_38""></a>4. We assume “most” accounts will act in good faith.</h3>
<p>One long-term study in particular suggests that people are generally well behaved when merely reminded of their
moral compass (see <a href=""http://thedishonestyproject.com/film/"">Prof. Dan Ariely, (dis)honesty - the truth about lies</a>.) We assume this somewhat going to be the general case. Ultimately it is up to society to ignore or remind those who habitually misbehave about the Civil Money Honour Code.</p>
<h3><a id=""5_Misbehaving_accounts_should_minimally_impact_legitimate_accounts_43""></a>5. Misbehaving accounts should minimally impact legitimate accounts.</h3>
<p>The idea is, “congratulations idiot, you’ve made a useless account and sent yourself a bunch of money, good for you.” We need to remind people that money means nothing in the first place. What matters is “are you a genuinely decent human being”, or “for what reason does this guy NOT deserve to be able to buy the thing I’m selling”? The answer is almost always “no reason” or “I just don’t have any left to sell”.</p>
<h3><a id=""6_c_100_always_equals_1hr_of_labour_but_also_USD_50_46""></a>6. //c 1.00 always equals 1hr of labour, but also USD $50</h3>
<p>Civil Money is a <em>hybrid</em> time based currency. Inflation is prevented in Civil Money because its value is pegged to a constant of time. However, the suggested value of //c 1.00 is also USD $50. In other words, an average wage should be $50/hr.</p>
<p>This is based on an upper-middle class USD$ 80,000/yr income over an 8hr work day, 200 days a year (excludes 165 days of weekends/personal/sick/vacation time.)
USD$ 80,000 / 1600hrs = $50/hr.
Since 1hr = //p 1.00 it follows that //p 1.00 = USD$50.</p>
<h3><a id=""7_Doublespend_is_allowed_54""></a>7. Double-spend is allowed.</h3>
<p>Because money means basically nothing, there is no reason why we can’t have implicit dispute resolution. Meaning you can dispute a transaction if a product or service was bad, and both parties will retain their money (the dispute is settled amicably by default.)</p>
<p>To prevent inflation through this mechanism, it reflects badly on users who abuse the system. That is, sellers who frequently do not volunteer a refund during dispute, or a customer who disputes a lot of their purchases.</p>
<h3><a id=""8_Servers_are_never_trusted_59""></a>8. Servers are never trusted.</h3>
<p>A consensus algorithm is always used to corroborate account, transaction and voting data.</p>
<p>Because data is stored in a Distributed Hash Table based on IP address, it is difficult to insert a malicious server at a specific network end-point as to influence the consensus about any particular target account. The more well behaved peers on the network, the more resilient it becomes.</p>
<p>At the end of the day, <em>somebody</em> has to securely deliver a trusted client application that will adhere to all protocols and corroborate data correctly. The <a href=""https://civil.money"">https://civil.money</a> endpoint is provided for this reason, however it is currently a single point of failure. Native applications will eventually need to be created which do not rely on DNS.</p>
<h3><a id=""9_We_use_TLS_but_not_for_protecting_information_secrecy_there_is_none_66""></a>9. We use TLS but not for protecting information secrecy (there is none.)</h3>
<p>Civil Money’s use of TLS is simply to minimise MiTM attacks, javascript tampering and such. Also most mobile frameworks are beginning to demand it.</p>
<h3><a id=""10_All_cryptographic_tasks_must_take_place_on_the_client_70""></a>10. All cryptographic tasks must take place “on the client”.</h3>
<p>Pass phrases should never be cached or transmitted over the internet at any point in time for any reason. Industry standard <a href=""https://www.ietf.org/rfc/rfc2898.txt"">RFC2898 (aka PBKDF2)</a> password key derivation is used to AES encrypt private keys.</p>
<p>The key derivation scheme can be customised/upgraded over time and the private key encryption method can theoretically be up to the client implementation to decide. All clients <em>should</em> however support a set of standardised schemes so people don’t need to always use one particular client application.</p>
<h3><a id=""11_Civil_Money_must_not_become_a_forum_for_advertisement_or_communication_It_is_a_framework_for_decentralised_monetary_exchange_only_75""></a>11. Civil Money must not become a forum for advertisement or communication. It is a framework for decentralised monetary exchange only.</h3>
<p>Storage of emails and general blobs of text are disallowed by design. Even transaction memos are “under the fold” as to deter any kind of spamming activity.</p>
<h3><a id=""12_Taxation_is_implicit_and_inverted_and_governments_can_access_their_funds_78""></a>12. Taxation is implicit and inverted and governments can access their funds.</h3>
<p>Instead of taking money out of pocket, taxation is a money creation process under Civil Money. Meaning the death of taxes. No more periodic tax filing, and tax evasion is impossible.
Governing authority accounts for every geographical region can be created for inverse-tax revenue spending if/when governments decide to join Civil Money.</p>
<h3><a id=""13_People_can_vote_on_changes_to_the_system_82""></a>13. People can vote on changes to the system</h3>
<p>People sign votes in the same way they do transactions.</p>
<p>Researchers are encouraged to collect and analyse votes and account history/transaction patterns from across the network in order to identify “vote stuffing” accounts.</p>
<p>This is a computer sciences issue, as such voting outcomes are only finalised when a reasonable margin of error is established and data has been peer-reviewed through the scientific method.</p>
<p>Initially, since Civil Money is a ghost town, it is up to the steering group to do its best to arrive at the most truthful impartial result. To help with this end, a two-thirds majority win is needed for any proposition to pass and all vote tallying data is freely available for download and verification by anyone.</p>
<h2><a id=""How_it_works_92""></a>How it works</h2>
<h3><a id=""Peers_form_a_Distributed_Hash_Table_DHT_95""></a>Peers form a Distributed Hash Table (<abbr title=""Distributed Hash Table"">DHT</abbr>)</h3>
<p>If you’re unfamiliar with what a <abbr title=""Distributed Hash Table"">DHT</abbr> is, see here: <a href=""https://en.wikipedia.org/wiki/Distributed_hash_table"">https://en.wikipedia.org/wiki/Distributed_hash_table</a></p>
<p>We use the <em>Consistent Hashing</em> model also known as the <a href=""https://en.wikipedia.org/wiki/Chord_(peer-to-peer)"">Chord <abbr title=""Distributed Hash Table"">DHT</abbr></a>.</p>
<p>Each peer’s ID is the first 8 bytes of <code>MD5(&quot;ip-address&quot;)</code>. MD5 is chosen solely for its distribution properties.</p>
<p>We’ll call this hashing function <code>DHT_ID()</code>.</p>
<p>Every peer holds a connection to a <code>predecessor</code> and <code>successor</code>. Thus, the network is basically a massive circular daisy chain. In-memory lookup tables assist in more efficiently resolving the responsible peer for any given <code>DHT_ID</code> by reducing the number of hops.</p>
<p>Each <abbr title=""Distributed Hash Table"">DHT</abbr> peer is responsible for numerical <code>DHT_IDs</code> landing in between itself and its <code>successor</code>.</p>
<p>Account records are stored on the network at:</p>
<pre><code>Server #1 = DHT_ID(&quot;copy1&quot; + AccountID)
Server #2 = DHT_ID(&quot;copy2&quot; + AccountID)
Server #3 = DHT_ID(&quot;copy3&quot; + AccountID)
Server #4 = DHT_ID(&quot;copy4&quot; + AccountID)
Server #5 = DHT_ID(&quot;copy5&quot; + AccountID)
</code></pre>
<p>Each of those servers will <em>independently</em> corroborate any <code>PUT</code> action with its own <abbr title=""Distributed Hash Table"">DHT</abbr>_ID resolution. When enough servers meeting the constant <code>MINIMUM-COPIES-REQUIRED</code> are corroborated, only then can an account, transaction or vote record be committed.</p>
<h3><a id=""All_client_and_interpeer_communication_is_performed_over_HTTP_Secure_WebSockets_WSS_122""></a>All client and inter-peer communication is performed over HTTP Secure WebSockets (WSS.)</h3>
<p>This is not for data secrecy (there is none) but rather for mitigating network based interferences and also satisfying SSL requirements for mobile platforms. No secret or sensitive data ever exists on the Civil Money network.</p>
<p>A throw-away wild-card SSL certificate is deployed with the server application, and a DNS server has been created which will echo sub-domains that look like IPs, such that:</p>
<pre><code>nslookup 127-0-0-1.untrusted-server.com = 127.0.0.1
</code></pre>
<p>This allows the Civil Money server to be hosted by anybody, and all web browsers will pass basic SSL certificate domain name checks. We don’t care that the server may be malicious, they are only one of multiple that we’re going to corroborate its replies against, and we have no secret data to hide from a malicious server in the event that we decide to try using it for object storage.</p>
<p>The reference client implementation never displays or downloads content from <abbr title=""Distributed Hash Table"">DHT</abbr> peers. The only communication going on is a stream of plain text over a single web socket. Any printable data is obviously HTML encoded.</p>
<h1><a id=""The_spec_136""></a>The spec</h1>
<h3><a id=""Messaging_140""></a>Messaging</h3>
<p>All message and object schema formats consist of a UTF-8 plain text dictionary.</p>
<h4><a id=""Request_payload_144""></a>Request payload</h4>
<p>The request message payload format is:</p>
<pre><code>CMD [Action] [NOnce] [Command specific args]
KEY: Value
KEY: Value
...
END [NOnce]
</code></pre>
<h4><a id=""Response_payload_155""></a>Response payload</h4>
<p>The response message payload format is:</p>
<pre><code>RES 0x[hexadecimal CMResult Code] [NOnce] [Command specific args]
KEY: Value
KEY: Value
...
END [NOnce]
</code></pre>
<p>The <code>NOnce</code> can be any random string consisting of letters or numbers and should be reasonably unique for each request. A truncated GUID is used in the reference implementation.</p>
<h3><a id=""Actions_168""></a>Actions</h3>
<p>The following Actions are defined.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Action</th>
<th>Description</th>
</tr>
</thead>
<tbody>
<tr>
<td>PING</td>
<td>Retrieves status information about a <abbr title=""Distributed Hash Table"">DHT</abbr> peer and optionally notifies the peer about your own end-point, if you are participating as a <abbr title=""Distributed Hash Table"">DHT</abbr> peer yourself.</td>
</tr>
<tr>
<td>FIND</td>
<td>Locates the responsible <abbr title=""Distributed Hash Table"">DHT</abbr> peer for the specified <abbr title=""Distributed Hash Table"">DHT</abbr>_ID.</td>
</tr>
<tr>
<td>GET</td>
<td>Gets an object at a specified path.</td>
</tr>
<tr>
<td>PUT</td>
<td>Tentatively puts an object at the specified path and receives a commit token if validation is successful.</td>
</tr>
<tr>
<td>QUERY-COMMIT</td>
<td>Queries the current status of an object’s path, which may be in the process of being <code>PUT</code>.</td>
</tr>
<tr>
<td>COMMIT</td>
<td>Requests that the peer <em>attempt</em> to commit the object associated with a commit token. <abbr title=""Distributed Hash Table"">DHT</abbr> peers will independently <code>QUERY-COMMIT</code> elsewhere on the network to make sure that enough other peers are also in the process of committing the same object.</td>
</tr>
<tr>
<td>LIST</td>
<td>Lists objects under the specified path.</td>
</tr>
<tr>
<td>SUBSCRIBE</td>
<td>Notifies the peer that it should send <code>NOTIFY</code> packets on the established WebSocket connection, about any new updates regarding a specified account.</td>
</tr>
<tr>
<td>NOTIFY</td>
<td>Sent by <abbr title=""Distributed Hash Table"">DHT</abbr> peers to notify a subscribed connection of account changes.</td>
</tr>
<tr>
<td>SYNC</td>
<td>Sent periodically by <abbr title=""Distributed Hash Table"">DHT</abbr> peers to inform other responsible peers on the network about the current state of an account.</td>
</tr>
</tbody>
</table>
<h4><a id=""The_PING_Action_186""></a>The PING Action</h4>
<p>The <code>PING</code> command serves multiple functions simultaneously.</p>
<ul>
<li>Determines whether an end-point is alive.</li>
<li>Acts as a way for you to find your own external network IP.</li>
<li>Provides insight as to the health of the peer. A peer without a Successor or Predecessor is broken and should not be used.</li>
<li>Provides a list of other hints regarding other <em>potentially</em> valid peers on the network.</li>
<li>Optionally informs the <abbr title=""Distributed Hash Table"">DHT</abbr> peer that you yourself are a peer and are trying to participate in the network.</li>
</ul>
<h5><a id=""Example_196""></a>Example</h5>
<pre><code>Request:
CMD PING ac72e
EP: 192.168.0.100:8000
END ac72e

Response:
RES 0x0 ac72e
YOUR-IP: 192.168.0.100
MY-IP: 192.168.0.101
SUCC: 192.168.0.102:443
PRED: 192.168.0.103:8000
SEEN: 192.168.0.102:443,192.168.0.103:8000,192.168.0.104:8000,192.168.0.105:8000
END ac72e
</code></pre>
<h5><a id=""PING_Request_Values_213""></a>PING Request Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>End-point</td>
<td>EP <em>(optional)</em></td>
<td>When specified, the target peer will evaluate your <abbr title=""Distributed Hash Table"">DHT</abbr>_ID and if applicable, attempt to connect and modify its current Predecessor.</td>
</tr>
</tbody>
</table>
<h5><a id=""PING_Response_Values_219""></a>PING Response Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>Your IP</td>
<td>YOUR-IP</td>
<td>Informs the caller of their public IP address. Peers should maintain a list of potential external IPs and update their own <abbr title=""Distributed Hash Table"">DHT</abbr>_ID only when confirmed by a number of other pinged peers.</td>
</tr>
<tr>
<td>My IP</td>
<td>MY-IP</td>
<td>Informs the caller of what the peer <em>thinks</em> its current external IP address is. This is useful for diagnosing peers that are stuck behind NAT. A <abbr title=""Distributed Hash Table"">DHT</abbr> peer is not considered valid until its <code>MY-IP</code> matches that of the outgoing connection.</td>
</tr>
<tr>
<td>Successor</td>
<td>SUCC</td>
<td>Informs the caller of the peer’s currently determined Successor.</td>
</tr>
<tr>
<td>Predecessor</td>
<td>PRED</td>
<td>Informs the caller of the peer’s currently determined Predecessor.</td>
</tr>
<tr>
<td>Seen List</td>
<td>SEEN</td>
<td>Informs the caller about other <em>successfully connecting</em> peers on the network.</td>
</tr>
</tbody>
</table>
<h5><a id=""PING_CMResult_Codes_229""></a>PING CMResult Codes</h5>
<p>Ping must always return <code>CMResult.S_OK</code>.</p>
<h4><a id=""The_FIND_Action_232""></a>The FIND Action</h4>
<p>The <code>FIND</code> action locates the responsible peer for a given <code>DHT_ID</code>. If the value does not fall within the peer’s own <code>DHT_ID</code> and that of its <code>Successor</code>, the request is re-routed to the best known and working potential peer that can handle the specified <abbr title=""Distributed Hash Table"">DHT</abbr>_ID.</p>
<h5><a id=""Example_236""></a>Example</h5>
<pre><code>Request:
CMD FIND 716c0
DHT-ID: az/nz+AWQd4=
HOPS: 192.168.0.102:443,192.168.0.103:8000
MAX-HOPS: 10
END 716c0

Response:
RES 0x0 716c0
HOPS: 192.168.0.102:443,192.168.0.103:8000,192.168.0.104:8000
PEER: 192.168.0.104:8000
END 716c0
</code></pre>
<h5><a id=""FIND_Request_Values_253""></a>FIND Request Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td><abbr title=""Distributed Hash Table"">DHT</abbr> ID</td>
<td><abbr title=""Distributed Hash Table"">DHT</abbr>-ID</td>
<td>The ID to locate on the network.</td>
</tr>
<tr>
<td>Hops So Far</td>
<td>HOPS</td>
<td>A comma delimited list of end-points that have serviced the request. Forwarding peers must add themselves to the end of the HOPS list. If peers find their own end-point in the HOPS list or if <code>MAX-HOPS</code> has been reached, they must terminate the search.</td>
</tr>
<tr>
<td>Maximum Hop Count</td>
<td>MAX-HOPS</td>
<td>Sets the desired maximum number of peers to query before giving up. The default value is 30.</td>
</tr>
</tbody>
</table>
<h5><a id=""FIND_Response_Values_261""></a>FIND Response Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>Hop List</td>
<td>HOPS</td>
<td>A comma delimited list of end-points that have serviced the request.</td>
</tr>
<tr>
<td>Responsible Peer</td>
<td>PEER</td>
<td>The end-point of the <abbr title=""Distributed Hash Table"">DHT</abbr> peer currently responsible for the requested <code>DHT_ID</code>.</td>
</tr>
</tbody>
</table>
<h5><a id=""FIND_CMResult_Codes_269""></a>FIND CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Max_Hops_Reached</li>
<li>E_Invalid_Request</li>
<li>E_Not_Enough_Peers</li>
</ul>
<h4><a id=""The_GET_Action_275""></a>The GET Action</h4>
<p>All objects are stored in a deterministic folder or path on a <abbr title=""Distributed Hash Table"">DHT</abbr> peer. The <code>GET</code> action attempts to retrieve the latest copy of any given Account, Transaction or Vote.</p>
<h5><a id=""Example_278""></a>Example</h5>
<pre><code>Request:
CMD GET 6ec4a ACCNT/test1?calculations-date=2016-09-24T10:00:00
END 6ec4a

Response:
RES 0x0 6ec4a
ID: test1
VER: 1
REG: CA-NS
UTC: 2016-09-16T17:18:45
UPD-UTC: 2016-09-16T17:18:45
ATTR-ELIG: UNEMP
ATTR-SKILL: 2,Roofer
ATTR-SKILL: 0,Fiddler
ATTR-PUSH: &quot;My notification &quot;&quot;sink&quot;&quot;&quot;,https://something.com/endpoint
PRIKEY: 0,9GO27EHf27eiuW+bd+6genl7h+8+ByNlWgqFG4p2vio=,CLru67gKQDyqetnwmtuX0IgjfE7nQjYxkSrvVJnqmcvHK7tpaMVNucrS2LKc0JV4LKGlQB0MXhR6fYRzNr5MSZqY3DkzYKF5H/3pdFQCqKS+2wagXFCA521we4bULtB5zIjK/4xTYltKfm08vMnJr26vxiEyBFUqXgjpDr5IHX8x3RT33hRvtYbMC7Z9JNFq
PUBKEY: 2016-09-16T17:18:45,1N8SIc03kFcY4EB9s3jkBshSFL5zsaRiGvOVAy/0whBtlJ5S4ReL0WpydJkJ0TqK4iU/CfDThLVtbEIteJDLE0BXI+pbMzeOhtLjPZBDye83q2GeQq9d2sfpmkI3uqW2D+NCo+nC//CMtaE9JqmmpTnKKEw4I3/oXBrtZj7x7ss=,
SIG: Qju9v3SDEEJ2/6/3whJ9MqlNomU36SCfU9Vr7ukCHAD9kPgQxUsSbLEcZ9gQpn4Bgzvb7IaRe183RpSmAWNUQpe3aSofgbEhzkdAuiE5EKLJu1KJ88vNy25j0By6xtorsd30b2yHEuyHs4m9Kz9mBxNdZU0h5/nMvtDz4qXitEU=
CALC-LAST-TRANS: 2016-09-27T12:01:23
CALC-DEBITS: 1.000234
CALC-CREDITS: 2.000000
CALC-REP: 50.0
CAN-VOTE: 0
END 6ec4a
</code></pre>
<h5><a id=""GET_Request_Values_306""></a>GET Request Values</h5>
<p>There is no key/value request body for a <code>GET</code> request. The object path is specified as the argument of the request <code>CMD</code> line.</p>
<p>For the <code>Account</code> object path, a query parameter <code>calculations-date</code> is permitted, which will instruct the peer to produce an <strong>uncorroborated</strong> summary of the account’s balance and reputation. These values begin with <code>CALC-*</code> in the response body, and must be omitted during any RSA signing checks.</p>
<h5><a id=""GET_Response_Values_312""></a>GET Response Values</h5>
<p>The <code>GET</code> response always consists of the object’s raw text key/value dictionary.</p>
<p>For <code>Account</code> objects where a <code>calculations-date</code> has been included, the following Account <code>CALC-</code> attributes are currently defined.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>Last Transaction</td>
<td>CALC-LAST-TRANS</td>
<td>The time stamp of the last transaction <code>MAX(PYR-UTC, PYE-UTC)</code>.</td>
</tr>
<tr>
<td>Recent Credits</td>
<td>CALC-CREDITS</td>
<td>The sum of all depreciated credit transactions.</td>
</tr>
<tr>
<td>Recent Debits</td>
<td>CALC-DEBITS</td>
<td>The sum of all depreciated debit transactions.</td>
</tr>
<tr>
<td>Recent Reputation</td>
<td>CALC-REP</td>
<td>The Recent Reputation credit score is defined as <code>MIN(1, (BASIC-YEARLY-ALLOWANCE + DEPRECIATED-CREDITS) / ( DEPRECIATED-DEBITS + BASIC-YEARLY-ALLOWANCE * 2 )) * 100</code>.</td>
</tr>
<tr>
<td>Can Vote</td>
<td>CAN-VOTE</td>
<td><code>1</code> (true) if the account has at least 1 transaction every month for the last 12 months with multiple parties, otherwise <code>0</code> (false.) This is a <em>hint</em> for the client regarding an account’s voting eligibility.</td>
</tr>
</tbody>
</table>
<h5><a id=""GET_CMResult_Codes_327""></a>GET CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Invalid_Request</li>
<li>E_Item_Not_Found</li>
<li>E_Invalid_Object_Path</li>
<li>E_Account_ID_Invalid</li>
</ul>
<h4><a id=""The_PUT_Action_337""></a>The PUT Action</h4>
<p>The <code>PUT</code> action informs a <abbr title=""Distributed Hash Table"">DHT</abbr> peer of your intention to commit a new or updated copy of an object. Object-specific update rules are validated and a <code>Commit Token</code> is included in the response if the object appears to be valid.</p>
<h5><a id=""Example_341""></a>Example</h5>
<pre><code>Request:
CMD PUT fbd06 TRANS/2016-09-24T20:13:30 test1 test2
VER: 1
UTC: 2016-09-24T20:13:30
PYR-ID: test2
PYR-REG: CA-NS
PYR-STAT: Accept
PYR-UTC: 2016-09-24T20:13:30
PYE-ID: test1
MEMO: Thank you for the thing, it was very thingy.
AMNT: 1.000000
PYR-SIG: PR+VGiRLJz1xwZTs5JOF5rxngL8vYEftkW5yCS2IA2YqNoQ73Tnw3WydQ4ZoLJ3UqEzB/suDa8GoPMdjG1esuuVW9MXsvkNXbkT+Wb+qCydNpxdETNXv6352oaSDLIO8sT8cnSwBdyIl80FRiy/ITH42ZAb9jM+T3iexiGNsE5A=
END fbd06

Response:
RES 0x0 fbd06 dfb67b3d-55fe-4e41-8dc8-aeb51dbb8253
END fbd06
</code></pre>
<h5><a id=""PUT_Request_Values_363""></a>PUT Request Values</h5>
<p>The <code>PUT</code> request body consist of an object key/value dictionary. The <code>CMD</code> command argument is the object’s path.</p>
<h5><a id=""PUT_Response_Values_367""></a>PUT Response Values</h5>
<p>There is no key/value response body in a <code>PUT</code> request. The <code>RES</code> response line contains a GUID commit token as its command specific argument.</p>
<h5><a id=""PUT_CMResult_Codes_371""></a>PUT CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Unknown_API_Version</li>
<li>E_Account_ID_Invalid</li>
<li>E_Object_Superseded</li>
<li>Type-specific error codes</li>
</ul>
<h4><a id=""The_QUERYCOMMIT_Action_381""></a>The QUERY-COMMIT Action</h4>
<p>The <code>QUERY-COMMIT</code> action allows a <abbr title=""Distributed Hash Table"">DHT</abbr> peer or client to confirm whether or not a peer is about to commit or has already commited an object with the correct <code>Updated UTC</code> time stamp.</p>
<h5><a id=""Example_385""></a>Example</h5>
<pre><code>Request:
CMD QUERY-COMMIT 4e93c TRANS/2016-09-24T20:13:30 test1 test2
END 4e93c

Response:
RES 0x0 4e93c 2016-09-24T20:13:30
END 4e93c
</code></pre>
<h5><a id=""QUERYCOMMIT_Request_Values_397""></a>QUERY-COMMIT Request Values</h5>
<p>There is no key/value request body for a <code>QUERY-COMMIT</code> request. The request <code>CMD</code> line contains the object <code>Path</code> about to be saved. This allows all <abbr title=""Distributed Hash Table"">DHT</abbr> peers to query the commit status of another responsible peers, without knowing what the object’s commit token is.</p>
<h5><a id=""QUERYCOMMIT_Response_Values_401""></a>QUERY-COMMIT Response Values</h5>
<p>There is no key/value response body in a <code>QUERY-COMMIT</code> request. The <code>RES</code> response line contains the object’s <code>Updated UTC</code> time stamp. The object <em>may or may not</em> be already committed.</p>
<p><abbr title=""Distributed Hash Table"">DHT</abbr> peers must return the highest <code>Updated UTC</code> either on record or pending commit.</p>
<h5><a id=""QUERYCOMMIT_CMResult_Codes_407""></a>QUERY-COMMIT CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Item_Not_Found</li>
</ul>
<h4><a id=""The_COMMIT_Action_413""></a>The COMMIT Action</h4>
<p>The <code>COMMIT</code> action instructs a <abbr title=""Distributed Hash Table"">DHT</abbr> peer to independently corroborate an object’s status on the network and, if successful, commit the record to permanent storage and indexing.</p>
<h5><a id=""Example_417""></a>Example</h5>
<pre><code>Request:
CMD COMMIT 506ab dfb67b3d-55fe-4e41-8dc8-aeb51dbb8253
END 506ab

Response:
RES 0x0 506ab
END 506ab
</code></pre>
<h5><a id=""COMMIT_Request_Values_428""></a>COMMIT Request Values</h5>
<p>There is no key/value request body for a <code>COMMIT</code> request. The request <code>CMD</code> line contains the object <code>Commit Token</code> to be committed.</p>
<h5><a id=""COMMIT_Response_Values_432""></a>COMMIT Response Values</h5>
<p>There is no key/value response body in a <code>COMMIT</code> request.</p>
<h5><a id=""COMMIT_CMResult_Codes_436""></a>COMMIT CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Item_Not_Found</li>
<li>E_Not_Enough_Peers</li>
</ul>
<h4><a id=""The_LIST_Action_443""></a>The LIST Action</h4>
<p>The <code>LIST</code> action provides basic lookup capability for object paths and includes basic sorting and pagination functionality.</p>
<h5><a id=""Example_447""></a>Example</h5>
<pre><code>Request:
CMD LIST 636a1 ACCNT/test2/TRANS
VER: 1
START: 0
MAX: 10
UTC-FROM: 2014-09-24T21:30:02
UTC-TO: 2016-09-25T21:30:02
SORT: UPD-UTC DESC
END 636a1

Response:
RES 0x0 636a1
START: 0
TOTAL: 22
COUNT: 10
ITEM: 2016-09-24T20:13:30 test1 test2 1.000000 2016-09-24T20:13:30 0 1  CA-NS
ITEM: 2016-09-21T23:54:52 test1 test2 1.000000 2016-09-21T23:54:52 0 1  CA-NS
ITEM: 2016-09-21T23:42:18 test1 test2 1.000000 2016-09-21T23:50:49 1 1 CA-NS CA-NS
ITEM: 2016-09-21T23:36:44 test1 test2 1.000000 2016-09-21T23:50:49 1 1 CA-NS CA-NS
ITEM: 2016-09-21T22:20:34 test1 test2 1.000000 2016-09-21T22:20:34 0 1  CA-NS
ITEM: 2016-09-21T22:16:40 test1 test2 1.000000 2016-09-21T22:16:40 0 1  CA-NS
ITEM: 2016-09-21T16:06:17 test1 test2 4.000000 2016-09-21T16:06:17 0 1  CA-NS
ITEM: 2016-09-21T16:04:28 test1 test2 5.000000 2016-09-21T16:04:28 0 1  CA-NS
ITEM: 2016-09-15T15:06:34 test1 test2 1.000000 2016-09-15T15:06:34 0 1  CA-NS
ITEM: 2016-09-15T14:45:50 test1 test2 1.000000 2016-09-15T14:48:24 1 1 CA-NS CA-NS
END 636a1
</code></pre>
<h5><a id=""LIST_Request_Values_477""></a>LIST Request Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>Start At Index</td>
<td>START</td>
<td>The starting record index, used for pagination.</td>
</tr>
<tr>
<td>Max Records</td>
<td>MAX</td>
<td>The maximum number of records to return. The default value is 1000 when not specified.</td>
</tr>
<tr>
<td>Updated Utc From Inclusive</td>
<td>UTC-FROM</td>
<td>The item <code>Updated UTC</code> date to begin listing from.</td>
</tr>
<tr>
<td>Updated Utc To Exclusive</td>
<td>UTC-TO</td>
<td>The item <code>Updated UTC</code> date to stop before.</td>
</tr>
<tr>
<td>Sorting</td>
<td>SORT</td>
<td>Path-specific field sorting e.g. <code>PYR-ID ASC</code>.</td>
</tr>
</tbody>
</table>
<p>The following path sorting fields are defined for both <code>ASC</code> and <code>DESC</code> directions.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Path</th>
<th>Field</th>
</tr>
</thead>
<tbody>
<tr>
<td>ACCNT/</td>
<td>UTC</td>
</tr>
<tr>
<td>ACCNT/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>ACCNT/</td>
<td>ID</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/VOTES/</td>
<td>UTC</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/VOTES/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/TRANS/</td>
<td>UTC</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/TRANS/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/TRANS/</td>
<td>PYR-ID</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/TRANS/</td>
<td>PYE-ID</td>
</tr>
<tr>
<td>ACCNT/<code>ID</code>/TRANS/</td>
<td>AMNT</td>
</tr>
<tr>
<td>VOTES/<code>PropositionID</code>/</td>
<td>UTC</td>
</tr>
<tr>
<td>VOTES/<code>PropositionID</code>/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>VOTES/<code>PropositionID</code>/</td>
<td>VTR-ID</td>
</tr>
<tr>
<td>TRANS/</td>
<td>UTC</td>
</tr>
<tr>
<td>TRANS/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>TRANS/</td>
<td>PYR-ID</td>
</tr>
<tr>
<td>TRANS/</td>
<td>PYE-ID</td>
</tr>
<tr>
<td>TRANS/</td>
<td>AMNT</td>
</tr>
<tr>
<td>REGIONS/<code>Region</code>/TRANS/</td>
<td>UTC</td>
</tr>
<tr>
<td>REGIONS/<code>Region</code>/TRANS/</td>
<td>UPD-UTC</td>
</tr>
<tr>
<td>REGIONS/<code>Region</code>/TRANS/</td>
<td>PYR-ID</td>
</tr>
<tr>
<td>REGIONS/<code>Region</code>/TRANS/</td>
<td>PYE-ID</td>
</tr>
<tr>
<td>REGIONS/<code>Region</code>/TRANS/</td>
<td>AMNT</td>
</tr>
</tbody>
</table>
<h5><a id=""LIST_Response_Values_517""></a>LIST Response Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>Start Index</td>
<td>START</td>
<td>The requested starting index.</td>
</tr>
<tr>
<td>Count</td>
<td>COUNT</td>
<td>The number of records included in this paginated response.</td>
</tr>
<tr>
<td>Total Record Count</td>
<td>TOTAL</td>
<td>The total number of records available for pagination.</td>
</tr>
<tr>
<td>Item</td>
<td>ITEM <em>(one per result)</em></td>
<td>Type-specific object indexes.</td>
</tr>
</tbody>
</table>
<p>The following one-liner Object Indexes are defined.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Type</th>
<th>Index format</th>
</tr>
</thead>
<tbody>
<tr>
<td>Account</td>
<td><code>ID</code></td>
</tr>
<tr>
<td>Vote</td>
<td><code>Proposition &quot; &quot; Voter &quot; &quot; Value &quot; &quot; Created-UTC &quot; &quot; Updated-UTC</code></td>
</tr>
<tr>
<td>Transaction</td>
<td><code>Created-UTC &quot; &quot; Payee &quot; &quot; Payer &quot; &quot; Amount &quot; &quot; Updated-UTC &quot; &quot; Payee-Status (Byte) &quot; &quot; Payer-Status (Byte) &quot; &quot; Payee-Region &quot; &quot; Payer-Region</code></td>
</tr>
</tbody>
</table>
<p><em>HINT:</em> The beginning of a Transaction index is just the <code>Transaction ID</code> (Date + Payee + Payer.)</p>
<h5><a id=""LIST_CMResult_Codes_538""></a>LIST CMResult Codes</h5>
<ul>
<li>S_OK</li>
<li>E_Invalid_Object_Path</li>
<li>E_Invalid_Request</li>
</ul>
<h4><a id=""The_SUBSCRIBE_Action_544""></a>The SUBSCRIBE Action</h4>
<p>The <code>SUBSCRIBE</code> action informs a <abbr title=""Distributed Hash Table"">DHT</abbr> peer that you would like to be receive “push” notifications for an account. This action should only be called on currently responsible peers for the account, and notifications received through a <code>NOTIFY</code> should not be trusted alone. Clients must look for multiple notifications by multiple subscribed peers before alerting a user.</p>
<p>Subscriptions are removed by <abbr title=""Distributed Hash Table"">DHT</abbr> peers when the underlying WebSocket connection is closed or otherwise broken.</p>
<h5><a id=""Example_550""></a>Example</h5>
<pre><code>Request:
CMD SUBSCRIBE 68343 test2
END 68343

Response:
RES 0x0 68343
END 68343
</code></pre>
<h5><a id=""SUBSCRIBE_Request_Values_562""></a>SUBSCRIBE Request Values</h5>
<p>There is no key/value request body for a <code>SUBSCRIBE</code> request. The request <code>CMD</code> line contains the account <code>ID</code> to receive notifications for.</p>
<h5><a id=""SUBSCRIBE_Response_Values_566""></a>SUBSCRIBE Response Values</h5>
<p>There is no key/value response body in a <code>SUBSCRIBE</code> request.</p>
<h5><a id=""SUBSCRIBE_CMResult_Codes_570""></a>SUBSCRIBE CMResult Codes</h5>
<ul>
<li>S_OK</li>
</ul>
<h4><a id=""The_NOTIFY_Action_574""></a>The NOTIFY Action</h4>
<p><abbr title=""Distributed Hash Table"">DHT</abbr> peers send a <code>NOTIFY</code> to any open client WebSocket connections that have requested push notifications for an account ID.</p>
<h5><a id=""Example_578""></a>Example</h5>
<pre><code>Push Notification:
CMD NOTIFY b7a82 TRANS/2016-09-24T20:13:30 test1 test2
VER: 1
UTC: 2016-09-24T20:13:30
PYR-ID: test2
PYR-REG: CA-NS
PYR-STAT: Cancel
PYR-UTC: 2016-09-24T22:13:30
PYE-ID: test1
MEMO: Thank you for the thing, it was very thingy.
AMNT: 1.000000
PYR-SIG: PTkJy/MdUjYY3Xiv5KCl+mt8Y7lm7yJs60LjqNt3BMhd6tDbkJ4liKr9aCQHdxQLd0BPSsFmfKMLGJpFseBw081eQhsSmaf1dQWjS5w9kktSZRoWGGCHHQnckWbLjs33VExoYgaH+rp+x5ZkrdZ/INX9nH07CngyTLUWmWMl9j8=
END b7a82

Client reply to DHT Peer:
RES 0x0 b7a82
END b7a82
</code></pre>
<h5><a id=""NOTIFY_Request_Values_599""></a>NOTIFY Request Values</h5>
<p>The <code>NOTIFY</code> action is an object key/value body. The <code>CMD</code> request argument is the object’s path, which can be used to uniquely identify the object as well its type.</p>
<h5><a id=""NOTIFY_Response_Values_603""></a>NOTIFY Response Values</h5>
<p>Clients should return <code>S_OK</code> to all <code>NOTIFY</code> messages.</p>
<h5><a id=""NOTIFY_CMResult_Codes_607""></a>NOTIFY CMResult Codes</h5>
<ul>
<li>S_OK</li>
</ul>
<h4><a id=""The_SYNC_Action_611""></a>The SYNC Action</h4>
<p>The <code>SYNC</code> action must be performed periodically by all <abbr title=""Distributed Hash Table"">DHT</abbr> peers in order to keep the network sufficiently populated with multiple copies of every account.</p>
<h5><a id=""Example_615""></a>Example</h5>
<pre><code>Request:
CMD SYNC 0021d test1
VER: 1
ID: test1
UPD-UTC: 2016-09-11T13:57:55
EP: 192.168.0.88:8012
TRANS-HASH: LBjy0hsRdsYPJswaKDrfLpwFXAgH4LdU7tY18vpbajo=
VOTES-HASH: 2vPI9Q1MvfxGfy011o71AoK+/YbWyZkBYRf1C6EMT44=
END 0021d

Response:
RES 0x0 0021d
END 0021d
</code></pre>
<h5><a id=""SYNC_Request_Values_632""></a>SYNC Request Values</h5>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>My End-point</td>
<td>EP</td>
<td>The calling <abbr title=""Distributed Hash Table"">DHT</abbr> peer’s service end-point. This informs the destination of the endpoint to use in order to potentially retrieve the item if it can’t be found elsewhere on the network.</td>
</tr>
<tr>
<td>Updated UTC</td>
<td>UPD-UTC</td>
<td>The account record’s current <code>Updated UTC</code> ISO-8601 value. If the destination determines that its is obsolete, it will query the network for a <em>corroborated</em> copy of the current account.</td>
</tr>
<tr>
<td>Transactions Hash</td>
<td>TRANS-HASH</td>
<td>An SHA256 hash of all ISO-8601 <code>Updated UTC</code> Transaction values for the account, hashed in ascending order.</td>
</tr>
<tr>
<td>Votes Hash</td>
<td>VOTES-HASH</td>
<td>An SHA256 hash of all ISO-8601 <code>Updated UTC</code> Vote values for the account, hashed in ascending order.</td>
</tr>
</tbody>
</table>
<h5><a id=""SYNC_Response_Values_643""></a>SYNC Response Values</h5>
<p>There is no key/value response body in a <code>SYNC</code> request.</p>
<h5><a id=""SYNC_CMResult_Codes_647""></a>SYNC CMResult Codes</h5>
<ul>
<li>S_OK</li>
</ul>
<h4><a id=""Object_Storage_Paths_651""></a>Object Storage Paths</h4>
<p><abbr title=""Distributed Hash Table"">DHT</abbr> peers must be able to GET, PUT and LIST items stored in the following deterministic locations.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Type</th>
<th>Action</th>
<th>Path</th>
</tr>
</thead>
<tbody>
<tr>
<td>Account</td>
<td>GET/PUT</td>
<td>ACCNT/<code>ID</code></td>
</tr>
<tr>
<td>Account</td>
<td>LIST</td>
<td>ACCNT/</td>
</tr>
<tr>
<td>Transaction</td>
<td>GET/PUT</td>
<td>TRANS/<code>yyyy-MM-ddTHH:mm:ss payee payer</code></td>
</tr>
<tr>
<td>Transaction</td>
<td>LIST</td>
<td>ACCNT/<code>Payer|Payee ID</code>/TRANS/</td>
</tr>
<tr>
<td>Transaction</td>
<td>LIST</td>
<td>REGION/<code>Region Code</code>/TRANS/</td>
</tr>
<tr>
<td>Vote</td>
<td>GET/PUT</td>
<td>VOTES/<code>Proposition ID</code>/<code>Voter ID</code></td>
</tr>
<tr>
<td>Vote</td>
<td>LIST</td>
<td>VOTES/<code>Proposition ID</code>/</td>
</tr>
<tr>
<td>Vote</td>
<td>LIST</td>
<td>ACCNT/<code>Voter ID</code>/VOTES/</td>
</tr>
</tbody>
</table>
<p><abbr title=""Distributed Hash Table"">DHT</abbr> peers should store versioned copies of records in a format and storage scheme suitable for
handling millions of rows.</p>
<h3><a id=""Object_Types_673""></a>Object Types</h3>
<p>The following object types are defined.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Action</th>
<th>Description</th>
</tr>
</thead>
<tbody>
<tr>
<td>Account</td>
<td>Describes a user account.</td>
</tr>
<tr>
<td>Transaction</td>
<td>Describes a digitally signed money transfer between accounts.</td>
</tr>
<tr>
<td>Vote</td>
<td>Describes a user vote for a proposal raised by the Civil Money steering group.</td>
</tr>
</tbody>
</table>
<h4><a id=""The_Account_object_685""></a>The Account object</h4>
<p>The Account object schema is:</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Field</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>Account ID</td>
<td>ID</td>
<td>string, max 48 utf-8 bytes</td>
</tr>
<tr>
<td>Created UTC</td>
<td>UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Updated UTC</td>
<td>UPD-UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Region</td>
<td>REG</td>
<td>ISO 3166-2 subdivision code</td>
</tr>
<tr>
<td>Private Key</td>
<td>PRIKEY</td>
<td>PrivateKeySchemeID* “,” Salt base64 “,” Encrypted base64</td>
</tr>
<tr>
<td>Public Key <em>(multiple allowed)</em></td>
<td>PUBKEY</td>
<td>ISO-8601 Effective Date “,” Key base64 “,” <em>Modification Signature</em>** base64</td>
</tr>
<tr>
<td>Attributes</td>
<td>ATTR-*</td>
<td>Extensible account attributes***</td>
</tr>
<tr>
<td>Signature</td>
<td>SIG</td>
<td>base64 RSA signature of all values</td>
</tr>
</tbody>
</table>
<p>* Currently recognised PrivateKeySchemeIDs. <strong>Encryption and decryption always take place on the client</strong>.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>ID</th>
<th>Scheme</th>
</tr>
</thead>
<tbody>
<tr>
<td>0</td>
<td>Encrypted using AES CBC mode and PKCS7 padding, with 16 byte IV and 32 byte Key. The IV and Key are derived using RFC2898 HMACSHA1 with 10,000 iterations.</td>
</tr>
</tbody>
</table>
<p>** The modification signature is necessary only when changing the private key after initial account creation. Clients and peers must select the public key with a suitable <em>effective date</em> according to an objects <code>UPD-UTC</code> timestamp.</p>
<p>*** Currently recognised account attributes:</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Name</th>
<th>Key</th>
<th>Values</th>
<th>Purpose</th>
</tr>
</thead>
<tbody>
<tr>
<td>Governing Authority</td>
<td>ATTR-GOV</td>
<td>base64 signature of values <code>UTC + REG</code></td>
<td>A secret private key held by the Civil Money steering group is used to generate a governing authority key, which designates a particular Civil Money account as the recipient of Inverse-Taxation income for a region. These will be assigned to governments on an as-requested basis after a vetting process (social engineering is an obvious challenge/attack vector here.)</td>
</tr>
<tr>
<td>Income Eligibility</td>
<td>ATTR-ELIG</td>
<td><code>WORK</code> Working, <code>HLTH</code> Health Problem, <code>UNEMP</code> Unemployed, <code>AGED</code> Retired</td>
<td>Provides people with a basic hint about a customer’s personal circumstance. A low credit score might be because they’re disabled or what have you. This just one of multiple considerations. The value does not affect credit rating so there is no incentive to lie.</td>
</tr>
<tr>
<td>Skill or Service</td>
<td>ATTR-SKILL <em>(multiple allowed)</em></td>
<td><code>SkillLevel* &quot;,&quot; Description</code></td>
<td>Provides more context about a person’s potential contribution to society.</td>
</tr>
<tr>
<td>Push Notification</td>
<td>ATTR-PUSH <em>(multiple allowed)</em></td>
<td>CSV of <code>Label,HTTP endpoint</code></td>
<td><abbr title=""Distributed Hash Table"">DHT</abbr> peers post a notification to this end point any time an object is created or updated, that was <strong>not</strong> through a <code>SYNC</code> operation.</td>
</tr>
</tbody>
</table>
<p>* Skill levels:</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Value</th>
<th>Level</th>
</tr>
</thead>
<tbody>
<tr>
<td>0</td>
<td>Amateur</td>
</tr>
<tr>
<td>1</td>
<td>Qualified</td>
</tr>
<tr>
<td>2</td>
<td>Experienced</td>
</tr>
<tr>
<td>3</td>
<td>Certified</td>
</tr>
</tbody>
</table>
<h5><a id=""Example_728""></a>Example</h5>
<pre><code>ID: test1
VER: 1
REG: CA-NS
UTC: 2016-09-16T17:18:45
UPD-UTC: 2016-09-16T17:18:45
ATTR-ELIG: UNEMP
ATTR-SKILL: 2,Roofer
ATTR-SKILL: 0,Fiddler
ATTR-PUSH: &quot;My notification &quot;&quot;sink&quot;&quot;&quot;,https://something.com/endpoint
PRIKEY: 0,9GO27EHf27eiuW+bd+6genl7h+8+ByNlWgqFG4p2vio=,CLru67gKQDyqetnwmtuX0IgjfE7nQjYxkSrvVJnqmcvHK7tpaMVNucrS2LKc0JV4LKGlQB0MXhR6fYRzNr5MSZqY3DkzYKF5H/3pdFQCqKS+2wagXFCA521we4bULtB5zIjK/4xTYltKfm08vMnJr26vxiEyBFUqXgjpDr5IHX8x3RT33hRvtYbMC7Z9JNFq
PUBKEY: 2016-09-16T17:18:45,1N8SIc03kFcY4EB9s3jkBshSFL5zsaRiGvOVAy/0whBtlJ5S4ReL0WpydJkJ0TqK4iU/CfDThLVtbEIteJDLE0BXI+pbMzeOhtLjPZBDye83q2GeQq9d2sfpmkI3uqW2D+NCo+nC//CMtaE9JqmmpTnKKEw4I3/oXBrtZj7x7ss=,
SIG: Qju9v3SDEEJ2/6/3whJ9MqlNomU36SCfU9Vr7ukCHAD9kPgQxUsSbLEcZ9gQpn4Bgzvb7IaRe183RpSmAWNUQpe3aSofgbEhzkdAuiE5EKLJu1KJ88vNy25j0By6xtorsd30b2yHEuyHs4m9Kz9mBxNdZU0h5/nMvtDz4qXitEU=
</code></pre>
<h5><a id=""Account_rules_745""></a>Account rules</h5>
<ol>
<li><code>Created UTC</code> and <code>ID</code> are read-only upon creation.</li>
<li><code>Updated UTC</code> must equal <code>Created UTC</code> during creation.</li>
<li><code>Public Keys</code> are read-only when appended and cannot be removed.</li>
<li>Clients must always sign objects using the newest <code>Public Key</code>.</li>
<li>For signature validation, clients must iterate through the account’s public keys in order to find the correct key based on the key <code>Effective Date</code> and the data’s <code>Updated UTC</code>.</li>
<li>When a new Public key is append is must include the <code>Modification Signature</code> component. The signature is an RSA of the <strong>new</strong> <code>Effective Date</code> utf-8 string, and raw <code>Key</code> byes, using the <strong>previous</strong> public/private key pair.</li>
<li>Peers must validate all public keys to make sure that each entry successfully validates the key after it.</li>
</ol>
<h4><a id=""The_Transaction_object_757""></a>The Transaction object</h4>
<p>The Transaction object schema is:</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Field</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>Created UTC</td>
<td>UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Amount</td>
<td>AMNT</td>
<td>The amount of the transaction up to 6 decimal places. Decimal separator is ‘.’</td>
</tr>
<tr>
<td>Memo</td>
<td>MEMO</td>
<td>A reader-friendly plain-text UTF-8 description or note about the transaction. Maximum allowed length is 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all memos.</td>
</tr>
<tr>
<td>Payee ID</td>
<td>PYE-ID</td>
<td>The account ID of the recipient/payee</td>
</tr>
<tr>
<td>Payee Region</td>
<td>PYE-REG</td>
<td>The Payee’s ISO 3166-2 subdivision code at the time of transaction.</td>
</tr>
<tr>
<td>Payee Tag</td>
<td>PYE-TAG</td>
<td>An optional electronic tag which is typically defined by the payee during acceptance. Up to 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all tags.</td>
</tr>
<tr>
<td>Payee Updated UTC</td>
<td>PYE-UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Payee Status</td>
<td>PYE-STAT</td>
<td>NotSet, Accept, Decline, Refund</td>
</tr>
<tr>
<td>Payee Signature</td>
<td>PYE-SIG</td>
<td>base64 RSA signature of <code>Payee Signing Data</code>*</td>
</tr>
<tr>
<td>Payer ID</td>
<td>PYR-ID</td>
<td>The account ID of the sender/payer</td>
</tr>
<tr>
<td>Payer Region</td>
<td>PYR-REG</td>
<td>The Payer’s ISO 3166-2 subdivision code at the time of transaction.</td>
</tr>
<tr>
<td>Payer Tag</td>
<td>PYR-TAG</td>
<td>An optional electronic tag which is typically defined by the payer during creation. Up to 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all tags.</td>
</tr>
<tr>
<td>Payer Updated UTC</td>
<td>PYR-UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Payer Status</td>
<td>PYR-STAT</td>
<td>NotSet, Accept, Dispute, Cancel</td>
</tr>
<tr>
<td>Payer Signature</td>
<td>PYR-SIG</td>
<td>base64 RSA signature of <code>Payeer Signing Data</code>**</td>
</tr>
</tbody>
</table>
<p>* <code>Payee Signing Data</code> is defined as the following values:</p>
<ul>
<li>ISO-8601 Created UTC</li>
<li>Amount (with 6 decimals)</li>
<li>Payee ID</li>
<li>Payer ID</li>
<li>Memo</li>
<li>Payee ISO-8601 Updated UTC</li>
<li>Payee Tag</li>
<li>Payee Status (enum byte value)</li>
<li>Payee Region</li>
</ul>
<p>** <code>Payer Signing Data</code> is defined as the following values:</p>
<ul>
<li>ISO-8601 Created UTC</li>
<li>Amount (with 6 decimals)</li>
<li>Payee ID</li>
<li>Payer ID</li>
<li>Memo</li>
<li>Payer ISO-8601 Updated UTC</li>
<li>Payer Tag</li>
<li>Payer Status (enum byte value)</li>
<li>Payer Region</li>
</ul>
<h5><a id=""Example_806""></a>Example</h5>
<pre><code>VER: 1
UTC: 2016-09-16T18:18:51
AMNT: 2.000000
MEMO: Thank you for shopping at blank
PYR-ID: test2
PYR-UTC: 2016-09-16T18:18:51
PYR-REG: CA-NB
PYR-STAT: Accept
PYR-TAG: My Expenses, Some other tag
PYR-SIG: l3cQFcSTPKte8SFgsCcT2nJ360j+pMooAjQ+BBgG62ccrOlejC26Fq/AzMVyHFT1VxIsdstfTnwX6Lg9EfwJ9NKFDlsBJVsqw2hsznD24HuB3yvRb+LIxbWrqsjSMEHCH4AsQ31FEDnYC0+5l8r/60ZUjZshJYH2snWBcmTIhwo=
PYE-ID: test1
PYE-UTC: 2016-09-16T18:19:51
PYE-REG: CA-NS
PYE-STAT: Accept
PYE-TAG: Receipt #244788222
PYE-SIG: o30hFX9ZC4vkrJsNaJzBbfH+XgOGTUN1xBvew0pA6JmOAXEfI7dVl6e+ZsJDHkP9vH91i/swEY3bt3gsv3GhLJPVPajp2d/LOGEgpGFJJRbQ8WDevISTpCJcif2Us7glBk0ZA9azJbCsLqcbXB1/d7RU1MqqGkaMII7L5g5buHk=
</code></pre>
<h5><a id=""Transaction_rules_826""></a>Transaction rules</h5>
<ol>
<li>
<p><code>Created UTC</code>, <code>Amount</code>, <code>Payer ID</code>, <code>Payee ID</code> and <code>Payer Region</code> are required and read-only upon creation.</p>
</li>
<li>
<p><code>Memo</code> is optional, but read-only upon creation, and must be no longer 255 UTF8 bytes in length.</p>
</li>
<li>
<p>Payer/Payee tags must be no longer than 48 UTF-8 bytes in length.</p>
</li>
<li>
<p><code>Payer Status</code> must be <code>Accept</code> during creation.</p>
</li>
<li>
<p>The payer’s signature and updated utc is required in order to create a new transaction. This means that only Payers are allow to create a transaction.<br>
<br>
This removes the potential for spamming of unwanted requests for payment. Payees may
instead request payment through the use of payment links or QR codes for minimal
friction during e-commerce or Point of Sale situations.<br>
<br>
Due to inevitability of malicious payment links intended to harvest pass phrases,
a reminder notice on the official <a href=""https://civil.money"">https://civil.money</a> app educates people about
dangers of following any payment links, and to pay special attention to the absence
of address bars or the reminder itself during payment or Point of Sale.</p>
</li>
<li>
<p>Payer/Payee Updated UTC must be greater than or equal to Created UTC.</p>
</li>
<li>
<p>Transaction amount must be 6 decimal places. Therefore, the minimum transaction amount is 0.000001.</p>
</li>
<li>
<p>A linear demurrage on the <code>Amount</code> begins 12 months after <code>Created UTC</code>, over a following 12 month period. <br>
<br>
The function <code>DEPRECIATE()</code> is defined as:</p>
<pre><code>ROUND(MIN(1, MAX(0, 1 - ( (DAYS-SINCE-CREATION - 365) / 365 ))) * AMOUNT, 6)
</code></pre>
<p><br>
This means that only your last two years of activity really matter, and your balance and credit score automatically
restores itself over time if you hit a financially rough spot.<br>
<br>
Also, people cannot hoard vast sums of money, and are encouraged to spend
earnings within their first 12 months, stimulating the economy.<br>
<br>
People who ping-pong money between accounts to get around the demurrage can be identified and should be frowned upon.</p>
</li>
<li>
<p>The unique identifier for any transaction is the following utf-8 string:<br>
<br>
<code>Created Utc + &quot; &quot; + PayeeID + &quot; &quot; + PayerID</code>
<br>
<br>
With this ID scheme, Transaction IDs can be sorted naturally
based on their creation date, collision and exhaustion are impossible,
and there is an implicit maximum of 1 transaction per second
per payer/payee which has the added benefit of blocking non-productive High Frequency
Trading.</p>
</li>
<li>
<p>When payers issue a Dispute they get their money back, but the payee
retains their money also, unless they choose to refund amicably. <br>
<br>
This only works because non-refunded disputed transactions reflect
badly on the seller as well as the buyer. There is disincentive
on both sides to abuse it.</p>
</li>
<li>
<p>The following payer/payee transaction states are allowed:</p>
</li>
</ol>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>PayerStatus</th>
<th>Byte Value (during signing)</th>
<th>Restrictions</th>
</tr>
</thead>
<tbody>
<tr>
<td>NotSet</td>
<td>0</td>
<td>Never Allowed</td>
</tr>
<tr>
<td>Accept</td>
<td>1</td>
<td>Required during creation</td>
</tr>
<tr>
<td>Dispute</td>
<td>2</td>
<td>Previous <code>PayerStatus</code> must be <code>Accept</code></td>
</tr>
<tr>
<td>Cancel</td>
<td>3</td>
<td>Previous <code>PayerStatus</code> must be <code>Accept</code> and the current <code>PayeeStatus</code> must be in <code>NotSet</code></td>
</tr>
</tbody>
</table>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>PayeeStatus</th>
<th>Byte Value (during signing)</th>
<th>Note</th>
</tr>
</thead>
<tbody>
<tr>
<td>NotSet</td>
<td>0</td>
<td>Required during creation.</td>
</tr>
<tr>
<td>Decline</td>
<td>1</td>
<td>Previous <code>PayeeStatus</code> must be <code>NotSet</code></td>
</tr>
<tr>
<td>Accept</td>
<td>2</td>
<td>Previous <code>PayeeStatus</code> must be <code>NotSet</code> or <code>Decline</code></td>
</tr>
<tr>
<td>Refund</td>
<td>3</td>
<td>Previous <code>PayeeStatus</code> must be <code>Accept</code></td>
</tr>
</tbody>
</table>
<h4><a id=""The_Vote_object_906""></a>The Vote object</h4>
<p>The Vote object schema is:</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Field</th>
<th>Key</th>
<th>Value</th>
</tr>
</thead>
<tbody>
<tr>
<td>API Version</td>
<td>VER</td>
<td>1</td>
</tr>
<tr>
<td>Created UTC</td>
<td>UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Updated UTC</td>
<td>UPD-UTC</td>
<td>ISO-8601 UTC date string</td>
</tr>
<tr>
<td>Voter ID</td>
<td>VTR-ID</td>
<td>The account ID of the voter</td>
</tr>
<tr>
<td>Proposition ID</td>
<td>PROP</td>
<td>The proposition ID for the vote.</td>
</tr>
<tr>
<td>Value</td>
<td>VOTE</td>
<td>The boolean value of the vote. <code>1</code> or <code>0</code>*.</td>
</tr>
<tr>
<td>Signature</td>
<td>SIG</td>
<td>base64 RSA signature of all UTF-8 encoded values in the Vote object.</td>
</tr>
</tbody>
</table>
<p>* By design propositions must be put to a vote in the form of a binary question (for or against.) This is to take potentially unfair “ranked order” voting procedures off the table.</p>
<h5><a id=""Example_921""></a>Example</h5>
<pre><code>VER: 1
UTC: 2016-09-16T18:26:28
UPD-UTC: 2016-09-16T18:26:28
VOTE: 1
PROP: 1
VTR-ID: test1
SIG: X3Vx9syas8LNqEURDemnUYGhkd451Dlkl/kJDXxZv37xcYKF6IdaD0wGEfhA/KMyo7XkrEfmhDui7pTrQ9KZbv+XCUKsjz9LNNXHikNDP2OHPlBIsjbhvAB53kb0nESeWMkmIJCXO2lQJHnOhH6RaVXVXFdIhnkuWJI+J0yKzd0=
</code></pre>
<h3><a id=""CMResult_Status_Codes_932""></a>CMResult Status Codes</h3>
<p>Status codes are 32-bit hexadecimal integers. Negative signed integers are errors, positive integers are success. This scheme is similar to a Microsoft Windows HRESULT. The following status codes are defined.</p>
<table class=""table table-striped table-bordered"">
<thead>
<tr>
<th>Value</th>
<th>Name</th>
<th>Description</th>
</tr>
</thead>
<tbody>
<tr>
<td>0x0</td>
<td>S_OK</td>
<td>OK</td>
</tr>
<tr>
<td>0x1</td>
<td>S_False</td>
<td>False</td>
</tr>
<tr>
<td>0x2</td>
<td>S_Item_Transient</td>
<td>At least 1 copy of the item was found, but the minimum number of copies required are not met.</td>
</tr>
<tr>
<td>0x80000000</td>
<td>E_General_Failure</td>
<td>General failure.</td>
</tr>
<tr>
<td>0x80000001</td>
<td>E_Not_Connected</td>
<td>The web socket is not currently connected.</td>
</tr>
<tr>
<td>0x80000002</td>
<td>E_Timeout_Waiting_On_Reply</td>
<td>Time-out waiting on a reply.</td>
</tr>
<tr>
<td>0x80000003</td>
<td>E_Invalid_Action</td>
<td>Invalid action.</td>
</tr>
<tr>
<td>0x80000004</td>
<td>E_Item_Not_Found</td>
<td>The item was not found.</td>
</tr>
<tr>
<td>0x80000005</td>
<td>E_Invalid_Request</td>
<td>Invalid request.</td>
</tr>
<tr>
<td>0x80000006</td>
<td>E_Not_Enough_Peers</td>
<td>There were not enough available peers to corroborate the request.</td>
</tr>
<tr>
<td>0x80000007</td>
<td>E_Invalid_Object_Path</td>
<td>The requested GET or PUT path is not valid for the item provided.</td>
</tr>
<tr>
<td>0x80000008</td>
<td>E_Object_Superseded</td>
<td>A newer version of this item is already being committed.</td>
</tr>
<tr>
<td>0x80000009</td>
<td>E_Max_Hops_Reached</td>
<td>The maximum number of <abbr title=""Distributed Hash Table"">DHT</abbr> peer hops have been reached.</td>
</tr>
<tr>
<td>0x8000000A</td>
<td>E_Connect_Attempt_Timeout</td>
<td>Unable to connect to any servers within a reasonable time-out period.</td>
</tr>
<tr>
<td>0x8000000B</td>
<td>E_Invalid_Search_Date</td>
<td>Invalid search date range.</td>
</tr>
<tr>
<td>0x8000000C</td>
<td>E_Unknown_API_Version</td>
<td>Unknown API version.</td>
</tr>
<tr>
<td>0x8000000D</td>
<td>E_Operation_Cancelled</td>
<td>The operation has been cancelled.</td>
</tr>
<tr>
<td>0x80001000</td>
<td>E_Crypto_Invalid_Password</td>
<td>The specified password didn’t work for decryption.</td>
</tr>
<tr>
<td>0x80001001</td>
<td>E_Crypto_Unrecognized_SchemeID</td>
<td>The account private key scheme ID is not recognised.</td>
</tr>
<tr>
<td>0x80001002</td>
<td>E_Crypto_Rfc2898_General_Failure</td>
<td>Unable to obtain an encryption key using Rfc2898.</td>
</tr>
<tr>
<td>0x80001003</td>
<td>E_Crypto_RSA_Signing_General_Failure</td>
<td>Unable to sign the data using RSA.</td>
</tr>
<tr>
<td>0x80001004</td>
<td>E_Crypto_RSA_Verify_General_Failure</td>
<td>Unable to verify the data using RSA.</td>
</tr>
<tr>
<td>0x80001005</td>
<td>E_Crypto_RSA_Key_Gen_Failure</td>
<td>Unable to generate an RSA key.</td>
</tr>
<tr>
<td>0x80002000</td>
<td>E_Account_Missing_Public_Key</td>
<td>No valid public key was found on the account for the specified time.</td>
</tr>
<tr>
<td>0x80002001</td>
<td>E_Account_ID_Invalid</td>
<td>The account ID is invalid.</td>
</tr>
<tr>
<td>0x80002002</td>
<td>E_Account_IDs_Are_Readonly</td>
<td>Account IDs are read-only.</td>
</tr>
<tr>
<td>0x80002003</td>
<td>E_Account_Created_Utc_Out_Of_Range</td>
<td>Created UTC is too far ahead of the server’s current time.</td>
</tr>
<tr>
<td>0x80002004</td>
<td>E_Account_Created_Utc_Is_Readonly</td>
<td>Created UTC is read-only.</td>
</tr>
<tr>
<td>0x80002005</td>
<td>E_Account_Updated_Utc_Out_Of_Range</td>
<td>Updated UTC is too far ahead of the server’s current time.</td>
</tr>
<tr>
<td>0x80002006</td>
<td>E_Account_Updated_Utc_Is_Old</td>
<td>The account Updated UTC is out-dated. A newer copy exists.</td>
</tr>
<tr>
<td>0x80002007</td>
<td>E_Account_Too_Few_Public_Keys</td>
<td>The number of public keys specified are less than the existing record’s.</td>
</tr>
<tr>
<td>0x80002008</td>
<td>E_Account_Cant_Corroborate</td>
<td>Unable to corroborate account information with the network.</td>
</tr>
<tr>
<td>0x80002009</td>
<td>E_Account_Cant_Corroborate_Public_Keys</td>
<td>Unable to corroborate account information with the network. The network’s copy has too fewer keys than the record provided.</td>
</tr>
<tr>
<td>0x8000200A</td>
<td>E_Account_Invalid_New_Public_Key_Date</td>
<td>The newest public key entry must equal the account’s Updated UTC when adding new keys.</td>
</tr>
<tr>
<td>0x8000200B</td>
<td>E_Account_Public_Key_Mismatch</td>
<td>One or more public keys do not match the existing account.</td>
</tr>
<tr>
<td>0x8000200C</td>
<td>E_Account_Public_Key_Signature_Error</td>
<td>One of the public keys in the account have an invalid RSA signature.</td>
</tr>
<tr>
<td>0x8000200D</td>
<td>E_Account_Signature_Error</td>
<td>The account RSA signature is invalid.</td>
</tr>
<tr>
<td>0x8000200E</td>
<td>E_Account_Invalid_Region</td>
<td>Invalid account region specified.</td>
</tr>
<tr>
<td>0x8000200F</td>
<td>E_Account_Governing_Authority_Attribute_Required</td>
<td>Account names that are equal to an ISO3166-2 subdivision code require a valid governing authority attribute.</td>
</tr>
<tr>
<td>0x80003000</td>
<td>E_Transaction_Payee_Not_Found</td>
<td>The payee could not be found on the network.</td>
</tr>
<tr>
<td>0x80003001</td>
<td>E_Transaction_Payer_Not_Found</td>
<td>The payer could not be found on the network.</td>
</tr>
<tr>
<td>0x80003002</td>
<td>E_Transaction_Invalid_Payee_Signature</td>
<td>Invalid payee signature.</td>
</tr>
<tr>
<td>0x80003003</td>
<td>E_Transaction_Invalid_Payer_Signature</td>
<td>Invalid payer signature.</td>
</tr>
<tr>
<td>0x80003004</td>
<td>E_Transaction_Payer_Signature_Required</td>
<td>The payer’s signature is required.</td>
</tr>
<tr>
<td>0x80003005</td>
<td>E_Transaction_PayeeID_Required</td>
<td>A payee ID is required.</td>
</tr>
<tr>
<td>0x80003006</td>
<td>E_Transaction_PayerID_Required</td>
<td>A payer ID is required.</td>
</tr>
<tr>
<td>0x80003007</td>
<td>E_Transaction_Created_Utc_Out_Of_Range</td>
<td>The transaction’s Created UTC time is out of range. Please check your device’s clock and try again.</td>
</tr>
<tr>
<td>0x80003008</td>
<td>E_Transaction_Payee_Updated_Utc_Out_Of_Range</td>
<td>The payee’s updated UTC time must be greater than Created UTC.</td>
</tr>
<tr>
<td>0x80003009</td>
<td>E_Transaction_Payer_Updated_Utc_Out_Of_Range</td>
<td>The payer’s updated UTC time must be greater than Created UTC.</td>
</tr>
<tr>
<td>0x8000300A</td>
<td>E_Transaction_Amount_Is_Readonly</td>
<td>The transaction amount cannot be altered.</td>
</tr>
<tr>
<td>0x8000300B</td>
<td>E_Transaction_Created_Utc_Is_Readonly</td>
<td>The transaction created UTC cannot be altered.</td>
</tr>
<tr>
<td>0x8000300C</td>
<td>E_Transaction_Payee_Is_Readonly</td>
<td>The transaction payee cannot be altered.</td>
</tr>
<tr>
<td>0x8000300D</td>
<td>E_Transaction_Payer_Is_Readonly</td>
<td>The transaction payer cannot be altered.</td>
</tr>
<tr>
<td>0x8000300E</td>
<td>E_Transaction_Memo_Is_Readonly</td>
<td>The transaction memo cannot be altered.</td>
</tr>
<tr>
<td>0x8000300F</td>
<td>E_Transaction_Invalid_Amount</td>
<td>The transaction amount is invalid.</td>
</tr>
<tr>
<td>0x80003010</td>
<td>E_Transaction_Payee_Region_Required</td>
<td>A payee region is required.</td>
</tr>
<tr>
<td>0x80003011</td>
<td>E_Transaction_Payer_Region_Required</td>
<td>A payer region is required.</td>
</tr>
<tr>
<td>0x80003012</td>
<td>E_Transaction_Payee_Region_Is_Readonly</td>
<td>The payee region is read-only.</td>
</tr>
<tr>
<td>0x80003013</td>
<td>E_Transaction_Payer_Region_Is_Readonly</td>
<td>The payer region is read-only.</td>
</tr>
<tr>
<td>0x80003014</td>
<td>E_Transaction_Payer_Accept_Status_Required</td>
<td>The payer status must be set to Accept during initial creation.</td>
</tr>
<tr>
<td>0x80003015</td>
<td>E_Transaction_Payee_Status_Invalid</td>
<td>The payee status must not be set without the payee’s signature.</td>
</tr>
<tr>
<td>0x80003016</td>
<td>E_Transaction_Payee_Status_Change_Not_Allowed</td>
<td>The new payee status value is not permitted, based on its previous status.</td>
</tr>
<tr>
<td>0x80003017</td>
<td>E_Transaction_Payer_Status_Change_Not_Allowed</td>
<td>The new payee status value is not permitted, based on its previous status.</td>
</tr>
<tr>
<td>0x80003018</td>
<td>E_Transaction_Payer_Payee_Must_Differ</td>
<td>The payee and payer must be different accounts.</td>
</tr>
<tr>
<td>0x80003019</td>
<td>E_Transaction_Tag_Too_Long</td>
<td>The payee and payer tags must be no more than 48 UTF8 bytes in length.</td>
</tr>
<tr>
<td>0x8000301A</td>
<td>E_Transaction_Memo_Too_Long</td>
<td>The memo must be no more than 48 UTF8 bytes in length.</td>
</tr>
<tr>
<td>0x80004000</td>
<td>E_Vote_Account_Not_Found</td>
<td>The vote account ID was not found.</td>
</tr>
<tr>
<td>0x80004001</td>
<td>E_Vote_Signature_Error</td>
<td>The vote’s signature is invalid.</td>
</tr>
<tr>
<td>0x80004003</td>
<td>E_Vote_Created_Utc_Out_Of_Range</td>
<td>Created UTC is too far ahead of the server’s current time.</td>
</tr>
<tr>
<td>0x80004004</td>
<td>E_Vote_Created_Utc_Is_Readonly</td>
<td>Created UTC is read-only.</td>
</tr>
<tr>
<td>0x80004005</td>
<td>E_Vote_Updated_Utc_Out_Of_Range</td>
<td>Updated UTC is too far ahead of the server’s current time.</td>
</tr>
<tr>
<td>0x80004006</td>
<td>E_Vote_Updated_Utc_Is_Old</td>
<td>The vote Updated UTC is out-dated. A newer copy exists.</td>
</tr>
</tbody>
</table>
<h1><a id=""Credits_and_Acknowledgements_1012""></a>Credits and Acknowledgements</h1>
<p>Civil Money would not exist without the work of these people.</p>
<ul>
<li>Ion Stoica, Robert Morris, David Karger, Frans Kaashoek, and Hari Balakrishnan for developing the <a href=""https://en.wikipedia.org/wiki/Chord_(peer-to-peer)"">Chord <abbr title=""Distributed Hash Table"">DHT</abbr></a> model.</li>
<li><a href=""http://bradconte.com"">Brad Conte</a> for his excellent series of succinct crypto library “reference” implementations, which are useful for lightweight cross platform testing.</li>
<li>Nenad Vukicevic for <a href=""https://github.com/vukicevic/crunch"">Crunch</a> - An arbitrary-precision integer arithmetic library for JavaScript.</li>
<li>The good people over at <a href=""http://bridge.net"">Bridge.NET</a>, which we’ve used to rapidly prototype the JavaScript front-end.</li>
<li>The Microsoft <a href=""https://github.com/dotnet/core"">.NET Core</a> team for providing a high performance truely cross-platform .NET development environment.</li>
</ul>
<h1><a id=""License_1023""></a>License</h1>
<p>Civil Money is free and unencumbered software released into the public domain (<a href=""http://unlicense.org"">unlicense.org</a>), unless otherwise denoted in the source file.</p>

";
        }
    }
}