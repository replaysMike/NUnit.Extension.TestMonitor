using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;

namespace NUnit.Extension.TestMonitor
{
    public class ConfigurationResolver
    {
        private const string SettingsFilename = "appsettings.json";
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
                    return GetDotNetFrameworkConfiguration() ?? defaultConfiguration;
            }
            return defaultConfiguration;
        }

        private Configuration GetDotNetCoreConfiguration(string appSettingsJson = SettingsFilename, string path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(path, appSettingsJson);
            if (!File.Exists(filePath))
                return null;
            var builder = new ConfigurationBuilder()
                .SetBasePath(path)
                .AddJsonFile(appSettingsJson, optional: true);
            var configurationRoot = builder.Build();
            var configuration = configurationRoot
                .GetSection(ConfigurationSectionName)
                .Get<Configuration>();
            return configuration;
        }

        private Configuration GetDotNetFrameworkConfiguration(string appSettingsJson = SettingsFilename, string path = null)
        {
            // resolve the configuration a little differently since we don't have a ConfigurationBuilder available
            if (string.IsNullOrEmpty(path))
                path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(path, appSettingsJson);

            if (!File.Exists(filePath))
                filePath = Path.Combine(Directory.GetCurrentDirectory(), appSettingsJson);
            if (!File.Exists(filePath))
                return null;

            var configString = File.ReadAllText(filePath);
            // here we use a container for the configuration to match the json format since we don't have a GetSection() capability
            if (string.IsNullOrEmpty(configString))
                return null;

            var config = JsonConvert.DeserializeObject<TestMonitorConfiguration>(configString).TestMonitor;
            return config;
        }
    }
}
