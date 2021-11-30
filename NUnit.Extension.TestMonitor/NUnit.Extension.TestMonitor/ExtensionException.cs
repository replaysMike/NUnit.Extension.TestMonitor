using System;
using System.Runtime.Serialization;

namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Extension configuration exception
    /// </summary>
    [Serializable]
    public sealed class ExtensionException : Exception
    {
        public ExtensionException()
        {
        }

        public ExtensionException(string message) : base(message)
        {
        }

        public ExtensionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ExtensionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
