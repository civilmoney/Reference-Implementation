
API version 1.0
======

The Civil Money API is in its first iteration and outlines the high level messaging protocol between clients and DHT peers. 

See the [README.md](README.md) for a more general overview of the framework's design.


How it works
------------

### Peers form a Distributed Hash Table (DHT)

If you're unfamiliar with what a DHT is, see here: [https://en.wikipedia.org/wiki/Distributed_hash_table](https://en.wikipedia.org/wiki/Distributed_hash_table)

We use the *Consistent Hashing* model also known as the [Chord DHT](https://en.wikipedia.org/wiki/Chord_(peer-to-peer)). 

Each peer's ID is the first 8 bytes of `MD5("ip-address")`. MD5 is chosen solely for its distribution properties.

We'll call this hashing function `DHT_ID()`.

Every peer holds a connection to a `predecessor` and `successor`. Thus, the network is basically a massive circular daisy chain. In-memory lookup tables assist in more efficiently resolving the responsible peer for any given `DHT_ID` by reducing the number of hops.

Each DHT peer is responsible for numerical `DHT_IDs` landing in between itself and its `successor`. 

Account records are stored on the network at:
```
Server #1 = DHT_ID("copy1" + LOWER(AccountID))
Server #2 = DHT_ID("copy2" + LOWER(AccountID))
Server #3 = DHT_ID("copy3" + LOWER(AccountID))
Server #4 = DHT_ID("copy4" + LOWER(AccountID))
Server #5 = DHT_ID("copy5" + LOWER(AccountID))
```

Each of those servers will _independently_ corroborate any `PUT` action with its own DHT_ID resolution. When enough servers meeting the constant `MINIMUM-COPIES-REQUIRED` are corroborated, only then can an account, transaction or vote record be committed.

*[DHT]: Distributed Hash Table

### All client and inter-peer communication is performed over HTTP Secure WebSockets (WSS.)

This is not for data secrecy (there is none) but rather for mitigating network based interferences and also satisfying SSL requirements for mobile platforms. No secret or sensitive data ever exists on the Civil Money network. 

A throw-away wild-card SSL certificate is deployed with the server application, and a DNS server has been created which will echo sub-domains that look like IPs, such that:

```
nslookup 127-0-0-1.untrusted-server.com = 127.0.0.1
```

This allows the Civil Money server to be hosted by anybody, and all web browsers will pass basic SSL certificate domain name checks. We don't care that the server may be malicious, they are only one of multiple that we're going to corroborate its replies against, and we have no secret data to hide from a malicious server in the event that we decide to try using it for object storage.

The reference client implementation never displays or downloads content from DHT peers. The only communication going on is a stream of plain text over a single web socket. Any printable data is HTML encoded.


### Messaging

All message and object schema formats consist of a UTF-8 plain text dictionary.

#### Request payload
The request message payload format is:

```
CMD [Action] [NOnce] [Command specific args]
KEY: Value
KEY: Value
...
END [NOnce]
```

#### Response payload
The response message payload format is:

```
RES 0x[hexadecimal CMResult Code] [NOnce] [Command specific args]
KEY: Value
KEY: Value
...
END [NOnce]
```

The `NOnce` can be any random string consisting of letters or numbers and should be reasonably unique for each request. A truncated GUID is used in the reference implementation.

### Actions


The following Actions are defined.

| Action | Description     |
|-------|---------|
|PING |Retrieves status information about a DHT peer and optionally notifies the peer about your own end-point, if you are participating as a DHT peer yourself. |
|FIND | Locates the responsible DHT peer for the specified DHT_ID. |
|GET | Gets an object at a specified path. |
|PUT | Tentatively puts an object at the specified path and receives a commit token if validation is successful. |
|QUERY-COMMIT | Queries the current status of an object's path, which may be in the process of being `PUT`. |
|COMMIT|Requests that the peer *attempt* to commit the object associated with a commit token. DHT peers will independently `QUERY-COMMIT` elsewhere on the network to make sure that enough other peers are also in the process of committing the same object.|
|LIST | Lists objects under the specified path. |
|SUBSCRIBE | Notifies the peer that it should send `NOTIFY` packets on the established WebSocket connection, about any new updates regarding a specified account. |
|NOTIFY | Sent by DHT peers to notify a subscribed connection of account changes.|
|SYNC | Sent periodically by DHT peers to inform other responsible peers on the network about the current state of an account. |

#### The PING Action

The `PING` command serves multiple functions simultaneously.

- Determines whether an end-point is alive.
- Acts as a way for you to find your own external network IP.
- Provides insight as to the health of the peer. A peer without a Successor or Predecessor is broken and should not be used.
- Provides a list of other hints regarding other *potentially* valid peers on the network.
- Optionally informs the DHT peer that you yourself are a peer and are trying to participate in the network.

##### Example
```
Request:
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
```

##### PING Request Values

| Name | Key | Value     |
|-------|---------|---------|
|End-point|EP *(optional)*|When specified, the target peer will evaluate your DHT_ID and if applicable, attempt to connect and modify its current Predecessor. |

##### PING Response Values
| Name | Key | Value     |
|-------|---------|---------|
|Your IP|YOUR-IP|Informs the caller of their public IP address. Peers should maintain a list of potential external IPs and update their own DHT_ID only when confirmed by a number of other pinged peers.|
|My IP|MY-IP|Informs the caller of what the peer *thinks* its current external IP address is. This is useful for diagnosing peers that are stuck behind NAT. A DHT peer is not considered valid until its `MY-IP` matches that of the outgoing connection.|
|Successor|SUCC|Informs the caller of the peer's currently determined Successor.|
|Predecessor|PRED|Informs the caller of the peer's currently determined Predecessor.|
|Seen List|SEEN|Informs the caller about other *successfully connecting* peers on the network.|


##### PING CMResult Codes
Ping must always return `CMResult.S_OK`.

#### The FIND Action

The `FIND` action locates the responsible peer for a given `DHT_ID`. If the value does not fall within the peer's own `DHT_ID` and that of its `Successor`, the request is re-routed to the best known and working potential peer that can handle the specified DHT_ID.

##### Example
```
Request:
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
```


##### FIND Request Values

| Name | Key | Value     |
|-------|---------|---------|
|DHT ID|DHT-ID|The ID to locate on the network.|
|Hops So Far|HOPS|A comma delimited list of end-points that have serviced the request. Forwarding peers must add themselves to the end of the HOPS list. If peers find their own end-point in the HOPS list or if `MAX-HOPS` has been reached, they must terminate the search.|
|Maximum Hop Count|MAX-HOPS|Sets the desired maximum number of peers to query before giving up. The default value is 30.|

##### FIND Response Values
| Name | Key | Value     |
|-------|---------|---------|
|Hop List|HOPS|A comma delimited list of end-points that have serviced the request.|
|Responsible Peer|PEER|The end-point of the DHT peer currently responsible for the requested `DHT_ID`.|



##### FIND CMResult Codes
- S_OK
- E_Max_Hops_Reached
- E_Invalid_Request
- E_Not_Enough_Peers

#### The GET Action
All objects are stored in a deterministic folder or path on a DHT peer. The `GET` action attempts to retrieve the latest copy of any given Account, Transaction or Vote.

##### Example
```
Request:
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
ATTR-PUSH: "My notification ""sink""",https://something.com/endpoint
PRIKEY: 0,9GO27EHf27eiuW+bd+6genl7h+8+ByNlWgqFG4p2vio=,CLru67gKQDyqetnwmtuX0IgjfE7nQjYxkSrvVJnqmcvHK7tpaMVNucrS2LKc0JV4LKGlQB0MXhR6fYRzNr5MSZqY3DkzYKF5H/3pdFQCqKS+2wagXFCA521we4bULtB5zIjK/4xTYltKfm08vMnJr26vxiEyBFUqXgjpDr5IHX8x3RT33hRvtYbMC7Z9JNFq
PUBKEY: 2016-09-16T17:18:45,1N8SIc03kFcY4EB9s3jkBshSFL5zsaRiGvOVAy/0whBtlJ5S4ReL0WpydJkJ0TqK4iU/CfDThLVtbEIteJDLE0BXI+pbMzeOhtLjPZBDye83q2GeQq9d2sfpmkI3uqW2D+NCo+nC//CMtaE9JqmmpTnKKEw4I3/oXBrtZj7x7ss=,
SIG: Qju9v3SDEEJ2/6/3whJ9MqlNomU36SCfU9Vr7ukCHAD9kPgQxUsSbLEcZ9gQpn4Bgzvb7IaRe183RpSmAWNUQpe3aSofgbEhzkdAuiE5EKLJu1KJ88vNy25j0By6xtorsd30b2yHEuyHs4m9Kz9mBxNdZU0h5/nMvtDz4qXitEU=
CALC-LAST-TRANS: 2016-09-27T12:01:23
CALC-DEBITS: 1.000234
CALC-CREDITS: 2.000000
CALC-REP: 50.0
CAN-VOTE: 0
END 6ec4a
```

##### GET Request Values

There is no key/value request body for a `GET` request. The object path is specified as the argument of the request `CMD` line.

For the `Account` object path, a query parameter `calculations-date` is permitted, which will instruct the peer to produce an **uncorroborated** summary of the account's balance and reputation. These values begin with `CALC-*` in the response body, and must be omitted during any RSA signing checks.

##### GET Response Values

The `GET` response always consists of the object's raw text key/value dictionary.

For `Account` objects where a `calculations-date` has been included, the following Account `CALC-` attributes are currently defined.

| Name | Key | Value     |
|-------|---------|---------|
|Last Transaction|CALC-LAST-TRANS|The time stamp of the last transaction `MAX(PYR-UTC, PYE-UTC)`.|
|Recent Credits|CALC-CREDITS|The sum of all depreciated credit transactions.|
|Recent Debits|CALC-DEBITS|The sum of all depreciated debit transactions.|
|Recent Reputation|CALC-REP|The Recent Reputation credit score is defined as `MIN(1, (BASIC-YEARLY-ALLOWANCE + DEPRECIATED-CREDITS) / ( DEPRECIATED-DEBITS + BASIC-YEARLY-ALLOWANCE * 2 )) * 100`. |
|Can Vote|CAN-VOTE|`1` (true) if the account has at least 1 transaction every month for the last 12 months with multiple parties, otherwise `0` (false.) This is a *hint* for the client regarding an account's voting eligibility. |


##### GET CMResult Codes
- S_OK
- E_Invalid_Request
- E_Item_Not_Found
- E_Invalid_Object_Path
- E_Account_ID_Invalid




#### The PUT Action

The `PUT` action informs a DHT peer of your intention to commit a new or updated copy of an object. Object-specific update rules are validated and a `Commit Token` is included in the response if the object appears to be valid.

##### Example

````
Request:
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
````

##### PUT Request Values

The `PUT` request body consist of an object key/value dictionary. The `CMD` command argument is the object's path.

##### PUT Response Values

There is no key/value response body in a `PUT` request. The `RES` response line contains a GUID commit token as its command specific argument.

##### PUT CMResult Codes

- S_OK
- E_Unknown_API_Version
- E_Account_ID_Invalid
- E_Object_Superseded
- Type-specific error codes



#### The QUERY-COMMIT Action

The `QUERY-COMMIT` action allows a DHT peer or client to confirm whether or not a peer is about to commit or has already commited an object with the correct `Updated UTC` time stamp.

##### Example

```
Request:
CMD QUERY-COMMIT 4e93c TRANS/2016-09-24T20:13:30 test1 test2
END 4e93c

Response:
RES 0x0 4e93c 2016-09-24T20:13:30
END 4e93c
```

##### QUERY-COMMIT Request Values

There is no key/value request body for a `QUERY-COMMIT` request. The request `CMD` line contains the object `Path` about to be saved. This allows all DHT peers to query the commit status of another responsible peers, without knowing what the object's commit token is.

##### QUERY-COMMIT Response Values

There is no key/value response body in a `QUERY-COMMIT` request. The `RES` response line contains the object's `Updated UTC` time stamp. The object *may or may not* be already committed.

DHT peers must return the highest `Updated UTC` either on record or pending commit.

##### QUERY-COMMIT CMResult Codes

- S_OK
- E_Item_Not_Found


#### The COMMIT Action

The `COMMIT` action instructs a DHT peer to independently corroborate an object's status on the network and, if successful, commit the record to permanent storage and indexing.

##### Example
```
Request:
CMD COMMIT 506ab dfb67b3d-55fe-4e41-8dc8-aeb51dbb8253
END 506ab

Response:
RES 0x0 506ab
END 506ab
```

##### COMMIT Request Values

There is no key/value request body for a `COMMIT` request. The request `CMD` line contains the object `Commit Token` to be committed.

##### COMMIT Response Values

There is no key/value response body in a `COMMIT` request. 

##### COMMIT CMResult Codes

- S_OK
- E_Item_Not_Found
- E_Not_Enough_Peers


#### The LIST Action

The `LIST` action provides basic lookup capability for object paths and includes basic sorting and pagination functionality.

##### Example
```
Request:
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
```

##### LIST Request Values

| Name | Key | Value     |
|-------|---------|---------|
|API Version|VER|1|
|Start At Index|START|The starting record index, used for pagination.|
|Max Records|MAX|The maximum number of records to return. The default value is 1000 when not specified.|
|Updated Utc From Inclusive|UTC-FROM|The item `Updated UTC` date to begin listing from.|
|Updated Utc To Exclusive|UTC-TO|The item `Updated UTC` date to stop before.|
|Sorting|SORT|Path-specific field sorting e.g. `PYR-ID ASC`.|

The following path sorting fields are defined for both `ASC` and `DESC` directions.

| Path | Field |
|-------|---------|
|ACCNT/|UTC|
|ACCNT/|UPD-UTC|
|ACCNT/|ID|
|ACCNT/`ID`/VOTES/|UTC|
|ACCNT/`ID`/VOTES/|UPD-UTC|
|ACCNT/`ID`/TRANS/|UTC|
|ACCNT/`ID`/TRANS/|UPD-UTC|
|ACCNT/`ID`/TRANS/|PYR-ID|
|ACCNT/`ID`/TRANS/|PYE-ID|
|ACCNT/`ID`/TRANS/|AMNT|
|VOTES/`PropositionID`/|UTC|
|VOTES/`PropositionID`/|UPD-UTC|
|VOTES/`PropositionID`/|VTR-ID|
|TRANS/|UTC|
|TRANS/|UPD-UTC|
|TRANS/|PYR-ID|
|TRANS/|PYE-ID|
|TRANS/|AMNT|
|REGIONS/`Region`/TRANS/|UTC|
|REGIONS/`Region`/TRANS/|UPD-UTC|
|REGIONS/`Region`/TRANS/|PYR-ID|
|REGIONS/`Region`/TRANS/|PYE-ID|
|REGIONS/`Region`/TRANS/|AMNT|


##### LIST Response Values

| Name | Key | Value     |
|-------|---------|---------|
|API Version|VER|1|
|Start Index|START|The requested starting index.|
|Count|COUNT|The number of records included in this paginated response.|
|Total Record Count|TOTAL|The total number of records available for pagination.|
|Item|ITEM *(one per result)*|Type-specific object indexes.|

The following one-liner Object Indexes are defined.

| Type | Index format |
|-------|---------|
|Account|`ID`|
|Vote|`Proposition " " Voter " " Value " " Created-UTC " " Updated-UTC`|
|Transaction|`Created-UTC " " Payee " " Payer " " Amount " " Updated-UTC " " Payee-Status (Byte) " " Payer-Status (Byte) " " Payee-Region " " Payer-Region`|

*HINT:* The beginning of a Transaction index is just the `Transaction ID` (Date + Payee + Payer.)


##### LIST CMResult Codes
- S_OK
- E_Invalid_Object_Path
- E_Invalid_Request


#### The SUBSCRIBE Action

The `SUBSCRIBE` action informs a DHT peer that you would like to be receive "push" notifications for an account. This action should only be called on currently responsible peers for the account, and notifications received through a `NOTIFY` should not be trusted alone. Clients must look for multiple notifications by multiple subscribed peers before alerting a user.

Subscriptions are removed by DHT peers when the underlying WebSocket connection is closed or otherwise broken.

##### Example

```
Request:
CMD SUBSCRIBE 68343 test2
END 68343

Response:
RES 0x0 68343
END 68343
```

##### SUBSCRIBE Request Values

There is no key/value request body for a `SUBSCRIBE` request. The request `CMD` line contains the account `ID` to receive notifications for.

##### SUBSCRIBE Response Values

There is no key/value response body in a `SUBSCRIBE` request.

##### SUBSCRIBE CMResult Codes
- S_OK


#### The NOTIFY Action

DHT peers send a `NOTIFY` to any open client WebSocket connections that have requested push notifications for an account ID.

##### Example
```
Push Notification:
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
```

##### NOTIFY Request Values

The `NOTIFY` action is an object key/value body. The `CMD` request argument is the object's path, which can be used to uniquely identify the object as well its type.

##### NOTIFY Response Values

Clients should return `S_OK` to all `NOTIFY` messages.

##### NOTIFY CMResult Codes
- S_OK


#### The SYNC Action
The `SYNC` action must be performed periodically by all DHT peers in order to keep the network sufficiently populated with multiple copies of every account.


##### Example
```
Request:
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
```

##### SYNC Request Values

| Name | Key | Value     |
|-------|---------|---------|
|API Version|VER|1|
|My End-point|EP|The calling DHT peer's service end-point. This informs the destination of the endpoint to use in order to potentially retrieve the item if it can't be found elsewhere on the network. |
|Updated UTC|UPD-UTC|The account record's current `Updated UTC` ISO-8601 value. If the destination determines that its is obsolete, it will query the network for a *corroborated* copy of the current account. |
|Transactions Hash|TRANS-HASH|An SHA256 hash of all ISO-8601 `Updated UTC` Transaction values for the account, hashed in ascending order. |
|Votes Hash|VOTES-HASH|An SHA256 hash of all ISO-8601 `Updated UTC` Vote values for the account, hashed in ascending order. |


##### SYNC Response Values

There is no key/value response body in a `SYNC` request.

##### SYNC CMResult Codes
- S_OK


#### Object Storage Paths

DHT peers must be able to GET, PUT and LIST items stored in the following deterministic locations.


| Type |Action| Path    | 
|------|---------|---------|
|Account|GET/PUT|ACCNT/`ID`|
|Account|LIST|ACCNT/|
|Transaction|GET/PUT|TRANS/`yyyy-MM-ddTHH:mm:ss payee payer`|
|Transaction|LIST|ACCNT/`Payer|Payee ID`/TRANS/|
|Transaction|LIST|REGION/`Region Code`/TRANS/|
|Vote|GET/PUT|VOTES/`Proposition ID`/`Voter ID`|
|Vote|LIST|VOTES/`Proposition ID`/|
|Vote|LIST|ACCNT/`Voter ID`/VOTES/|


DHT peers should store versioned copies of records in a format and storage scheme suitable for 
handling millions of rows.



### Object Types

The following object types are defined.

| Action | Description     |
|-------|---------|
|Account | Describes a user account. |
|Transaction | Describes a digitally signed money transfer between accounts. |
|Vote| Describes a user vote for a proposal raised by the Civil Money steering group.|



#### The Account object
The Account object schema is:

| Field | Key     | Value
|-------|---------|-------|
|API Version|VER|1|
|Account ID|ID|string, max 48 utf-8 bytes|
|Created UTC|UTC|ISO-8601 UTC date string|
|Updated UTC|UPD-UTC|ISO-8601 UTC date string|
|Region|REG|ISO 3166-2 subdivision code|
|Private Key|PRIKEY|PrivateKeySchemeID\* "," Salt base64 "," Encrypted base64|
|Public Key *(multiple allowed)*|PUBKEY|ISO-8601 Effective Date "," Key base64 "," *Modification Signature*\*\* base64|
|Attributes |ATTR-\*|Extensible account attributes\*\*\*|
|Signature | SIG | base64 RSA signature of all values

\* Currently recognised PrivateKeySchemeIDs. **Encryption and decryption always take place on the client**.

| ID  | Scheme |
|-------|---------|
|0|Encrypted using AES CBC mode and PKCS7 padding, with 16 byte IV and 32 byte Key. The IV and Key are derived using RFC2898 HMACSHA1 with 10,000 iterations.|


\*\* The modification signature is necessary only when changing the private key after initial account creation. Clients and peers must select the public key with a suitable *effective date* according to an objects `UPD-UTC` timestamp.

\*\*\* Currently recognised account attributes:

| Name  | Key     | Values | Purpose |
|-------|---------|--------|---------|
|Governing Authority|ATTR-GOV|base64 signature of values `UTC + REG`|A secret private key held by the Civil Money steering group is used to generate a governing authority key, which designates a particular Civil Money account as the recipient of Inverse-Taxation income for a region. These will be assigned to governments on an as-requested basis after a vetting process (social engineering is an obvious challenge/attack vector here.) |
|Income Eligibility|ATTR-ELIG|`WORK` Working, `HLTH` Health Problem, `UNEMP` Unemployed, `AGED` Retired|Provides people with a basic hint about a customer's personal circumstance. A low credit score might be because they're disabled or what have you. This just one of multiple considerations. The value does not affect credit rating so there is no incentive to lie.|
|Skill or Service|ATTR-SKILL *(multiple allowed)*|`SkillLevel* "," Description`|Provides more context about a person's potential contribution to society.|
|Push Notification|ATTR-PUSH *(multiple allowed)*| CSV of `Label,HTTP endpoint`|DHT peers post a notification to this end point any time an object is created or updated, that was **not** through a `SYNC` operation.|

\* Skill levels:

| Value  | Level  |
|-------|---------|
| 0 | Amateur      |
| 1 | Qualified    |
| 2 | Experienced  |
| 3 | Certified    |


##### Example

```
ID: test1
VER: 1
REG: CA-NS
UTC: 2016-09-16T17:18:45
UPD-UTC: 2016-09-16T17:18:45
ATTR-ELIG: UNEMP
ATTR-SKILL: 2,Roofer
ATTR-SKILL: 0,Fiddler
ATTR-PUSH: "My notification ""sink""",https://something.com/endpoint
PRIKEY: 0,9GO27EHf27eiuW+bd+6genl7h+8+ByNlWgqFG4p2vio=,CLru67gKQDyqetnwmtuX0IgjfE7nQjYxkSrvVJnqmcvHK7tpaMVNucrS2LKc0JV4LKGlQB0MXhR6fYRzNr5MSZqY3DkzYKF5H/3pdFQCqKS+2wagXFCA521we4bULtB5zIjK/4xTYltKfm08vMnJr26vxiEyBFUqXgjpDr5IHX8x3RT33hRvtYbMC7Z9JNFq
PUBKEY: 2016-09-16T17:18:45,1N8SIc03kFcY4EB9s3jkBshSFL5zsaRiGvOVAy/0whBtlJ5S4ReL0WpydJkJ0TqK4iU/CfDThLVtbEIteJDLE0BXI+pbMzeOhtLjPZBDye83q2GeQq9d2sfpmkI3uqW2D+NCo+nC//CMtaE9JqmmpTnKKEw4I3/oXBrtZj7x7ss=,
SIG: Qju9v3SDEEJ2/6/3whJ9MqlNomU36SCfU9Vr7ukCHAD9kPgQxUsSbLEcZ9gQpn4Bgzvb7IaRe183RpSmAWNUQpe3aSofgbEhzkdAuiE5EKLJu1KJ88vNy25j0By6xtorsd30b2yHEuyHs4m9Kz9mBxNdZU0h5/nMvtDz4qXitEU=
```

##### Account rules

1. `Created UTC` and `ID` are read-only upon creation.
2. `Updated UTC` must equal `Created UTC` during creation.
3. `Public Keys` are read-only when appended and cannot be removed. 
4. Clients must always sign objects using the newest `Public Key`. 
5. For signature validation, clients must iterate through the account's public keys in order to find the correct key based on the key `Effective Date` and the data's `Updated UTC`.
6. When a new Public key is append is must include the `Modification Signature` component. The signature is an RSA of the **new** `Effective Date` utf-8 string, and raw `Key` byes, using the **previous** public/private key pair. 
7. Peers must validate all public keys to make sure that each entry successfully validates the key after it.



#### The Transaction object
The Transaction object schema is:

| Field | Key     | Value
|-------|---------|-------|
|API Version|VER|1|
|Created UTC|UTC|ISO-8601 UTC date string|
|Amount|AMNT|The amount of the transaction up to 6 decimal places. Decimal separator is '.'|
|Memo|MEMO|A reader-friendly plain-text UTF-8 description or note about the transaction. Maximum allowed length is 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all memos.|
|Payee ID|PYE-ID|The account ID of the recipient/payee|
|Payee Region|PYE-REG|The Payee's ISO 3166-2 subdivision code at the time of transaction.|
|Payee Tag|PYE-TAG|An optional electronic tag which is typically defined by the payee during acceptance. Up to 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all tags.|
|Payee Updated UTC|PYE-UTC|ISO-8601 UTC date string|
|Payee Status|PYE-STAT|NotSet, Accept, Decline, Refund|
|Payee Signature|PYE-SIG|base64 RSA signature of `Payee Signing Data`\*|
|Payer ID|PYR-ID|The account ID of the sender/payer|
|Payer Region|PYR-REG|The Payer's ISO 3166-2 subdivision code at the time of transaction.|
|Payer Tag|PYR-TAG|An optional electronic tag which is typically defined by the payer during creation. Up to 48 UTF-8 bytes. Implementations which use HTML must HTML-encode all tags.|
|Payer Updated UTC|PYR-UTC|ISO-8601 UTC date string|
|Payer Status|PYR-STAT|NotSet, Accept, Dispute, Cancel|
|Payer Signature|PYR-SIG|base64 RSA signature of `Payeer Signing Data`\**|

\* `Payee Signing Data` is defined as the following values:

- ISO-8601 Created UTC
- Amount (with 6 decimals)
- Payee ID 
- Payer ID
- Memo
- Payee ISO-8601 Updated UTC
- Payee Tag
- Payee Status (enum byte value)
- Payee Region

\** `Payer Signing Data` is defined as the following values:
- ISO-8601 Created UTC
- Amount (with 6 decimals)
- Payee ID
- Payer ID
- Memo
- Payer ISO-8601 Updated UTC
- Payer Tag
- Payer Status (enum byte value)
- Payer Region





##### Example
```
VER: 1
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
```

##### Transaction rules

1. `Created UTC`, `Amount`, `Payer ID`, `Payee ID` and `Payer Region` are required and read-only upon creation.

2. `Memo` is optional, but read-only upon creation, and must be no longer 255 UTF8 bytes in length.

3. Payer/Payee tags must be no longer than 48 UTF-8 bytes in length.

4. `Payer Status` must be `Accept` during creation.

5. The payer's signature and updated utc is required in order to create a new transaction. This means that only Payers are allow to create a transaction.\
\
  This removes the potential for spamming of unwanted requests for payment. Payees may 
  instead request payment through the use of payment links or QR codes for minimal 
  friction during e-commerce or Point of Sale situations.\
\
  Due to inevitability of malicious payment links intended to harvest pass phrases,
  a reminder notice on the official https://civil.money app educates people about 
  dangers of following any payment links, and to pay special attention to the absence 
  of address bars or the reminder itself during payment or Point of Sale.
 
6. Payer/Payee Updated UTC must be greater than or equal to Created UTC.
 
7. Transaction amount must be 6 decimal places. Therefore, the minimum transaction amount is 0.000001.
 
8. A linear demurrage on the `Amount` begins 12 months after `Created UTC`, over a following 12 month period. \
   \
   The function `DEPRECIATE()` is defined as:
   ```
   ROUND(MIN(1, MAX(0, 1 - ( (DAYS-SINCE-CREATION - 365) / 365 ))) * AMOUNT, 6)
   ```
   \
  This means that only your last two years of activity really matter, and your balance and credit score automatically
  restores itself over time if you hit a financially rough spot.\
  \
  Also, people cannot hoard vast sums of money, and are encouraged to spend
  earnings within their first 12 months, stimulating the economy.\
  \
  People who ping-pong money between accounts to get around the demurrage can be identified and should be frowned upon.

9. The unique identifier for any transaction is the following utf-8 string:\
\
  `Created Utc + " " + PayeeID + " " + PayerID `
\
\
  With this ID scheme, Transaction IDs can be sorted naturally 
  based on their creation date, collision and exhaustion are impossible,
  and there is an implicit maximum of 1 transaction per second
  per payer/payee which has the added benefit of blocking non-productive High Frequency 
  Trading.

10. When payers issue a Dispute they get their money back, but the payee 
  retains their money also, unless they choose to refund amicably. \
\
  This only works because non-refunded disputed transactions reflect 
  badly on the seller as well as the buyer. There is disincentive
  on both sides to abuse it.

11. The following payer/payee transaction states are allowed:
 

|PayerStatus|Byte Value (during signing)| Restrictions|
|-------|---------|---------|
|NotSet|0|Never Allowed|
|Accept|1|Required during creation|
|Dispute|2|Previous `PayerStatus` must be `Accept`|
|Cancel|3|Previous `PayerStatus` must be `Accept` and the current `PayeeStatus` must be in `NotSet`|


|PayeeStatus|Byte Value (during signing)|Note|
|-------|---------|---------|
|NotSet|0|Required during creation.|
|Decline|1|Previous `PayeeStatus` must be `NotSet`|
|Accept|2|Previous `PayeeStatus` must be `NotSet` or `Decline`|
|Refund|3|Previous `PayeeStatus` must be `Accept`|





#### The Vote object
The Vote object schema is:

| Field | Key     | Value
|-------|---------|-------|
|API Version|VER|1|
|Created UTC|UTC|ISO-8601 UTC date string|
|Updated UTC|UPD-UTC|ISO-8601 UTC date string|
|Voter ID|VTR-ID|The account ID of the voter|
|Proposition ID|PROP|The proposition ID for the vote.|
|Value|VOTE|The boolean value of the vote. `1` or `0`\*.|
|Signature|SIG|base64 RSA signature of all UTF-8 encoded values in the Vote object.|

\* By design propositions must be put to a vote in the form of a binary question (for or against.) This is to take potentially unfair "ranked order" voting procedures off the table.

##### Example
```
VER: 1
UTC: 2016-09-16T18:26:28
UPD-UTC: 2016-09-16T18:26:28
VOTE: 1
PROP: 1
VTR-ID: test1
SIG: X3Vx9syas8LNqEURDemnUYGhkd451Dlkl/kJDXxZv37xcYKF6IdaD0wGEfhA/KMyo7XkrEfmhDui7pTrQ9KZbv+XCUKsjz9LNNXHikNDP2OHPlBIsjbhvAB53kb0nESeWMkmIJCXO2lQJHnOhH6RaVXVXFdIhnkuWJI+J0yKzd0=
```

### CMResult Status Codes

Status codes are 32-bit hexadecimal integers. Negative signed integers are errors, positive integers are success. This scheme is similar to a Microsoft Windows HRESULT. The following status codes are defined. 

| Value | Name | Description|
|-------|------|-------|
|0x0|S_OK|OK|
|0x1|S_False|False|
|0x2|S_Item_Transient|At least 1 copy of the item was found, but the minimum number of copies required are not met.|
|0x80000000|E_General_Failure|General failure.|
|0x80000001|E_Not_Connected|The web socket is not currently connected.|
|0x80000002|E_Timeout_Waiting_On_Reply|Time-out waiting on a reply.|
|0x80000003|E_Invalid_Action|Invalid action.|
|0x80000004|E_Item_Not_Found|The item was not found.|
|0x80000005|E_Invalid_Request|Invalid request.|
|0x80000006|E_Not_Enough_Peers|There were not enough available peers to corroborate the request.|
|0x80000007|E_Invalid_Object_Path|The requested GET or PUT path is not valid for the item provided.|
|0x80000008|E_Object_Superseded|A newer version of this item is already being committed.|
|0x80000009|E_Max_Hops_Reached|The maximum number of DHT peer hops have been reached.|
|0x8000000A|E_Connect_Attempt_Timeout|Unable to connect to any servers within a reasonable time-out period.|
|0x8000000B|E_Invalid_Search_Date|Invalid search date range.|
|0x8000000C|E_Unknown_API_Version|Unknown API version.|
|0x8000000D|E_Operation_Cancelled|The operation has been cancelled.|
|0x80001000|E_Crypto_Invalid_Password|The specified password didn't work for decryption.|
|0x80001001|E_Crypto_Unrecognized_SchemeID|The account private key scheme ID is not recognised.|
|0x80001002|E_Crypto_Rfc2898_General_Failure|Unable to obtain an encryption key using Rfc2898.|
|0x80001003|E_Crypto_RSA_Signing_General_Failure|Unable to sign the data using RSA.|
|0x80001004|E_Crypto_RSA_Verify_General_Failure|Unable to verify the data using RSA.|
|0x80001005|E_Crypto_RSA_Key_Gen_Failure|Unable to generate an RSA key.|
|0x80002000|E_Account_Missing_Public_Key|No valid public key was found on the account for the specified time.|
|0x80002001|E_Account_ID_Invalid|The account ID is invalid.|
|0x80002002|E_Account_IDs_Are_Readonly|Account IDs are read-only.|
|0x80002003|E_Account_Created_Utc_Out_Of_Range|Created UTC is too far ahead of the server's current time.|
|0x80002004|E_Account_Created_Utc_Is_Readonly|Created UTC is read-only.|
|0x80002005|E_Account_Updated_Utc_Out_Of_Range|Updated UTC is too far ahead of the server's current time.|
|0x80002006|E_Account_Updated_Utc_Is_Old|The account Updated UTC is out-dated. A newer copy exists.|
|0x80002007|E_Account_Too_Few_Public_Keys|The number of public keys specified are less than the existing record's.|
|0x80002008|E_Account_Cant_Corroborate|Unable to corroborate account information with the network.|
|0x80002009|E_Account_Cant_Corroborate_Public_Keys|Unable to corroborate account information with the network. The network's copy has too fewer keys than the record provided.|
|0x8000200A|E_Account_Invalid_New_Public_Key_Date|The newest public key entry must equal the account's Updated UTC when adding new keys.|
|0x8000200B|E_Account_Public_Key_Mismatch|One or more public keys do not match the existing account.|
|0x8000200C|E_Account_Public_Key_Signature_Error|One of the public keys in the account have an invalid RSA signature.|
|0x8000200D|E_Account_Signature_Error|The account RSA signature is invalid.|
|0x8000200E|E_Account_Invalid_Region|Invalid account region specified.|
|0x8000200F|E_Account_Governing_Authority_Attribute_Required|Account names that are equal to an ISO3166-2 subdivision code require a valid governing authority attribute.|
|0x80003000|E_Transaction_Payee_Not_Found|The payee could not be found on the network.|
|0x80003001|E_Transaction_Payer_Not_Found|The payer could not be found on the network.|
|0x80003002|E_Transaction_Invalid_Payee_Signature|Invalid payee signature.|
|0x80003003|E_Transaction_Invalid_Payer_Signature|Invalid payer signature.|
|0x80003004|E_Transaction_Payer_Signature_Required|The payer's signature is required.|
|0x80003005|E_Transaction_PayeeID_Required|A payee ID is required.|
|0x80003006|E_Transaction_PayerID_Required|A payer ID is required.|
|0x80003007|E_Transaction_Created_Utc_Out_Of_Range|The transaction's Created UTC time is out of range. Please check your device's clock and try again.|
|0x80003008|E_Transaction_Payee_Updated_Utc_Out_Of_Range|The payee's updated UTC time must be greater than Created UTC.|
|0x80003009|E_Transaction_Payer_Updated_Utc_Out_Of_Range|The payer's updated UTC time must be greater than Created UTC.|
|0x8000300A|E_Transaction_Amount_Is_Readonly|The transaction amount cannot be altered.|
|0x8000300B|E_Transaction_Created_Utc_Is_Readonly|The transaction created UTC cannot be altered.|
|0x8000300C|E_Transaction_Payee_Is_Readonly|The transaction payee cannot be altered.|
|0x8000300D|E_Transaction_Payer_Is_Readonly|The transaction payer cannot be altered.|
|0x8000300E|E_Transaction_Memo_Is_Readonly|The transaction memo cannot be altered.|
|0x8000300F|E_Transaction_Invalid_Amount|The transaction amount is invalid.|
|0x80003010|E_Transaction_Payee_Region_Required|A payee region is required.|
|0x80003011|E_Transaction_Payer_Region_Required|A payer region is required.|
|0x80003012|E_Transaction_Payee_Region_Is_Readonly|The payee region is read-only.|
|0x80003013|E_Transaction_Payer_Region_Is_Readonly|The payer region is read-only.|
|0x80003014|E_Transaction_Payer_Accept_Status_Required|The payer status must be set to Accept during initial creation.|
|0x80003015|E_Transaction_Payee_Status_Invalid|The payee status must not be set without the payee's signature.|
|0x80003016|E_Transaction_Payee_Status_Change_Not_Allowed|The new payee status value is not permitted, based on its previous status.|
|0x80003017|E_Transaction_Payer_Status_Change_Not_Allowed|The new payee status value is not permitted, based on its previous status.|
|0x80003018|E_Transaction_Payer_Payee_Must_Differ|The payee and payer must be different accounts.|
|0x80003019|E_Transaction_Tag_Too_Long|The payee and payer tags must be no more than 48 UTF8 bytes in length.|
|0x8000301A|E_Transaction_Memo_Too_Long|The memo must be no more than 48 UTF8 bytes in length.|
|0x80004000|E_Vote_Account_Not_Found|The vote account ID was not found.|
|0x80004001|E_Vote_Signature_Error|The vote's signature is invalid.|
|0x80004003|E_Vote_Created_Utc_Out_Of_Range|Created UTC is too far ahead of the server's current time.|
|0x80004004|E_Vote_Created_Utc_Is_Readonly|Created UTC is read-only.|
|0x80004005|E_Vote_Updated_Utc_Out_Of_Range|Updated UTC is too far ahead of the server's current time.|
|0x80004006|E_Vote_Updated_Utc_Is_Old|The vote Updated UTC is out-dated. A newer copy exists.|


Credits and Acknowledgements
=====

Civil Money would not exist without the work of these people.

- Ion Stoica, Robert Morris, David Karger, Frans Kaashoek, and Hari Balakrishnan for developing the [Chord DHT](https://en.wikipedia.org/wiki/Chord_(peer-to-peer)) model.
- [Brad Conte](http://bradconte.com) for his excellent series of succinct crypto library "reference" implementations, which are useful for lightweight cross platform testing.
- Nenad Vukicevic for [Crunch](https://github.com/vukicevic/crunch) - An arbitrary-precision integer arithmetic library for JavaScript.
- The good people over at [Bridge.NET](http://bridge.net), which we've used to rapidly prototype the JavaScript front-end. 
- The Microsoft [.NET Core](https://github.com/dotnet/core) team for providing a high performance truely cross-platform .NET development environment.

License
=======
Civil Money is free and unencumbered software released into the public domain ([unlicense.org](http://unlicense.org)), unless otherwise denoted in the source file.