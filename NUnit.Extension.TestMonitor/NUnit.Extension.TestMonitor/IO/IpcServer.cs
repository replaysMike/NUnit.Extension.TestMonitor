using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace NUnit.Extension.TestMonitor.IO
{
    public class IpcServer : IDisposable
    {
        private const int BufferSize = 1024 * 60;
        private NamedPipeServerStream _serverStream;
        private StreamWriter _ipcWriter;
        private ManualResetEvent _connectionEvent;
        private Configuration _configuration;
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
            _serverStream = new NamedPipeServerStream(nameof(TestMonitorExtension), PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous, BufferSize, BufferSize);
            _ipcWriter = new StreamWriter(_serverStream, Encoding.Default, BufferSize);
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

        public void Write(string text)
        {
            try
            {
                if (_serverStream?.IsConnected == true)
                {
                    _ipcWriter.Write(text);
                    _ipcWriter.Flush();
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
                _serverStream.EndWaitForConnection(c);
                _connectionEvent.Set();
            }
            catch (IOException ex)
            {
                WriteLog($"[{DateTime.Now}]|ERROR|{nameof(HandleConnection)}|{ex.GetBaseException().Message}|{ex.StackTrace.ToString()}\r\n");
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
