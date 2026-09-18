using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    // Shared safe-parse logic for both ProjectXmlResolver and ProjectReferenceResolver.
    // Returns AnyFramework whenever the raw value is missing, an MSBuild expression, or unrecognised.
    internal static class TargetFrameworkParser
    {
        internal static NuGetFramework ParseOrAny(string value)
        {
            if (!string.IsNullOrEmpty(value) && !value.Contains("$"))
            {
                NuGetFramework framework = NuGetFramework.Parse(value);
                if (!framework.IsUnsupported)
                    return framework;
            }
            return NuGetFramework.AnyFramework;
        }
    }
}
