About the CM project
===========

This library contains all common/shared objects and business logic for use by both the CM.Javascript or CM.Server projects.

Because the CM.Javascript project uses Bridge.NET for compilation, code needs to be *linked* as opposed to referenced as a binary. As such, there are a few `#if JAVASCRIPT .. #endif` compiler regions to be aware of as well, simply because the javascript lexical compiler is not fully compatible/bug-free with all features of the .NET runtime.