using System;

namespace NUnit.Extension.TestMonitor.IO
{
    public class MessageEventArgs : EventArgs
    {
        public EventEntry EventEntry { get; set; }
        public MessageEventArgs(EventEntry eventEntry)
        {
            EventEntry = eventEntry;
        }
    }
}
