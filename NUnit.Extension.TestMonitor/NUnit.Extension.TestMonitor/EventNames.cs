namespace NUnit.Extension.TestMonitor
{
    /// <summary>
    /// Name of events generated
    /// </summary>
    public enum EventNames
    {
        None = 0,
        StartRun,
        StartAssembly,
        EndAssembly,
        StartSuite,
        EndSuite,
        StartTestFixture,
        EndTestFixture,
        StartTest,
        EndTest,
        Report
    }
}
