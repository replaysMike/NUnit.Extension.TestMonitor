using System;
using System.Xml;

namespace NUnit.Extension.TestMonitor
{
    public class TestMonitorExtensionEventArgs : EventArgs
    {
        public string EventName { get; }
        public XmlDocument Report { get; }
        public TestMonitorExtensionEventArgs(string eventName, XmlDocument report) : base()
        {
            EventName = eventName;
            Report = report;
        }

        public override string ToString()
        {
            return $"{EventName}-{Report?.OuterXml?.ToString()}";
        }
    }
}
