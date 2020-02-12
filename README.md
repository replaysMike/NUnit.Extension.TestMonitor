# NUnit.Extension.TestMonitor
Provides realtime test monitoring functionality to NUnit

# Installation
Install TestMonitor from the Package Manager Console:
```
PM> Install-Package NUnit.Extension.TestMonitor
```
To enable the extension, you must add a file (or edit if it exists already) called `.addins` and place it in the build output folder of your test project. It should contain text with the following content:

```
NUnit.Extension.TestMonitor.dll                   # Include the TestMonitor nUnit extension
```
This is required by NUnit in order for it to find extensions.

# Usage

This plugin offers an IPC/Named Pipes interface so that you may display or log tests in real-time.

Detailed examples will follow once this package is published.

