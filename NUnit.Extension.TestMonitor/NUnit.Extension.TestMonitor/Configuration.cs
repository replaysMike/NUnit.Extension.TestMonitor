﻿using System.Text.Json.Serialization;

namespace NUnit.Extension.TestMonitor
{
    public class Configuration
    {
        public const int DefaultNamedPipesConnectionTimeoutMilliseconds = 2000;

        /// <summary>
        /// Port number to send test events to
        /// </summary>
        public int Port { get;set;} = 35001;

        /// <summary>
        /// The events to emit
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventEmitTypes EventEmitType { get; set; } = EventEmitTypes.Grpc | EventEmitTypes.StdOut;

        /// <summary>
        /// The format of the event data
        /// </summary>
        public EventFormatTypes EventFormat { get; set; } = EventFormatTypes.Json;

        /// <summary>
        /// The output stream when outputting <see cref="EventEmitType"/> = <see cref="EventEmitTypes.StdOut"/>
        /// </summary>
        public EventOutputStreams EventOutputStream { get; set; } = EventOutputStreams.StdOut;

        /// <summary>
        /// The path to write event logs to
        /// </summary>
        public string EventsLogFile { get; set; } = "C:\\web\\server-logs\\testing.log";

        /// <summary>
        /// The timeout (in seconds) to wait for a Named Pipe client connection
        /// </summary>
        public int NamedPipesConnectionTimeoutMilliseconds { get; set; } = DefaultNamedPipesConnectionTimeoutMilliseconds;

        /// <summary>
        /// Specify which launchers the extension will work for.
        /// If tests are launched by other launchers, the extension will disable waiting for connections.
        /// Default: NUnit.Commander.exe
        /// </summary>
        public string SupportedRunnerExe { get; set; } = "NUnit.Commander.exe";
    }
}
