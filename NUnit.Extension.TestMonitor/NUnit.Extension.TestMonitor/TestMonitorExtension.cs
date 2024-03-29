﻿using Newtonsoft.Json;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using NUnit.Extension.TestMonitor.IO;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Provides a real-time reporting mechanism through IPC and Standard in/out
    /// </summary>
    [Extension(Description = "Provides Test event monitoring and logging", EngineVersion = "3.4")]
    public class TestMonitorExtension : ITestEventListener, IDisposable
    {
        private SemaphoreSlim _lock;
        private readonly ConfigurationResolver _configurationResolver;
        private readonly Configuration _configuration;
        private readonly IpcClient _ipcClient;
#if GRPC
        private readonly GrpcClient _grpcClient;
#endif

        private StdOut StdOut { get; }

        public TestMonitorExtension()
        {
            try
            {
                _lock = new SemaphoreSlim(1, 1);
                _configurationResolver = new ConfigurationResolver(new RuntimeDetection());
                _configuration = _configurationResolver.GetConfiguration();
                if (_configuration == null) throw new ExtensionException("Could not resolve a valid configuration!");
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.StdOut))
                    StdOut = new StdOut(_configuration.EventOutputStream);
                else
                    StdOut = new StdOut(EventOutputStreams.None);

                // ensure path to log file exists
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.LogFile) && !string.IsNullOrEmpty(_configuration.EventsLogFile))
                    EnsurePathExists(_configuration.EventsLogFile);

                // check for the existence of a known runner. If one is specified in the config, and it's currently running then continue
                if (!string.IsNullOrWhiteSpace(_configuration.SupportedRunnerExe))
                {
                    if (!CheckForKnownRunner(_configuration.SupportedRunnerExe))
                    {
                        // create no IPC server, extension will ignore incoming messages
                        WriteLog($"|WARN|{nameof(TestMonitorExtension)}|Aborting extension registration, supported test runner not found.{Environment.NewLine}");
                        return;
                    }
                }

                var activationMessage = $"NUnit.Extension.TestMonitor extension activated for run '{RuntimeInfo.Instance.TestRunId}'. EventFormat: '{_configuration.EventFormat}' ProcessInfo: {RuntimeInfo.Instance.ProcessName}|{RuntimeInfo.Instance.ProcessId}|{RuntimeInfo.Instance.ProcessSession}|{RuntimeInfo.Instance.ProcessStartTime}|{RuntimeInfo.Instance.ProcessRuntime}  EntryAssembly:{RuntimeInfo.Instance.EntryAssembly?.FullName} ExecutingAssembly: {RuntimeInfo.Instance.ExecutingAssembly?.FullName}{Environment.NewLine}";
                StdOut.WriteLine(activationMessage);

                WriteLog(activationMessage);

                // submit events to Grpc server
#if GRPC
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.Grpc))
                {
                    WriteLog($"|INFO|{nameof(TestMonitorExtension)}|Connecting to Grpc server...{Environment.NewLine}");
                    _grpcClient = new GrpcClient(_configuration);
                }
