# NUnit.Extension.TestMonitor
Provides realtime test monitoring functionality to NUnit using IPC/Named Pipes, log files, or StdOut.

# Installation

Choose which configuration you would like to use: [NUnit-Console](https://github.com/nunit/nunit-console) runner or `dotnet test`. See below for installation/configuration instructions for either way of running tests.

## Configuration

This extension is compatible with both the [NUnit-Console](https://github.com/nunit/nunit-console) runner as well as when using `dotnet test` however they must be configured differently. Keep in mind NUnit-Console runner does not currently support .Net Core testing, if you need that you should use the `dotnet test` configuration.

### NUnit-Console Configuration

If using the [NUnit-Console](https://github.com/nunit/nunit-console) runner you must place the extension inside the `addins/` folder of the installation location of NUnit-Console. [Download](https://github.com/replaysMike/NUnit.Extension.TestMonitor/releases) the latest pre-compiled release. The folder should already exist, and you should place the extension dll's in it's own folder called `NUnit.Extension.TestMonitor`. Additionally, you must tell NUnit-Console of it's existence by appending to the `nunit.bundle.addins` file with the following contents:

```
addins/NUnit.Extension.TestMonitor/NUnit.Extension.TestMonitor.dll      # Include the TestMonitor nUnit extension
```
If you wish to customize the extension with non-default options (such as enabling file based logging) you can edit the settings file which will be located at `addins/NUnit.Extension.TestMonitor/appsettings.json`.

### Dotnet Test Configuration

If using the `dotnet test` command to run your tests you should add the Nuget package to your test project. 

```
PM> Install-Package NUnit.Extension.TestMonitor
```

To customize settings for the extension, you can add an `appsettings.json` configuration file to your test project and it will be read by the nuget extension. It should contain the following section in the root of your settings file:

```json
{
  "TestMonitor": {
    // the type of events to emit, can define multiple. Valid options: NamedPipes,StdOut,LogFile
    "EventEmitType": "NamedPipes,StdOut,LogFile",
    // format of the event data. Valid options: json, xml
    "EventFormat": "json",
    // full path to save the log to
    "EventsLogFile": "C:\\logs\\TestMonitor.log",
    // for EventEmitType=StdOut, choose which stream to use. Valid options: StdOut, Trace, Debug, None
    "EventOutputStream": "StdOut"
  }
}
```

# Usage

This plugin offers an IPC/Named Pipes interface so that you may display or log tests in real-time.

Detailed examples will follow once this package is published.
