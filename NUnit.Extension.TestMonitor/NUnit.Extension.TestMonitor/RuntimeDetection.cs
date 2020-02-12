using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NUnit.Extension.TestMonitor
{
    public class RuntimeDetection
    {
        private Assembly _entryAssembly;

        public string DetectedRuntimePlatform { get; private set; }
        public RuntimeFramework DetectedRuntimeFramework { get; private set; } = RuntimeFramework.DotNetFramework;
        public string DetectedRuntimeFrameworkDescription { get; private set; }

        public RuntimeDetection() : this(Assembly.GetEntryAssembly())
        {
        }

        public RuntimeDetection(Assembly entryAssembly)
        {
            _entryAssembly = entryAssembly;
            DetectRuntime();
        }

        private void DetectRuntime()
        {
            string framework = null;
            DetectedRuntimePlatform = RuntimeInformation.OSDescription;
            DetectedRuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription;

#if NETFRAMEWORK
            if (_entryAssembly == null)
            {
                var appDomain = AppDomain.CurrentDomain.SetupInformation;
                var configFile = appDomain.ConfigurationFile;
                DetectedRuntimeFramework = RuntimeFramework.DotNetFramework;
                framework = appDomain.TargetFrameworkName;
            }
#endif
            // if we have a known entry assembly, use that as it may be more reliable
            if (framework == null)
                framework = _entryAssembly?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
            if (framework?.Contains(".NETCoreApp") == true) // ".NETCoreApp,Version=v2.1"
                DetectedRuntimeFramework = RuntimeFramework.DotNetCore;
            if (framework?.Contains(".NETFramework") == true) // ".NETFramework,Version=v4.8"
                DetectedRuntimeFramework = RuntimeFramework.DotNetFramework;
        }

        public enum RuntimeFramework
        {
            /// <summary>
            /// Runtime framework not detected
            /// </summary>
            Unknown,
            /// <summary>
            /// .Net Core
            /// </summary>
            DotNetCore,
            /// <summary>
            /// .Net Framework
            /// </summary>
            DotNetFramework
        }
    }
}
