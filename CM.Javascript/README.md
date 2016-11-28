About the CM.Javascript project
===========

This is a reference client implementation which can run in any reasonably modern web browser. We use web-workers for time consuming operations, and (per the spec) all communication takes place over TLS/SSL secure web sockets.

To make development fast and easy we have opted to leverage the [Bridge.NET](http://bridge.net) lexical compiler. This means we keep all of the code in one language, can reuse a lot of the stuff in the core CM.dll project, and the compiler will do all of the legwork emitting reasonably clean javascript.

The CM.Server project when built in DEBUG mode will serve up non-minified javascript and CSS for easier development.