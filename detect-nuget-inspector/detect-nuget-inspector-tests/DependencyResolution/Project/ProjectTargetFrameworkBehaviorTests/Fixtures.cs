using Microsoft.Build.Locator;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.PackagesConfig;
using Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Xml;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project.Test;
public partial class ProjectTargetFrameworkBehaviorTests
{
    private static int CountLogLines(string output, string message)
    {
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Count(line => line == message);
    }

    private static string WriteReviewProject(string directory, string items)
    {
        string path = Path.Combine(directory, "fixture.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net48;net8.0</TargetFrameworks></PropertyGroup>" + items + "</Project>");
        return path;
    }

    private static (DependencyResult Result, string Output) RunReviewResolver(bool useXmlResolver, string items, Action<string> createFeed, bool inspectProject = false)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string feed = Path.Combine(directory, "feed");
        Directory.CreateDirectory(feed);
        try
        {
            createFeed(feed);
            string projectPath = WriteReviewProject(directory, items);
            var service = new NugetSearchService(feed, null!);
            using var output = new StringWriter();
            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(output);
                var failures = new HashSet<string>();
                DependencyResult result;
                if (inspectProject)
                {
                    string missingPath = Path.Combine(directory, "missing.file");
                    var inspector = new ProjectInspector(new ProjectInspectionOptions
                    {
                        TargetPath = projectPath,
                        ProjectName = "ReviewFixture",
                        ProjectUniqueId = "ReviewFixture",
                        ProjectDirectory = directory,
                        VersionName = "1.0.0",
                        PackagesConfigPath = missingPath,
                        ProjectJsonPath = missingPath,
                        ProjectJsonLockPath = missingPath,
                        ProjectAssetsJsonPath = missingPath,
                        DirectoryPackagesPropsPath = missingPath
                    }, service);
                    var container = inspector.GetContainer();
                    result = new DependencyResult { Packages = container.Packages, Dependencies = container.Dependencies };
                }
                else
                {
                    result = useXmlResolver
                        ? new ProjectXmlResolver(projectPath, service, "NONE", failures).Process()
                        : new ProjectReferenceResolver(projectPath, service, "NONE", failures).Process();
                }
                Assert.IsTrue(result.Success);
                return (result, output.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CreateResolverFeed(string feedDirectory)
    {
        CreatePackage(
            feedDirectory,
            "FrameworkProbe",
            "1.0.0",
            ("net8.0", "Net8.Dependency"),
            ("net48", "Net48.Dependency"));
        CreatePackage(feedDirectory, "Net8.Dependency", "1.0.0");
        CreatePackage(feedDirectory, "Net48.Dependency", "1.0.0");
    }

    private static string WriteProject(string projectDirectory, string projectProperties)
    {
        string projectPath = Path.Combine(projectDirectory, "fixture.csproj");
        File.WriteAllText(projectPath,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{projectProperties}</PropertyGroup>" +
            "<ItemGroup><PackageReference Include=\"FrameworkProbe\" Version=\"1.0.0\" /></ItemGroup></Project>");
        return projectPath;
    }

    private static string WriteProjectWithReferences(string projectDirectory, string projectProperties, params string[] packageIds)
    {
        string projectPath = Path.Combine(projectDirectory, "fixture.csproj");
        string references = string.Concat(packageIds.Select(id => $"<PackageReference Include=\"{id}\" Version=\"1.0.0\" />"));
        File.WriteAllText(projectPath,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{projectProperties}</PropertyGroup>" +
            $"<ItemGroup>{references}</ItemGroup></Project>");
        return projectPath;
    }

    private static void CreatePackage(
        string feedDirectory,
        string packageId,
        string version,
        params (string Framework, string Dependency)[] dependencyGroups)
    {
        CreatePackageWithGroups(feedDirectory, packageId, version,
            dependencyGroups.Select(group => (group.Framework, group.Dependency, "[1.0.0]")).ToArray());
    }

    private static void CreatePackageWithGroups(
        string feedDirectory,
        string packageId,
        string version,
        params (string Framework, string Dependency, string VersionRange)[] dependencyGroups)
    {
        var packageBuilder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = "Framework-specific dependency lookup test package"
        };
        packageBuilder.Authors.Add("NuGet Inspector tests");
        foreach (var group in dependencyGroups)
        {
            packageBuilder.DependencyGroups.Add(new PackageDependencyGroup(
                NuGetFramework.Parse(group.Framework),
                new[] { new PackageDependency(group.Dependency, VersionRange.Parse(group.VersionRange)) }));
        }

        string contentPath = Path.Combine(feedDirectory, $"{packageId}.{version}.txt");
        File.WriteAllText(contentPath, "test package content");
        packageBuilder.Files.Add(new PhysicalPackageFile
        {
            SourcePath = contentPath,
            TargetPath = "content/readme.txt"
        });
        using (var packageStream = File.Create(Path.Combine(feedDirectory, $"{packageId}.{version}.nupkg")))
        {
            packageBuilder.Save(packageStream);
        }
        File.Delete(contentPath);
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrEmpty(value) ? "<unavailable>" : $"[{value}]";
    }

    private static string EvaluateTargetFramework(string projectPath)
    {
        return EvaluateProperty(projectPath, "TargetFramework");
    }

    private static string EvaluateProperty(string projectPath, string propertyName)
    {
        var evaluatedProject = new Microsoft.Build.Evaluation.Project(projectPath);
        string value = evaluatedProject.GetPropertyValue(propertyName);
        Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.UnloadProject(evaluatedProject);
        return value;
    }
}
