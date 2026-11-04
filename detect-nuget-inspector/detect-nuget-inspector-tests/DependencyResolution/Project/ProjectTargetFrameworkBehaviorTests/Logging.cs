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
    [DataTestMethod]
    [DataRow(false, "1.0.0")]
    [DataRow(true, "1.0.0")]
    [DataRow(false, "(1.0.0,2.0.0)")]
    [DataRow(true, "(1.0.0,2.0.0)")]
    public void ReviewMetadataFailuresLogOnceAndSummarizeBothPackages(bool useXmlResolver, string directRange)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
        string temporaryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string feedDirectory = Path.Combine(temporaryRoot, "feed");
        Directory.CreateDirectory(feedDirectory);
        try
        {
            CreatePackage(feedDirectory, "AvailableRoot", "1.0.0", ("net48", "MissingChild"), ("net8.0", "MissingChild"));
            string projectPath = WriteReviewProject(temporaryRoot,
                $"<ItemGroup><PackageReference Include=\"MissingDirect\" Version=\"{directRange}\" /><PackageReference Include=\"AvailableRoot\" Version=\"1.0.0\" /></ItemGroup>" +
                (useXmlResolver ? "<Import Project=\"missing.targets\" />" : ""));
            string missingPath = Path.Combine(temporaryRoot, "missing.file");
            var options = new ProjectInspectionOptions
            {
                TargetPath = projectPath,
                ProjectName = "ReviewFixture",
                ProjectUniqueId = "ReviewFixture",
                ProjectDirectory = temporaryRoot,
                VersionName = "1.0.0",
                PackagesConfigPath = missingPath,
                ProjectJsonPath = missingPath,
                ProjectJsonLockPath = missingPath,
                ProjectAssetsJsonPath = missingPath,
                DirectoryPackagesPropsPath = missingPath
            };
            var service = new NugetSearchService(feedDirectory, null!);
            var inspector = new ProjectInspector(options, service);
            TextWriter originalOutput = Console.Out;
            using var output = new StringWriter();
            try
            {
                Console.SetOut(output);
                var container = inspector.GetContainer();
                Assert.IsTrue(container.Dependencies.Any(package => package.Name == "MissingDirect" && package.Version == "1.0.0"));
                Assert.IsFalse(container.Packages.Any(package => package.PackageId.Name == "MissingChild"));
                Assert.IsFalse(container.Packages.Any(package => package.Dependencies.Any(child => child.Name == "MissingChild")));
            }
            finally
            {
                Console.SetOut(originalOutput);
            }
            string logs = output.ToString();
            TestContext.WriteLine(logs);
            StringAssert.Contains(logs, useXmlResolver ? "Using backup XML resolver." : "Reference resolver succeeded.");
            Assert.AreEqual(1, CountLogLines(logs,
                "Transitive dependency information could not be resolved for 2 package(s)."), "Summary count: " + logs);
            Assert.AreEqual(1, CountLogLines(logs,
                $"Nuget was unable to find the package 'MissingDirect' with version range '{VersionRange.Parse(directRange)}', assuming it is using version '1.0.0'"), logs);
            Assert.AreEqual(1, CountLogLines(logs, $"Unable to find package for 'MissingChild' version '{VersionRange.Parse("[1.0.0]")}'"), logs);

            var failures = new HashSet<string>();
            DependencyResult result = useXmlResolver
                ? new ProjectXmlResolver(projectPath, service, "NONE", failures).Process()
                : new ProjectReferenceResolver(projectPath, service, "NONE", failures).Process();
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEquivalent(new[]
            {
                "MissingDirect|" + VersionRange.Parse(directRange),
                "MissingChild|" + VersionRange.Parse("[1.0.0]")
            }, failures.ToArray());
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ProjectInspectorLogsOneSummaryForStep6Failures()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        string temporaryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string feedDirectory = Path.Combine(temporaryRoot, "feed");
        Directory.CreateDirectory(feedDirectory);
        try
        {
            CreateResolverFeed(feedDirectory);
            string projectPath = WriteProject(temporaryRoot, "");
            string missingPath = Path.Combine(temporaryRoot, "missing.file");
            var options = new ProjectInspectionOptions
            {
                TargetPath = projectPath,
                ProjectName = "FrameworkProbeProject",
                ProjectUniqueId = "FrameworkProbeProject",
                ProjectDirectory = temporaryRoot,
                VersionName = "1.0.0",
                PackagesConfigPath = missingPath,
                ProjectJsonPath = missingPath,
                ProjectJsonLockPath = missingPath,
                ProjectAssetsJsonPath = missingPath,
                DirectoryPackagesPropsPath = missingPath
            };
            var inspector = new ProjectInspector(options, new NugetSearchService(feedDirectory, null!));
            const string summary = "Transitive dependency information could not be resolved for 1 package(s).";
            TextWriter originalOutput = Console.Out;
            using var capturedOutput = new StringWriter();
            try
            {
                Console.SetOut(capturedOutput);
                inspector.GetContainer();
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            string output = capturedOutput.ToString();
            Assert.AreEqual(1, output.Split(summary).Length - 1, output);
            Assert.IsTrue(output.IndexOf(summary, StringComparison.Ordinal) < output.IndexOf("Finished processing project", StringComparison.Ordinal), output);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [TestMethod]
    public void PackagesConfigKeepsItsExistingOutputAndLookupLogging()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string feedDirectory = Path.Combine(temporaryRoot, "feed");
        Directory.CreateDirectory(feedDirectory);
        try
        {
            CreateResolverFeed(feedDirectory);
            string packagesConfigPath = Path.Combine(temporaryRoot, "packages.config");
            File.WriteAllText(packagesConfigPath,
                "<packages><package id=\"FrameworkProbe\" version=\"1.0.0\" targetFramework=\"net8.0\" /></packages>");
            using (var packagesConfigStream = File.OpenRead(packagesConfigPath))
            {
                var packageReference = new PackagesConfigReader(packagesConfigStream).GetPackages().Single();
                TestContext.WriteLine($"packages.config framework parsed: [{packageReference.TargetFramework.Framework}]");
            }

            var resolver = new PackagesConfigResolver(
                packagesConfigPath,
                new NugetSearchService(feedDirectory, null!),
                "NONE");
            TextWriter originalOutput = Console.Out;
            using var capturedOutput = new StringWriter();
            DependencyResult result;
            try
            {
                Console.SetOut(capturedOutput);
                result = resolver.Process();
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            Assert.AreEqual(1, result.Dependencies.Count);
            Assert.IsTrue(result.Dependencies.Any(dependency => dependency.Name == "FrameworkProbe" && dependency.Version == "1.0.0"));
            CollectionAssert.AreEquivalent(
                new[] { "FrameworkProbe" },
                result.Packages.Select(package => package.PackageId.Name).ToArray(),
                $"Captured packages.config logs: {capturedOutput}");
            Assert.IsTrue(capturedOutput.ToString().Contains("Have not looked up package 'frameworkprobe', using metadata resources."));
            Assert.IsFalse(capturedOutput.ToString().Contains("Transitive dependency information could not be resolved"));
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }
}
