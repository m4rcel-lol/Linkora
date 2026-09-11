using System;
using System.Reflection;

namespace WLMClient.Config
{
    /// <summary>
    /// The application's version, as shown at the bottom of the main window. Taken from the
    /// assembly, so it follows whatever the build stamped in rather than a number kept by hand.
    /// </summary>
    static class AppVersion
    {
        private static string display;

        /// <summary>Something like "1.1.0" or, for a stamped release, "1.1.0 (build 20260911)".</summary>
        public static string Display
        {
            get
            {
                if (display == null)
                {
                    display = Resolve();
                }

                return display;
            }
        }

        private static string Resolve()
        {
            try
            {
                Assembly assembly = typeof(AppVersion).Assembly;

                AssemblyInformationalVersionAttribute informational =
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

                string value = informational == null ? null : informational.InformationalVersion;

                if (string.IsNullOrWhiteSpace(value))
                {
                    Version version = assembly.GetName().Version;

                    return version == null ? "" : version.ToString(3);
                }

                // The SDK appends "+<source revision>" to the informational version; a build
                // stamp is more useful to show than a commit hash.
                int plus = value.IndexOf('+');

                if (plus < 0)
                {
                    return value;
                }

                string baseVersion = value.Substring(0, plus);
                string suffix = value.Substring(plus + 1);

                const string BuildPrefix = "build.";

                if (suffix.StartsWith(BuildPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string build = suffix.Substring(BuildPrefix.Length);

                    // Anything after the build stamp itself (a commit hash, say) is not useful here.
                    int separator = build.IndexOf('.');

                    if (separator > 0)
                    {
                        build = build.Substring(0, separator);
                    }

                    return baseVersion + " (build " + build + ")";
                }

                return baseVersion;
            }
            catch
            {
                return "";
            }
        }
    }
}
