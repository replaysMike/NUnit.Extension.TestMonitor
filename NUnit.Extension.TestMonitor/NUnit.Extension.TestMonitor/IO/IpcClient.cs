using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace NUnit.Extension.TestMonitor.IO
{
    /// <summary>
    /// Connects to an IpcServer
    /// </summary>
    public sealed class IpcClient : IDisposable
    {
        private readonly Encoding UseEncoding = Encoding.UTF8;
        // how often to should poll for test event updates
        private const int MaxMessageBufferSize = 1024 * 1024 * 4; // 4mb max total message size
        private const ushort StartMessageHeader = 0xA0FF;
        private const ushort EndMessageHeader = 0xA1FF;
        private const byte TotalHeaderLength = sizeof(UInt16) + sizeof(UInt32) + sizeof(UInt16);

        private readonly ManualResetEvent _dataReadEvent;
        private readonly ManualResetEvent _closeEvent;
        private readonly Configuration _configuration;
        private readonly XmlSerializer _xmlSerializer = new XmlSerializer(typeof(DataEvent));
        private NamedPipeClientStream _client;
        private byte[] _messageBufferBytes;
        private bool _isDisposed;

        public bool IsWaitingForConnection { get; private set; }

        public IpcClient(Configuration configuration)
        {
            _configuration = configuration;
            _dataReadEvent = new ManualResetEvent(false);
            _closeEvent = new ManualResetEvent(false);
        }

        public void Connect(Action<IpcClient> onSuccessConnect, Action<IpcClient> onFailedConnect)
        {
            if (_isDisposed) throw new ObjectDisposedException($"Cannot {nameof(Connect)}() a disposed client!");
            _client = new NamedPipeClientStream(".", "Commander", PipeDirection.Out, PipeOptions.Asynchronous);
            try
            {
                IsWaitingForConnection = true;
                if (_configuration.NamedPipesConnectionTimeoutMilliseconds > 0)
                    _client.Connect(_configuration.NamedPipesConnectionTimeoutMilliseconds); // specify in ms
                else
                    _client.Connect();
                _client.ReadMode = PipeTransmissionMode.Byte;
                _messageBufferBytes = new byte[MaxMessageBufferSize];
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case TimeoutException _:
                    case IOException _:
                        // failed to connect
                        IsWaitingForConnection = false;
                        onFailedConnect?.Invoke(this);
                        return;
                    // throw all others
                    default:
                        throw;
                }
            }
            // we are connected now
            IsWaitingForConnection = false;
            onSuccessConnect?.Invoke(this);
        }

        /// <summary>
        /// Write text to connected IPC server
        /// </summary>
        /// <param name="text"></param>
        public void Write(string text)
        {
            if (_isDisposed) throw new ObjectDisposedException($"Cannot {nameof(Write)}() a disposed client!");
            var textBytes = UseEncoding.GetBytes(text);
            Write(textBytes, 0, textBytes.Length);
        }

        /// <summary>
        /// Write binary to connected IPC server
        /// </summary>
        /// <param name="data"></param>
        /// <param name="startPosition"></param>
        /// <param name="length"></param>
        public void Write(byte[] data, int startPosition, int length)
        {
            if (_isDisposed) throw new ObjectDisposedException($"Cannot {nameof(Write)}() a disposed client!");
            try
            {
                if (_client?.IsConnected == true && _client.CanWrite)
                {
                    // write to a local buffer before sending out the IPC pipe. This helps to prevent partial write messages from being sent
                    var lengthWithHeader = 0;
                    using (var stream = new MemoryStream(_messageBufferBytes))
                    {
                        using (var writer = new BinaryWriter(stream))
                        {
                            lengthWithHeader = TotalHeaderLength + length;
                            // write the data length header
                            writer.Write(StartMessageHeader);
                            writer.Write((UInt32)length);
                            writer.Write(EndMessageHeader);
                            // write the data
                            writer.Write(data, startPosition, length);
                        }
                    }
                    // write to the IPC named pipe
                    _client.Write(_messageBufferBytes, 0, lengthWithHeader);
                }
            }
            catch (IOException ex)
            {
                throw new ExtensionException($"[{DateTime.Now}]|ERROR|{nameof(Write)}|Error writing to named pipe: {ex.GetBaseException().Message}. StackTrace: {ex.StackTrace}{Environment.NewLine}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool isDisposing)
        {
            _isDisposed = true;
            if (isDisposing)
            {
                if (_client?.IsConnected == true)
                {
                    _client.Close();
                }
                _client?.Dispose();
            }
        }
    }
}
