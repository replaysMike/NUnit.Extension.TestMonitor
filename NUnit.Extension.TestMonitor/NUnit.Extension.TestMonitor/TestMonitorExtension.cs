using Newtonsoft.Json;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Xml;

namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Provides a real-time reporting mechanism through IPC and Standard in/out
    /// </summary>
    [Extension(Description = "Provides Test event monitoring and logging", EngineVersion = "3.4")]
    public class TestMonitorExtension : ITestEventListener, IDisposable
    {
        private NamedPipeServerStream _serverStream;
        private StreamWriter _ipcWriter;
        private SemaphoreSlim _lock;
        private ConfigurationResolver _configurationResolver;
        private Configuration _configuration;

        private StdOut StdOut { get; }

        public TestMonitorExtension()
        {
            _lock = new SemaphoreSlim(1, 1);
            _configurationResolver = new ConfigurationResolver(new RuntimeDetection());
            _configuration = _configurationResolver.GetConfiguration();
            if (_configuration.EventEmitType.HasFlag(EventEmitTypes.StdOut))
                StdOut = new StdOut(_configuration.EventOutputStream);
            else
                StdOut = new StdOut(EventOutputStreams.None);
            if (_configuration.EventEmitType.HasFlag(EventEmitTypes.NamedPipes))
                StartIpcServer();
        }

        public void OnTestEvent(string report)
        {
            // StdOut.WriteLine($"Test event: {report}");
            var doc = new XmlDocument();
            doc.LoadXml(report);
            // we are only interested in specific events
            switch (doc.FirstChild.Name)
            {
                // a specific test suite is starting
                case "start-suite":
                    StartSuite(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                    break;
                // a specific test case is starting
                case "start-test":
                    StartTest(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                    break;
                // a specific test case is completed
                case "test-case":
                    EndTest(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                    break;
                // a specific test suite is completed
                case "test-suite":
                    EndSuite(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                    break;
                // final run results
                case "test-run":
                    EndRun(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                    break;
            }
        }

        private void StartSuite(TestMonitorExtensionEventArgs e)
        {
            var entry = e.Report.FirstChild;
            var id = entry.GetAttribute("id");
            var parentId = entry.GetAttribute("parentId");
            var suiteName = entry.GetAttribute("name");
            var suiteFullName = entry.GetAttribute("fullname");
            var type = entry.GetAttribute("type");
            switch (type)
            {
                case "TestFixture":
                    StdOut.WriteLine($"[StartSuite] {suiteName}");
                    WriteEvent(new DataEvent(EventNames.StartSuite)
                    {
                        Id = id,
                        ParentId = parentId,
                        TestSuite = suiteName,
                        FullName = suiteFullName,
                        StartTime = DateTime.Now
                    });
                    break;
            }
        }

        private void StartTest(TestMonitorExtensionEventArgs e)
        {
            var entry = e.Report.FirstChild;
            var id = entry.GetAttribute("id");
            var parentId = entry.GetAttribute("parentId");
            var name = entry.GetAttribute("name");
            var fullName = entry.GetAttribute("fullname");
            var type = entry.GetAttribute("type");
            switch (type)
            {
                case "TestMethod":
                    StdOut.WriteLine($"[StartTest] {name}");
                    WriteEvent(new DataEvent(EventNames.StartTest)
                    {
                        Id = id,
                        ParentId = parentId,
                        TestName = name,
                        FullName = fullName,
                        StartTime = DateTime.Now
                    });
                    break;
            }
        }

        private void EndTest(TestMonitorExtensionEventArgs e)
        {
            var entry = e.Report.FirstChild;
            var id = entry.GetAttribute("id");
            var parentId = entry.GetAttribute("parentId");
            var name = entry.GetAttribute("name");
            var fullName = entry.GetAttribute("fullname");
            var startTime = DateTime.Parse(entry.GetAttribute("start-time"));
            var endTime = DateTime.Parse(entry.GetAttribute("end-time"));
            var duration = TimeSpan.FromSeconds(double.Parse(entry.GetAttribute("duration")));
            var result = entry.GetAttribute("result") == "Passed" ? true : false;

            StdOut.WriteLine($"[EndTest] '{name}' {(result ? "passed" : "failed")} in {duration}");
            WriteEvent(new DataEvent(EventNames.EndTest)
            {
                Id = id,
                ParentId = parentId,
                TestName = name,
                FullName = fullName,
                StartTime = startTime,
                EndTime = endTime,
                Duration = duration,
                TestResult = result,
            });
        }

        private void EndSuite(TestMonitorExtensionEventArgs e)
        {
            var entry = e.Report.FirstChild;
            var id = entry.GetAttribute("id");
            var name = entry.GetAttribute("name");
            var fullName = entry.GetAttribute("fullname");
            var type = entry.GetAttribute("type");
            var startTime = DateTime.Parse(entry.GetAttribute("start-time"));
            var endTime = DateTime.Parse(entry.GetAttribute("end-time"));
            var duration = TimeSpan.FromSeconds(double.Parse(entry.GetAttribute("duration")));
            var result = entry.GetAttribute("result") == "Passed" ? true : false;
            var totalTests = int.Parse(entry.GetAttribute("total"));
            var warnings = int.Parse(entry.GetAttribute("warnings"));
            var inconclusive = int.Parse(entry.GetAttribute("inconclusive"));
            var skipped = int.Parse(entry.GetAttribute("skipped"));
            var passed = int.Parse(entry.GetAttribute("passed"));
            var failed = int.Parse(entry.GetAttribute("failed"));
            switch (type)
            {
                case "TestMethod":
                    StdOut.WriteLine($"[EndSuite] '{name}' completed in {duration}. {passed} passed {failed} failures");
                    WriteEvent(new DataEvent(EventNames.EndSuite)
                    {
                        Id = id,
                        TestName = name,
                        FullName = fullName,
                        StartTime = startTime,
                        EndTime = endTime,
                        Duration = duration,
                        TestResult = result,
                        Passed = passed,
                        Failed = failed,
                        Warnings = warnings,
                        Skipped = skipped,
                        TestCount = totalTests,
                        Inconclusive = inconclusive
                    });
                    break;
            }
        }

        private void EndRun(TestMonitorExtensionEventArgs e)
        {
            var entry = e.Report.FirstChild;
            var id = entry.GetAttribute("id");
            var name = entry.GetAttribute("name");
            var fullName = entry.GetAttribute("fullname");
            var startTime = DateTime.Parse(entry.GetAttribute("start-time"));
            var endTime = DateTime.Parse(entry.GetAttribute("end-time"));
            var duration = TimeSpan.FromSeconds(double.Parse(entry.GetAttribute("duration")));
            var result = entry.GetAttribute("result") == "Passed" ? true : false;
            var totalTests = int.Parse(entry.GetAttribute("total"));
            var inconclusive = int.Parse(entry.GetAttribute("inconclusive"));
            var skipped = int.Parse(entry.GetAttribute("skipped"));
            var passed = int.Parse(entry.GetAttribute("passed"));
            var failed = int.Parse(entry.GetAttribute("failed"));
            StdOut.WriteLine($"[Report] All tests completed in {duration}. {passed} passed {failed} failures");
            WriteEvent(new DataEvent(EventNames.Report)
            {
                Id = id,
                TestName = name,
                FullName = fullName,
                StartTime = startTime,
                EndTime = endTime,
                Duration = duration,
                TestResult = result,
                Passed = passed,
                Failed = failed,
                Report = new DataReport
                {
                    TotalTests = totalTests,
                },
                Skipped = skipped,
                Inconclusive = inconclusive
            });
        }

        private void StartIpcServer()
        {
            _serverStream = new NamedPipeServerStream(nameof(TestMonitorExtension));
            _ipcWriter = new StreamWriter(_serverStream);
        }

        private void WriteEvent(DataEvent data)
        {
            _lock?.Wait();
            try
            {
                data.Runtime = _configurationResolver.RuntimeDetection.DetectedRuntimeFramework.ToString();
                var serializedString = JsonConvert.SerializeObject(data) + Environment.NewLine;
                // emit IPC/Named pipes
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.NamedPipes)
                    && _ipcWriter != null
                    && _serverStream.IsConnected)
                    _ipcWriter.Write(serializedString);
                // emit log
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.LogFile)
                    && !string.IsNullOrEmpty(_configuration.EventsLogFile))
                    File.AppendAllText(_configuration.EventsLogFile, serializedString);
            }
            finally
            {
                _lock?.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _lock?.Wait();
                try
                {
                    _ipcWriter?.Flush();
                    _ipcWriter?.Dispose();
                    _ipcWriter = null;
                    _serverStream?.Disconnect();
                    _serverStream?.Dispose();
                    _serverStream = null;
                }
                finally
                {
                    _lock?.Release();
                    _lock?.Dispose();
                    _lock = null;
                }
            }
        }
    }
}
