﻿using System;
using System.Text.Json.Serialization;

namespace NUnit.Extension.TestMonitor
{
    [Flags]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventEmitTypes
    {
        None = 1 << 0,
        /// <summary>
        /// Send events via IPC / Named Pipes
        /// </summary>
        NamedPipes = 1 << 1,
        /// <summary>
        /// Send events to standard output
        /// </summary>
        StdOut = 1 << 2,
        /// <summary>
        /// Send events to log file
        /// </summary>
        LogFile = 1 << 3
    }
}
