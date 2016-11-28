About the CM.Server project
===========

This is the crux of a Civil Money server. It hosts the Distributed Hash Table (DHT) client, an HTTPS/TLS web server, a SynchronisationManager, and a LinearHashTable data storage system.
 
Optionally it also spins up an AuthoritiveDomainReporter and handles API functions. There are no security implications in enabling the functions on non-authoritative servers however only the *.civil.money seeds are currently intended to use these.

The project when built in DEBUG mode can instantiate multiple servers all at once. In RELEASE mode it will use the live network.

