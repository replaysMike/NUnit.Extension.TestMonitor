using System.Text.Json.Serialization;

namespace NUnit.Extension.TestMonitor
{
    public class Configuration
    {
        /// <summary>
        /// The events to emit
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EventEmitTypes EventEmitType { get; set; } = EventEmitTypes.NamedPipes;

        /// <summary>
        /// The format of the event data
        /// </summary>
        public EventFormatTypes EventFormat { get; set; } = EventFormatTypes.Json;

        /// <summary>
        /// The output stream when outputting <see cref="EventEmitType"/> = <see cref="EventEmitTypes.StdOut"/>
        /// </summary>
        public EventOutputStreams EventOutputStream { get; set; } = EventOutputStreams.Trace;

        /// <summary>
        /// The path to write event logs to
        /// </summary>
        public string EventsLogFile { get; set; }
    }
}
