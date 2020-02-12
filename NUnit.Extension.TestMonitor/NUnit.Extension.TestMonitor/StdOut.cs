using System;
using System.Diagnostics;

namespace NUnit.Extension.TestMonitor
{
    public class StdOut
    {
        private EventOutputStreams _outputStream = EventOutputStreams.Trace;

        public StdOut() { }

        public StdOut(EventOutputStreams outputStream)
        {
            _outputStream = outputStream;
        }

        /// <summary>
        /// Write a message to configured output stream
        /// </summary>
        /// <param name="message"></param>
        public void WriteLine(string message)
        {
            switch (_outputStream)
            {
                case EventOutputStreams.Trace:
                    Trace.WriteLine(message);
                    break;
                case EventOutputStreams.Debug:
                    Debug.WriteLine(message);
                    break;
                case EventOutputStreams.StdOut:
                    Console.Out.WriteLine(message);
                    break;
            }
        }

        /// <summary>
        /// Write a message to configured output stream
        /// </summary>
        /// <param name="message"></param>
        public void Write(string message)
        {
            switch (_outputStream)
            {
                case EventOutputStreams.Trace:
                    Trace.WriteLine(message);
                    break;
                case EventOutputStreams.Debug:
                    Debug.WriteLine(message);
                    break;
                case EventOutputStreams.StdOut:
                    Console.Out.WriteLine(message);
                    break;
            }
        }
    }
}
