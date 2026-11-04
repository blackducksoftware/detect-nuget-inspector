using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    internal static class TargetFrameworkHelper
    {
        internal static List<NuGetFramework> Parse(string targetFramework, string targetFrameworks)
        {
            // NuGet dependency groups vary by TFM, so fallback lookups need each declared framework.
            var frameworks = new List<NuGetFramework>();
            AddFrameworks(targetFramework, frameworks);
            AddFrameworks(targetFrameworks, frameworks);
            return frameworks;
        }

        private static void AddFrameworks(string value, List<NuGetFramework> frameworks)
        {
            foreach (string entry in (value ?? string.Empty).Split(';'))
            {
                string trimmedEntry = entry.Trim();
                if (trimmedEntry.Length == 0)
                {
                    Console.WriteLine("Ignoring empty target framework value.");
                    continue;
                }

                // Trim and reject unsupported values: NuGet can parse inputs like "foo" as an unsupported v0.0 TFM.
                NuGetFramework framework = NuGetFramework.Parse(trimmedEntry);
                if (framework.IsUnsupported)
                {
                    Console.WriteLine($"Ignoring unsupported target framework '{trimmedEntry}'.");
                    continue;
                }

                if (!frameworks.Contains(framework))
                {
                    frameworks.Add(framework);
                }
            }
        }
    }
}