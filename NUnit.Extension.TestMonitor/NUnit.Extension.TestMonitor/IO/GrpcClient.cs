#if GRPC
using Grpc.Core;
#endif
using System;
using System.Threading.Tasks;

namespace NUnit.Extension.TestMonitor.IO
{
    /// <summary>
    /// Grpc client for sending test event messages
    /// </summary>
    internal class GrpcClient
    {
#if GRPC
        private const string GrpcServer = "127.0.0.1";
        private readonly Configuration _configuration;
        private readonly TestEventService1.TestEvent.TestEventClient _client;
        private readonly Channel _channel;

        public GrpcClient(Configuration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (_configuration.Port <= 0) throw new ExtensionException($"Port number '{_configuration.Port}' must be greater than 0.");

            _channel = new Channel(GrpcServer, _configuration.Port, ChannelCredentials.Insecure);
            _client = new TestEventService1.TestEvent.TestEventClient(_channel);
        }

        /// <summary>
        /// Send a test event
        /// </summary>
        /// <param name="testEvent">The event string to send</param>
        /// <returns></returns>
        public void WriteTestEvent(string testEvent)
        {
            var request = new TestEventService1.TestEventRequest
            {
                Event = testEvent
            };
            _client.WriteTestEvent(request);
        }

        /// <summary>
        /// Send a test event
        /// </summary>
        /// <param name="testEvent">The event string to send</param>
        /// <returns></returns>
        public async Task WriteTestEventAsync(string testEvent)
        {
            var request = new TestEventService1.TestEventRequest
            {
                Event = testEvent
            };
            await _client.WriteTestEventAsync(request);
        }
#endif
    }
}
