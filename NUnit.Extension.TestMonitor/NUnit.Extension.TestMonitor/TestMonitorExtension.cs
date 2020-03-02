using Newtonsoft.Json;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
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
        private const int BufferSize = 1024 * 60;
        private NamedPipeServerStream _serverStream;
        private StreamWriter _ipcWriter;
        private SemaphoreSlim _lock;
        private ManualResetEvent _connectionEvent;
        private ConfigurationResolver _configurationResolver;
        private Configuration _configuration;

        private StdOut StdOut { get; }

        public TestMonitorExtension()
        {
            _lock = new SemaphoreSlim(1, 1);
            _connectionEvent = new ManualResetEvent(false);
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
                        StartTime = DateTime.Now,
                        TestStatus = TestStatus.Running
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
                        StartTime = DateTime.Now,
                        TestStatus = TestStatus.Running
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
            var testResult = entry.GetAttribute("result") == "Passed" ? true : false;
            var testStatus = testResult ? TestStatus.Pass : TestStatus.Fail;
            var testOutput = entry.SelectSingleNode("output").InnerText;
            var failure = entry.SelectSingleNode("failure");
            var errorMessage = failure?.SelectSingleNode("message").InnerText;
            var stackTrace = failure?.SelectSingleNode("stack-trace").InnerText;

            StdOut.WriteLine($"[EndTest] '{name}' {(testResult ? "passed" : "failed")} in {duration}");
            WriteEvent(new DataEvent(EventNames.EndTest)
            {
                Id = id,
                ParentId = parentId,
                TestName = name,
                FullName = fullName,
                StartTime = startTime,
                EndTime = endTime,
                Duration = duration,
                TestResult = testResult,
                TestStatus = testStatus,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                TestOutput = testOutput,
                Report = new DataReport
                {
                    TotalTests = 1
                }
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
            var testStatus = result ? TestStatus.Pass : TestStatus.Fail;

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
                        Inconclusive = inconclusive,
                        TestStatus = testStatus
                    });
                    break;
            }
        }

        /// <summary>
        /// All tests have been run
        /// </summary>
        /// <param name="e"></param>
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
            var testCount = int.Parse(entry.GetAttribute("testcasecount"));
            var inconclusive = int.Parse(entry.GetAttribute("inconclusive"));
            var skipped = int.Parse(entry.GetAttribute("skipped"));
            var passed = int.Parse(entry.GetAttribute("passed"));
            var failed = int.Parse(entry.GetAttribute("failed"));
            var testStatus = result ? TestStatus.Pass : TestStatus.Fail;
            var testCaseNodes = e.Report.GetElementsByTagName("test-case");
            var testCases = new List<TestCaseReport>();
            foreach(XmlNode testCaseNode in testCaseNodes)
            {
                var failure = testCaseNode.SelectSingleNode("failure");
                var testResult = testCaseNode.GetAttribute("result") == "Passed" ? true : false;
                testCases.Add(new TestCaseReport
                {
                    Id = testCaseNode.GetAttribute("id"),
                    TestSuite = testCaseNode.ParentNode.GetAttribute("name"),
                    TestName = testCaseNode.GetAttribute("name"),
                    FullName = testCaseNode.GetAttribute("fullname"),
                    ErrorMessage = failure?.SelectSingleNode("message").InnerText,
                    StackTrace = failure?.SelectSingleNode("stack-trace").InnerText,
                    StartTime = DateTime.Parse(testCaseNode.GetAttribute("start-time")),
                    EndTime = DateTime.Parse(testCaseNode.GetAttribute("end-time")),
                    Duration = TimeSpan.FromSeconds(double.Parse(testCaseNode.GetAttribute("duration"))),
                    TestOutput = testCaseNode.SelectSingleNode("output").InnerText,
                    TestStatus = testResult ? TestStatus.Pass : TestStatus.Fail,
                    TestResult = testResult
                });
            }

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
                TestCount = testCount,
                TestStatus = testStatus,
                Report = new DataReport
                {
                    TotalTests = testCount,
                    TestReports = testCases
                },
                Skipped = skipped,
                Inconclusive = inconclusive
            });
        }

        private void StartIpcServer()
        {
            _serverStream = new NamedPipeServerStream(nameof(TestMonitorExtension), PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.None, BufferSize, BufferSize);
            _ipcWriter = new StreamWriter(_serverStream, Encoding.Default, BufferSize);
            var connectionResult = _serverStream.BeginWaitForConnection((ar) => HandleConnection(ar), null);
            if (!_connectionEvent.WaitOne(_configuration.NamedPipesConnectionTimeoutMilliseconds))
            {
                // timeout waiting for connecting client
                var testContext = TestContext.CurrentContext;
                StdOut.WriteLine($"Timeout ({TimeSpan.FromSeconds(_configuration.NamedPipesConnectionTimeoutMilliseconds)}) waiting for NamedPipe client to connect!");
                _serverStream.Dispose();
            }
        }

        private void HandleConnection(IAsyncResult c)
        {
            // connected
            _serverStream.EndWaitForConnection(c);
            _connectionEvent.Set();
        }

        private void WriteEvent(DataEvent data)
        {
            _lock?.Wait();
            try
            {
                data.Runtime = _configurationResolver.RuntimeDetection.DetectedRuntimeFramework.ToString();
                var serializedString = JsonConvert.SerializeObject(data) + Environment.NewLine;
                // StdOut.WriteLine($"WRITE {data.Event} - {serializedString.Length} bytes");
                // emit IPC/Named pipes
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.NamedPipes) && _ipcWriter != null && _serverStream.IsConnected)
                {
                    try
                    {
                        _ipcWriter.Write(serializedString);
                        _ipcWriter.Flush();
                    }
                    catch (IOException ex)
                    {
                        StdOut.WriteLine($"Error writing to named pipe: {ex.Message}");
                    }
                }
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
                    if (_serverStream?.IsConnected == true)
                        _serverStream?.Disconnect();
                    _serverStream?.Dispose();
                    _serverStream = null;
                    _connectionEvent?.Dispose();
                    _connectionEvent = null;
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
