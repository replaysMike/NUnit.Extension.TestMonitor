using ProtoBuf;
using System.Collections.Generic;

namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Full test report
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DataReport
    {
        /// <summary>
        /// Total number of tests run
        /// </summary>
        public int TotalTests { get; set; }

        public List<TestCaseReport> TestReports { get; set; } = new List<TestCaseReport>();
    }
}
