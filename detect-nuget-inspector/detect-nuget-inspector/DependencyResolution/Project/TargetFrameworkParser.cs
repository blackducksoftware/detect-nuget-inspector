using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    // Shared safe-parse logic for both ProjectXmlResolver and ProjectReferenceResolver.
    // Returns null when the value is missing, an MSBuild expression, or unrecognised — callers
    // that receive null will not query the NuGet feed for transitives, avoiding false data.
    internal static class TargetFrameworkParser
    {
        internal static NuGetFramework ParseOrNull(string value)
        {
            if (!string.IsNullOrEmpty(value) && !value.Contains("$"))
            {
                NuGetFramework framework = NuGetFramework.Parse(value);
                if (!framework.IsUnsupported)
                    return framework;
            }
            return null;
        }
    }
}
