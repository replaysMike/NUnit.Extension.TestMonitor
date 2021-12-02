using System;

namespace NUnit.Extension.TestMonitor
{
    public class EventEntry
    {
        public bool IsQueuedForRemoval => RemovalTime != DateTime.MinValue;
        public DateTime RemovalTime { get; set; }
        public DateTime DateAdded { get; }
        public TimeSpan Elapsed => DateTime.Now.Subtract(DateAdded);
        public DataEvent Event { get; set; }

        public EventEntry(DataEvent dataEvent)
        {
            Event = dataEvent;
            DateAdded = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{Event}";
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hc = 23;
            hc = hc * 31 + Event.Id.GetHashCode();
            hc = hc * 31 + Event.Event.GetHashCode();
            hc = hc * 31 + Event.TestStatus.GetHashCode();

            return hc;
        }
    }
}
