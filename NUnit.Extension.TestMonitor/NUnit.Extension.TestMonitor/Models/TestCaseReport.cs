﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProtoBuf;
using System;

namespace NUnit.Extension.TestMonitor
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TestCaseReport
    {
        /// <summary>
        /// Internal NUnit test id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The .Net runtime type
        /// </summary>
        public string Runtime { get; set; }

        /// <summary>
        /// The .Net runtime full version
        /// </summary>
        public string RuntimeVersion { get; set; }

        /// <summary>
        /// Parent object id
        /// </summary>
        public string TestSuite { get; set; }

        /// <summary>
        /// Name of test
        /// </summary>
        public string TestName { get; set; }

        /// <summary>
        /// Full name of test
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// True if test passed, false if it failed
        /// </summary>
        public bool TestResult { get; set; }

        /// <summary>
        /// Current test status
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TestStatus TestStatus { get; set; }

        /// <summary>
        /// Start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Duration of test/suite/run
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Test output
        /// </summary>
        public string TestOutput { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// Total number of assertions
        /// </summary>
        public int Asserts { get; set; }

        /// <summary>
        /// True if this test was ignored
        /// </summary>
        public bool IsSkipped { get; set; }

        public override string ToString()
        {
            return $"{TestName} - {TestStatus}";
        }
    }
}
