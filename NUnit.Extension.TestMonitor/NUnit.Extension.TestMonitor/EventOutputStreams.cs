namespace NUnit.Extension.TestMonitor
{
    public enum EventOutputStreams
    {
        /// <summary>
        /// No output stream
        /// </summary>
        None = 1 << 0,
        /// <summary>
        /// Trace output stream
        /// </summary>
        Trace = 1 << 1,
        /// <summary>
        /// Debug output stream
        /// </summary>
        Debug = 1 << 2,
        /// <summary>
        /// StdOut / Console output
        /// </summary>
        StdOut = 1 << 3
    }
}