#endif

                // submit events to IpcServer
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.NamedPipes))
                {
                    WriteLog($"|INFO|{nameof(TestMonitorExtension)}|Connecting to local Ipc server...{Environment.NewLine}");
                    _ipcClient = new IpcClient(_configuration);
                    _ipcClient.Connect((client) =>
                    {
                        WriteLog($"|INFO|{nameof(TestMonitorExtension)}|Connected to local Ipc server.{Environment.NewLine}");
                    }, (client) =>
                    {
                        WriteLog($"|WARN|{nameof(TestMonitorExtension)}|Failed to connect to local Ipc server!{Environment.NewLine}");
                    });
                }
            }
            catch (Exception ex)
            {
                if (_configuration != null)
                    WriteLog($"|ERROR|{nameof(TestMonitorExtension)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
                throw new ExtensionException($"Fatal exception: {ex.GetBaseException().Message}|{ex.StackTrace}");
            }
        }

        public void OnTestEvent(string report)
        {
            // if not sending events anywhere, ignore incoming messages
#if GRPC
            if (_ipcClient == null && _grpcClient == null)
                return;
#else
            if (_ipcClient == null)
                return;
#endif
            try
            {
                // StdOut.WriteLine($"Test event: {report}");
                var doc = new XmlDocument();
                doc.LoadXml(report);
                // we are only interested in specific events
                switch (doc.FirstChild.Name)
                {
                    case "start-run":
                        StartRun(new TestMonitorExtensionEventArgs(doc.FirstChild.Name, doc));
                        break;
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
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(OnTestEvent)}|{ex.GetBaseException().Message}|{ex.StackTrace.ToString()}{Environment.NewLine}");
            }
        }

        private void StartRun(TestMonitorExtensionEventArgs e)
        {
            try
            {
                if (e?.Report != null && e.Report.HasChildNodes)
                {
                    var entry = e.Report.FirstChild;
                    var count = GetInteger(entry.GetAttribute("count"));
                    StdOut.WriteLine($"[StartRun] Tests: {count}");
                    WriteEvent(new DataEvent(EventNames.StartRun)
                    {
                        TestCount = count,
                        StartTime = DateTime.Now,
                        TestRunId = RuntimeInfo.Instance.TestRunId,
                        TestRunner = RuntimeInfo.Instance.ProcessName,
                    });
                }
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(StartRun)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        private void StartSuite(TestMonitorExtensionEventArgs e)
        {
            try
            {
                if (e?.Report != null && e.Report.HasChildNodes)
                {
                    var entry = e.Report.FirstChild;
                    var id = entry.GetAttribute("id");
                    var parentId = entry.GetAttribute("parentId");
                    var name = entry.GetAttribute("name");
                    var fullName = entry.GetAttribute("fullname");
                    var type = entry.GetAttribute("type");
                    switch (type)
                    {
                        case "Assembly":
                            StdOut.WriteLine($"[StartAssembly] {name}");
                            WriteEvent(new DataEvent(EventNames.StartAssembly)
                            {
                                Id = id,
                                ParentId = parentId,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = DateTime.Now,
                                TestStatus = TestStatus.Running
                            });
                            break;
                        case "TestFixture":
                            StdOut.WriteLine($"[StartTestFixture] {name}");
                            WriteEvent(new DataEvent(EventNames.StartTestFixture)
                            {
                                Id = id,
                                ParentId = parentId,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = DateTime.Now,
                                TestStatus = TestStatus.Running
                            });
                            break;
                        case "TestSuite":
                        // older versions (3.4.1) of nUnit dont provide a type on the start event, must have been a bug as the end events are correct
                        default:
                            StdOut.WriteLine($"[StartSuite] {name}");
                            WriteEvent(new DataEvent(EventNames.StartSuite)
                            {
                                Id = id,
                                ParentId = parentId,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = DateTime.Now,
                                TestStatus = TestStatus.Running
                            });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(StartSuite)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        private void StartTest(TestMonitorExtensionEventArgs e)
        {
            try
            {
                if (e?.Report != null && e.Report.HasChildNodes)
                {
                    var entry = e.Report.FirstChild;
                    var id = entry.GetAttribute("id");
                    var parentId = entry.GetAttribute("parentId");
                    var name = entry.GetAttribute("name");
                    var fullName = entry.GetAttribute("fullname");
                    var type = entry.GetAttribute("type");

                    // regular test method or test case source. It seems test case sources dont have a type.
                    StdOut.WriteLine($"[StartTest] {name}");
                    WriteEvent(new DataEvent(EventNames.StartTest)
                    {
                        Id = id,
                        ParentId = parentId,
                        TestRunId = RuntimeInfo.Instance.TestRunId,
                        TestRunner = RuntimeInfo.Instance.ProcessName,
                        TestName = name,
                        FullName = fullName,
                        TestType = type,
                        StartTime = DateTime.Now,
                        TestStatus = TestStatus.Running
                    });
                }
                else
                {
                    WriteLog($"|ERROR|{nameof(StartTest)}|Test had no data.{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(StartTest)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        private void EndTest(TestMonitorExtensionEventArgs e)
        {
            try
            {
                if (e?.Report != null && e.Report.HasChildNodes)
                {
                    var entry = e.Report.FirstChild;
                    var id = entry.GetAttribute("id");
                    var parentId = entry.GetAttribute("parentId");
                    var name = entry.GetAttribute("name");
                    var fullName = entry.GetAttribute("fullname");
                    var startTime = GetDateTime(entry.GetAttribute("start-time"));
                    var endTime = GetDateTime(entry.GetAttribute("end-time"));
                    var duration = TimeSpan.FromSeconds(GetDouble(entry.GetAttribute("duration")));
                    var label = entry.GetAttribute("label");
                    var result = entry.GetAttribute("result");
                    var asserts = GetInteger(entry.GetAttribute("asserts"));
                    var isSkipped = false;
                    var testOutput = entry.SelectSingleNode("output")?.InnerText;
                    var failure = entry.SelectSingleNode("failure");
                    var errorMessage = failure?.SelectSingleNode("message")?.InnerText;
                    var stackTrace = failure?.SelectSingleNode("stack-trace")?.InnerText;
                    var properties = entry.SelectSingleNode("properties");
                    var skipReason = properties?.SelectSingleNode("property[@name='_SKIPREASON']")?.GetAttribute("value");

                    var testResult = false;
                    TestStatus testStatus = TestStatus.Fail;

                    switch (result.ToLower())
                    {
                        case "passed":
                            testResult = true;
                            testStatus = TestStatus.Pass;
                            break;
                        case "failed":
                            testResult = false;
                            testStatus = TestStatus.Fail;
                            break;
                        case "skipped":
                            testResult = false;
                            testStatus = TestStatus.Skipped;
                            isSkipped = true;
                            if (skipReason != null)
                                errorMessage = skipReason;
                            break;
                    }

                    StdOut.WriteLine($"[EndTest] '{name}' {(testResult ? "passed" : "failed")} in {duration}");
                    WriteEvent(new DataEvent(EventNames.EndTest)
                    {
                        Id = id,
                        ParentId = parentId,
                        TestRunId = RuntimeInfo.Instance.TestRunId,
                        TestRunner = RuntimeInfo.Instance.ProcessName,
                        TestName = name,
                        FullName = fullName,
                        StartTime = startTime,
                        EndTime = endTime,
                        Duration = duration,
                        TestResult = testResult,
                        IsSkipped = isSkipped,
                        TestStatus = testStatus,
                        ErrorMessage = errorMessage,
                        StackTrace = stackTrace,
                        TestOutput = testOutput,
                        Asserts = asserts,
                        Report = new DataReport
                        {
                            TotalTests = 1
                        }
                    });
                }
                else
                {
                    WriteLog($"|WARN|{nameof(EndTest)}|EndTest report not sent.|{e?.Report?.OuterXml}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(EndTest)}|{ex.Message}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        private void EndSuite(TestMonitorExtensionEventArgs e)
        {
            try
            {
                if (e?.Report != null && e.Report.HasChildNodes)
                {
                    var entry = e.Report.FirstChild;
                    var id = entry.GetAttribute("id");
                    var name = entry.GetAttribute("name");
                    var fullName = entry.GetAttribute("fullname");
                    var type = entry.GetAttribute("type");
                    var startTime = GetDateTime(entry.GetAttribute("start-time"));
                    var endTime = GetDateTime(entry.GetAttribute("end-time"));
                    var duration = TimeSpan.FromSeconds(GetDouble(entry.GetAttribute("duration")));
                    var result = entry.GetAttribute("result") == "Passed" ? true : false;
                    var testCaseCount = GetInteger(entry.GetAttribute("testcasecount"));
                    var totalTests = GetInteger(entry.GetAttribute("total"));
                    var warnings = GetInteger(entry.GetAttribute("warnings"));
                    var inconclusive = GetInteger(entry.GetAttribute("inconclusive"));
                    var skipped = GetInteger(entry.GetAttribute("skipped"));
                    var passed = GetInteger(entry.GetAttribute("passed"));
                    var failed = GetInteger(entry.GetAttribute("failed"));
                    var properties = entry.SelectSingleNode("properties");
                    var processId = GetInteger(properties?.SelectSingleNode("property[@name='_PID']")?.GetAttribute("value"));
                    var appDomain = properties?.SelectSingleNode("property[@name='_APPDOMAIN']")?.GetAttribute("value");
                    var testStatus = result ? TestStatus.Pass : TestStatus.Fail;

                    switch (type)
                    {
                        case "Assembly":
                            StdOut.WriteLine($"[EndAssembly] '{name}' completed in {duration}. {passed} passed {failed} failures");
                            WriteEvent(new DataEvent(EventNames.EndAssembly)
                            {
                                Id = id,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = startTime,
                                EndTime = endTime,
                                ProcessId = processId,
                                AppDomain = appDomain,
                                Duration = duration,
                                TestResult = result,
                                Passed = passed,
                                Failed = failed,
                                Warnings = warnings,
                                Skipped = skipped,
                                TestCases = testCaseCount,
                                TestCount = totalTests,
                                Inconclusive = inconclusive,
                                TestStatus = testStatus
                            });
                            break;
                        case "TestFixture":
                            StdOut.WriteLine($"[EndTestFixture] '{name}' completed in {duration}. {passed} passed {failed} failures");
                            WriteEvent(new DataEvent(EventNames.EndTestFixture)
                            {
                                Id = id,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = startTime,
                                EndTime = endTime,
                                Duration = duration,
                                TestResult = result,
                                Passed = passed,
                                Failed = failed,
                                Warnings = warnings,
                                Skipped = skipped,
                                TestCases = testCaseCount,
                                TestCount = totalTests,
                                Inconclusive = inconclusive,
                                TestStatus = testStatus
                            });
                            break;
                        case "TestSuite":
                        // older versions (3.4.1) of nUnit dont provide a type on the start event, must have been a bug as the end events are correct
                        default:
                            StdOut.WriteLine($"[EndSuite] '{name}' completed in {duration}. {passed} passed {failed} failures");
                            WriteEvent(new DataEvent(EventNames.EndSuite)
                            {
                                Id = id,
                                TestRunId = RuntimeInfo.Instance.TestRunId,
                                TestRunner = RuntimeInfo.Instance.ProcessName,
                                TestSuite = name,
                                FullName = fullName,
                                StartTime = startTime,
                                EndTime = endTime,
                                Duration = duration,
                                TestResult = result,
                                Passed = passed,
                                Failed = failed,
                                Warnings = warnings,
                                Skipped = skipped,
                                TestCases = testCaseCount,
                                TestCount = totalTests,
                                Inconclusive = inconclusive,
                                TestStatus = testStatus
                            });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(EndSuite)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        /// <summary>
        /// All tests have been run
        /// </summary>
        /// <param name="e"></param>
        private void EndRun(TestMonitorExtensionEventArgs e)
        {
            try
            {
                var entry = e.Report.FirstChild;
                var id = entry.GetAttribute("id");
                var name = entry.GetAttribute("name");
                var fullName = entry.GetAttribute("fullname");
                var startTime = GetDateTime(entry.GetAttribute("start-time"));
                var endTime = GetDateTime(entry.GetAttribute("end-time"));
                var duration = TimeSpan.FromSeconds(GetDouble(entry.GetAttribute("duration")));
                var result = entry.GetAttribute("result") == "Passed" ? true : false;
                var testCount = GetInteger(entry.GetAttribute("testcasecount"));
                var inconclusive = GetInteger(entry.GetAttribute("inconclusive"));
                var skipped = GetInteger(entry.GetAttribute("skipped"));
                var passed = GetInteger(entry.GetAttribute("passed"));
                var failed = GetInteger(entry.GetAttribute("failed"));
                var asserts = GetInteger(entry.GetAttribute("asserts"));
                var testStatus = result ? TestStatus.Pass : TestStatus.Fail;
                var testCaseNodes = e.Report.GetElementsByTagName("test-case");
                var testCases = new List<TestCaseReport>();
                if (testCaseNodes != null && testCaseNodes.Count > 0)
                {
                    var runtime = _configurationResolver.RuntimeDetection.DetectedRuntimeFramework.ToString();
                    var runtimeVersion = _configurationResolver.RuntimeDetection.DetectedRuntimeFrameworkDescription.ToString();

                    foreach (XmlNode testCaseNode in testCaseNodes)
                    {
                        var label = testCaseNode.GetAttribute("label");
                        var failure = testCaseNode.SelectSingleNode("failure");
                        var errorMessage = failure?.SelectSingleNode("message")?.InnerText;
                        var stackTrace = failure?.SelectSingleNode("stack-trace")?.InnerText;
                        var testOutput = testCaseNode.SelectSingleNode("output")?.InnerText;
                        var testResult = testCaseNode.GetAttribute("result") == "Passed" ? true : false;
                        var testCaseAsserts = GetInteger(testCaseNode.GetAttribute("asserts"));
                        var isSkipped = false;
                        var testCaseResult = testCaseNode.GetAttribute("result");
                        var properties = testCaseNode.SelectSingleNode("properties");
                        var skipReason = properties?.SelectSingleNode("property[@name='_SKIPREASON']")?.GetAttribute("value");

                        TestStatus testCaseStatus = TestStatus.Fail;
                        switch (testCaseResult.ToLower())
                        {
                            case "passed":
                                testResult = true;
                                testCaseStatus = TestStatus.Pass;
                                break;
                            case "failed":
                                testResult = false;
                                testCaseStatus = TestStatus.Fail;
                                break;
                            case "skipped":
                                testResult = false;
                                testCaseStatus = TestStatus.Skipped;
                                isSkipped = true;
                                if (skipReason != null)
                                    errorMessage = skipReason;
                                break;
                        }

                        testCases.Add(new TestCaseReport
                        {
                            Id = testCaseNode.GetAttribute("id"),
                            Runtime = runtime,
                            RuntimeVersion = runtimeVersion,
                            TestSuite = testCaseNode.ParentNode.GetAttribute("name"),
                            TestName = testCaseNode.GetAttribute("name"),
                            FullName = testCaseNode.GetAttribute("fullname"),
                            ErrorMessage = errorMessage,
                            StackTrace = stackTrace,
                            TestResult = testResult,
                            IsSkipped = isSkipped,
                            TestStatus = testCaseStatus,
                            TestOutput = testOutput,
                            Asserts = testCaseAsserts,
                            StartTime = GetDateTime(testCaseNode.GetAttribute("start-time")),
                            EndTime = GetDateTime(testCaseNode.GetAttribute("end-time")),
                            Duration = TimeSpan.FromSeconds(GetDouble(testCaseNode.GetAttribute("duration"))),
                        });
                    }
                }

                StdOut.WriteLine($"[Report] All tests completed in {duration}. {passed} passed {failed} failures");
                WriteEvent(new DataEvent(EventNames.Report)
                {
                    Id = id,
                    TestRunId = RuntimeInfo.Instance.TestRunId,
                    TestRunner = RuntimeInfo.Instance.ProcessName,
                    TestName = name,
                    FullName = fullName,
                    StartTime = startTime,
                    EndTime = endTime,
                    Duration = duration,
                    TestResult = result,
                    Passed = passed,
                    Failed = failed,
                    Asserts = asserts,
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

                // NUnit doesn't dispose its extensions, we are forced to treat this event as a final message
                Dispose(true);
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(EndRun)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }

        private void WriteEvent(DataEvent dataEvent)
        {
            _lock?.Wait();
            try
            {
                // inject data required on every event
                dataEvent.TestRunId = RuntimeInfo.Instance.TestRunId;
                dataEvent.Runtime = _configurationResolver.RuntimeDetection.DetectedRuntimeFramework.ToString();
                dataEvent.RuntimeVersion = _configurationResolver.RuntimeDetection.DetectedRuntimeFrameworkDescription.ToString();

                string serializedString = null;
                byte[] serializedBytes = null;
                // serialize the data
                try
                {
                    switch (_configuration.EventFormat)
                    {
                        case EventFormatTypes.Binary:
                            using (var stream = new MemoryStream())
                            {
                                Serializer.Serialize<DataEvent>(stream, dataEvent);
                                serializedBytes = stream.ToArray();
                            }
                            break;
                        case EventFormatTypes.Json:
                            serializedString = JsonConvert.SerializeObject(dataEvent) + Environment.NewLine;
                            break;
                        case EventFormatTypes.Xml:
                            var serializer = new XmlSerializer(typeof(DataEvent));
                            var settings = new XmlWriterSettings
                            {
                                Indent = false,
                                NewLineHandling = NewLineHandling.None
                            };

                            using (var stringWriter = new StringWriter())
                            {
                                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                                {
                                    serializer.Serialize(xmlWriter, dataEvent);
                                    serializedString = stringWriter.ToString() + Environment.NewLine;
                                }
                            }
                            break;
                    }
                }
                catch (Exception serializationException)
                {
                    WriteLog($"|ERROR|{nameof(WriteEvent)}|Failed to {_configuration.EventFormat} serialize event: {serializationException.GetBaseException().Message}|{serializationException.StackTrace}{Environment.NewLine}");
                    return;
                }

                // emit serialized data over IPC/Named pipes
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.NamedPipes) && _ipcClient != null)
                {
                    try
                    {
                        if (serializedBytes != null)
                            _ipcClient.Write(serializedBytes, 0, serializedBytes.Length);
                        else if (!string.IsNullOrEmpty(serializedString))
                            _ipcClient.Write(serializedString);
                    }
                    catch (IOException ex)
                    {
                        StdOut.WriteLine($"Error writing to named pipe: {ex.GetBaseException().Message}. StackTrace: {ex.StackTrace}");
                        WriteLog($"|ERROR|{nameof(WriteEvent)}|Error writing to named pipe: {ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
                    }
                }
#if GRPC
                if (_configuration.EventEmitType.HasFlag(EventEmitTypes.Grpc) && _grpcClient != null)
                {
                    try
                    {
                        _grpcClient.WriteTestEvent(serializedString);
                    }
                    catch (Exception ex)
                    {
                        StdOut.WriteLine($"Error writing to Grpc server: {ex.GetBaseException().Message}. StackTrace: {ex.StackTrace}");
                        WriteLog($"|ERROR|{nameof(WriteEvent)}|Error writing to Grpc server: {ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
                    }
                }
#endif

                // also log data to file if configured
                if (serializedBytes != null)
                    WriteLog(serializedBytes, dataEvent);
                else
                    WriteLog(serializedString);
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(WriteEvent)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
            finally
            {
                _lock?.Release();
            }
        }

        private bool CheckForKnownRunner(string runnerExe)
        {
            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(runnerExe));
                WriteLog($"|INFO|{nameof(TestMonitorExtension)}|Number of matching processes named '{runnerExe}': {processes.Length}{Environment.NewLine}");
                return processes.Any();
            }
            catch (InvalidOperationException ex)
            {
                WriteLog($"|ERROR|{nameof(WriteEvent)}|Could not check for the presence of the test runner named '{runnerExe}'. {ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
            return false;
        }

        private int GetInteger(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;
            int.TryParse(str, out var value);
            return value;
        }

        private double GetDouble(string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;
            double.TryParse(str, out var value);
            return value;
        }

        private DateTime GetDateTime(string str)
        {
            if (string.IsNullOrEmpty(str))
                return DateTime.MinValue;
            DateTime.TryParse(str, out var value);
            return value;
        }

        private void WriteLog(string text)
        {
            if (_configuration == null)
            {
                throw new ArgumentNullException($"{nameof(_configuration)} is null!");
            }

            if (_configuration.EventEmitType.HasFlag(EventEmitTypes.LogFile) && !string.IsNullOrEmpty(_configuration.EventsLogFile))
            {
                File.AppendAllText(_configuration.EventsLogFile, $"[{DateTime.Now}][{text.Length}]|{text}");
            }
        }

        private void WriteLog(byte[] bytes, DataEvent dataEvent)
        {
            if (!string.IsNullOrEmpty(_configuration.EventsLogFile))
            {
                var status = string.Empty;
                var duration = dataEvent.Duration.ToString();
                var title = dataEvent.TestName;
                if (string.IsNullOrEmpty(title))
                    title = dataEvent.TestSuite;
                switch (dataEvent.Event)
                {
                    case EventNames.EndTest:
                        status = dataEvent.TestStatus.ToString();
                        break;
                    case EventNames.Report:
                        status = dataEvent.TestStatus.ToString();
                        break;
                }
                File.AppendAllText(_configuration.EventsLogFile, $"[{DateTime.Now}][{bytes.Length}]|{dataEvent.Event} {(title)} {duration} {status}{Environment.NewLine}");
            }
        }

        private void EnsurePathExists(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    var message = $"Unable to create log path due to exception";
                    throw new ExtensionException(message, ex);
                }
            }
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
                    _lock?.Wait();
                    try
                    {
                        _ipcClient?.Dispose();
                    }
                    finally
                    {
                        _lock?.Release();
                        _lock?.Dispose();
                        _lock = null;
                    }
                }
                WriteLog($"NUnit.Extension.TestMonitor extension deactivated for run {RuntimeInfo.Instance.TestRunId}.{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                WriteLog($"|ERROR|{nameof(Dispose)}|{ex.GetBaseException().Message}|{ex.StackTrace}{Environment.NewLine}");
            }
        }
    }
}
