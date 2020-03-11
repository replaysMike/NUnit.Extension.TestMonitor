using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace NUnit.Extension.TestMonitor.IO
{
    public class IpcServer : IDisposable
    {
        private readonly Encoding UseEncoding = Encoding.UTF8;
        private const int BufferSize = 1024 * 256;
        private const int MaxMessageBufferSize = 1024 * 1024 * 4; // 4Mb max message size
        private const ushort StartMessageHeader = 0xA0FF;
        private const ushort EndMessageHeader = 0xA1FF;
        private const byte TotalHeaderLength = sizeof(UInt16) + sizeof(UInt32) + sizeof(UInt16);

        private NamedPipeServerStream _serverStream;
        private BinaryWriter _ipcWriter;
        private ManualResetEvent _connectionEvent;
        private Configuration _configuration;
        private byte[] _messageBufferBytes;

        public StdOut StdOut { get; }
        public Guid TestRunId { get; }

        public IpcServer(Configuration configuration, StdOut stdOut, Guid testRunId)
        {
            _configuration = configuration;
            StdOut = stdOut;
            TestRunId = testRunId;
            _connectionEvent = new ManualResetEvent(false);
        }

        public void Start()
        {
            _serverStream = new NamedPipeServerStream(nameof(TestMonitorExtension), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, BufferSize, BufferSize);
            _ipcWriter = new BinaryWriter(_serverStream, Encoding.Default);
            var connectionResult = _serverStream.BeginWaitForConnection((ar) => HandleConnection(ar), null);
            if (!_connectionEvent.WaitOne(_configuration.NamedPipesConnectionTimeoutMilliseconds))
            {
                // timeout waiting for connecting client
                var timeoutMessage = $"Timeout ({TimeSpan.FromSeconds(_configuration.NamedPipesConnectionTimeoutMilliseconds)}) waiting for NamedPipe client to connect!";
                StdOut.WriteLine(timeoutMessage);
                WriteLog(timeoutMessage);
                _serverStream.Dispose();
            }
        }

        /// <summary>
        /// Write text to connected IPC clients
        /// </summary>
        /// <param name="text"></param>
        public void Write(string text)
        {
            var textBytes = UseEncoding.GetBytes(text);
            Write(textBytes, 0, textBytes.Length);
        }

        /// <summary>
        /// Write binary to connected IPC clients
        /// </summary>
        /// <param name="data"></param>
        /// <param name="startPosition"></param>
        /// <param name="length"></param>
        public void Write(byte[] data, int startPosition, int length)
        {
            try
            {
                if (_serverStream?.IsConnected == true)
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
                    _ipcWriter.Write(_messageBufferBytes, 0, lengthWithHeader);
                }
            }
            catch (IOException ex)
            {
                StdOut.WriteLine($"Error writing to named pipe: {ex.GetBaseException().Message}. StackTrace: {ex.StackTrace}");
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(Write)}|Error writing to named pipe: {ex.GetBaseException().Message}. StackTrace: {ex.StackTrace}\r\n");
            }
        }

        private void HandleConnection(IAsyncResult c)
        {
            // connected
            try
            {
                _messageBufferBytes = new byte[MaxMessageBufferSize];
                _serverStream.EndWaitForConnection(c);
                _connectionEvent.Set();
            }
            catch (IOException ex)
            {
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(HandleConnection)}|{ex.GetBaseException().Message}|{ex.StackTrace.ToString()}\r\n");
            }
            catch (ObjectDisposedException)
            {
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(HandleConnection)}|Timeout waiting for a client to connect!\r\n");
            }
            catch (Exception ex)
            {
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(HandleConnection)}|Unhandled Exception|{ex.GetBaseException().Message}|{ex.StackTrace.ToString()}\r\n");
            }
        }

        private void WriteLog(string text)
        {
            if (!string.IsNullOrEmpty(_configuration.EventsLogFile))
                File.AppendAllText(_configuration.EventsLogFile, text);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            try
            {
                if (isDisposing)
                {
                    try
                    {
                        if (_serverStream?.IsConnected == true)
                        {
                            _ipcWriter?.Flush();
                            _ipcWriter?.Dispose();
                            _ipcWriter = null;
                        }
                        _serverStream?.Dispose();
                        _serverStream = null;
                        _connectionEvent?.Dispose();
                        _connectionEvent = null;
                        _messageBufferBytes = null;
                    }
                    finally
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(IpcServer)}|{nameof(Dispose)}|{ex.GetBaseException().Message}|{ex.StackTrace.ToString()}\r\n");
            }
        }
    }
}
