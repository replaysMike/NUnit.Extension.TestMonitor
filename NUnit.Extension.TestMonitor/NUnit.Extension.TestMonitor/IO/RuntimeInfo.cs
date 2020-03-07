using System;
using System.Diagnostics;
using System.Reflection;

namespace NUnit.Extension.TestMonitor.IO
{
    /// <summary>
    /// Gets information about the runtime
    /// </summary>
    public class RuntimeInfo
    {
        private static readonly RuntimeInfo _instance = new RuntimeInfo();
        public static RuntimeInfo Instance => _instance;

        public Guid TestRunId { get; }
        public int ProcessId { get; }
        public string ProcessName { get; }
        public int ProcessSession { get; }
        public DateTime ProcessStartTime { get; }
        public TimeSpan ProcessRuntime => DateTime.Now.Subtract(ProcessStartTime);
        public Assembly EntryAssembly { get; }
        public Assembly ExecutingAssembly { get; }

        static RuntimeInfo() { }
        private RuntimeInfo()
        {
            TestRunId = Guid.NewGuid();
            EntryAssembly = Assembly.GetEntryAssembly();
            ExecutingAssembly = Assembly.GetExecutingAssembly();
            var process = Process.GetCurrentProcess();
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
            ProcessSession = process.SessionId;
            ProcessStartTime = process.StartTime;
        }
    }
}
