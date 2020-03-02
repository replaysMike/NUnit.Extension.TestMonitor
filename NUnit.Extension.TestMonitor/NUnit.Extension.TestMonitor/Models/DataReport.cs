using System.Collections.Generic;

namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Full test report
    /// </summary>
    public class DataReport
    {
        /// <summary>
        /// Total number of tests run
        /// </summary>
        public int TotalTests { get; set; }

        public ICollection<TestCaseReport> TestReports { get; set; } = new List<TestCaseReport>();
    }
}
