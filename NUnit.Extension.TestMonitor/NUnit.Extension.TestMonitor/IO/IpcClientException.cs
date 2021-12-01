using System;

namespace NUnit.Extension.TestMonitor.IO
{
    public class IpcClientException : Exception
    {
        public IpcClientException(string message) : base(message) { }
    }
}
