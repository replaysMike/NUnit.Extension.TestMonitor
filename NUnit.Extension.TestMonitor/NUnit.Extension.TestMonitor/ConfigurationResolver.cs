using Microsoft.Extensions.Configuration;
using System;
using System.Configuration;
using System.IO;

namespace NUnit.Extension.TestMonitor
{
    public class ConfigurationResolver
    {
        private const string DotNetCoreSettingsFilename = "appsettings.json";
        private const string ConfigurationSectionName = "TestMonitor";

        /// <summary>
        /// Information about the detected runtime
        /// </summary>
        public RuntimeDetection RuntimeDetection { get; }

        public ConfigurationResolver(RuntimeDetection runtimeDetection)
        {
            RuntimeDetection = runtimeDetection;
        }

        /// <summary>
        /// Resolve the extension configuration if available
        /// </summary>
        /// <returns></returns>
        public Configuration GetConfiguration()
        {
            var defaultConfiguration = new Configuration();
            switch (RuntimeDetection.DetectedRuntimeFramework)
            {
                case RuntimeDetection.RuntimeFramework.DotNetCore:
                    return GetDotNetCoreConfiguration() ?? defaultConfiguration;
                case RuntimeDetection.RuntimeFramework.DotNetFramework:
                    return GetDotNetFrameworkConfiguration(defaultConfiguration);
            }
            return defaultConfiguration;
        }

        private Configuration GetDotNetCoreConfiguration(string appSettingsJson = DotNetCoreSettingsFilename, string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(path, appSettingsJson);
            if (!File.Exists(filePath))
                return null;
            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(path)
                .AddJsonFile(appSettingsJson, optional: true);
            var configurationRoot = builder.Build();
            var configuration = configurationRoot
                .GetSection(ConfigurationSectionName)
                .Get<Configuration>();
            return configuration;
        }

        private Configuration GetDotNetFrameworkConfiguration(Configuration defaultConfiguration)
        {
            if (!Enum.TryParse<EventEmitTypes>(ConfigurationManager.AppSettings["EventEmitType"], out var eventEmitType))
                eventEmitType = defaultConfiguration.EventEmitType;
            if (!Enum.TryParse<EventFormatTypes>(ConfigurationManager.AppSettings["EventFormat"], out var eventFormat))
                eventFormat = defaultConfiguration.EventFormat;
            var configuration = new Configuration
            {
                EventEmitType = eventEmitType,
                EventFormat = eventFormat,
                EventsLogFile = ConfigurationManager.AppSettings["EventsLogFile"] ?? defaultConfiguration.EventsLogFile
            };
            return configuration;
        }
    }
}
