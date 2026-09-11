using System;
using System.Collections.Specialized;
using System.IO;
using System.Xml.Linq;

namespace WLMShared
{
    /// <summary>
    /// Minimal, cross platform stand in for System.Configuration.ConfigurationManager. The original
    /// projects declared &lt;appSettings file="Messenger.config"/&gt; in App.config, so the settings
    /// actually live in Messenger.config next to the executable; that is what this reads.
    /// </summary>
    public static class ConfigurationManager
    {
        private const string SettingsFileName = "Messenger.config";

        private static readonly object locker = new object();
        private static NameValueCollection appSettings;

        public static NameValueCollection AppSettings
        {
            get
            {
                lock (locker)
                {
                    if (appSettings == null)
                    {
                        appSettings = Load();
                    }

                    return appSettings;
                }
            }
        }

        /// <summary>Forces the settings file to be read again on the next access.</summary>
        public static void Reload()
        {
            lock (locker)
            {
                appSettings = null;
            }
        }

        /// <summary>Absolute path of the settings file this process will read.</summary>
        public static string SettingsFilePath
        {
            get { return Path.Combine(AppContext.BaseDirectory, SettingsFileName); }
        }

        private static NameValueCollection Load()
        {
            NameValueCollection settings = new NameValueCollection();

            string path = SettingsFilePath;
            if (!File.Exists(path))
            {
                // Also allow running straight out of a source checkout.
                string fallback = Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName);
                if (!File.Exists(fallback))
                {
                    return settings;
                }

                path = fallback;
            }

            try
            {
                XDocument document = XDocument.Load(path);
                if (document.Root == null)
                {
                    return settings;
                }

                // Accept either <appSettings> as the root or nested inside <configuration>.
                XElement appSettingsElement = document.Root.Name.LocalName == "appSettings"
                    ? document.Root
                    : document.Root.Element("appSettings");

                if (appSettingsElement == null)
                {
                    return settings;
                }

                foreach (XElement add in appSettingsElement.Elements("add"))
                {
                    XAttribute key = add.Attribute("key");
                    XAttribute value = add.Attribute("value");

                    if (key != null)
                    {
                        settings[key.Value] = value == null ? string.Empty : value.Value;
                    }
                }
            }
            catch (Exception exception)
            {
                throw new ConfigurationErrorsException(
                    "Unable to read " + path + ": " + exception.Message, exception);
            }

            return settings;
        }
    }

    /// <summary>Raised when the settings file exists but cannot be parsed.</summary>
    public class ConfigurationErrorsException : Exception
    {
        public ConfigurationErrorsException(string message) : base(message) { }
        public ConfigurationErrorsException(string message, Exception inner) : base(message, inner) { }
    }
}
