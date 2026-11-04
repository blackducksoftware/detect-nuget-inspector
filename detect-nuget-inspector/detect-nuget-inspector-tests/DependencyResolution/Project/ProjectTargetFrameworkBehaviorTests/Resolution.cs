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
    [TestMethod]
    public void LocalFolderFeedSupportsFrameworkSpecificDependencyLookups()
    {
        string feedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(feedDirectory);
        try
        {
            CreatePackage(
                feedDirectory,
                "FrameworkProbe",
                "1.0.0",
                ("net8.0", "Net8.Dependency"),
                ("net48", "Net48.Dependency"));
            CreatePackage(feedDirectory, "Net8.Dependency", "1.0.0");
            CreatePackage(feedDirectory, "Net48.Dependency", "1.0.0");
            var service = new NugetSearchService(feedDirectory, null!);
            var identity = new PackageIdentity("FrameworkProbe", NuGetVersion.Parse("1.0.0"));

            Assert.IsNotNull(service.FindBestPackage("FrameworkProbe", VersionRange.Parse("[1.0.0]")));
            var net8Dependencies = service.DependenciesForPackage(identity, NuGetFramework.Parse("net8.0")).ToList();
            var net48Dependencies = service.DependenciesForPackage(identity, NuGetFramework.Parse("net48")).ToList();
            var net8Tree = new NugetTreeResolver(service);
            net8Tree.Add(new NugetDependency("FrameworkProbe", VersionRange.Parse("[1.0.0]"), NuGetFramework.Parse("net8.0")));

            TestContext.WriteLine($"Local source: {feedDirectory}");
            TestContext.WriteLine($"net8.0 dependencies: {string.Join(", ", net8Dependencies.Select(dependency => dependency.Id))}");
            TestContext.WriteLine($"net48 dependencies: {string.Join(", ", net48Dependencies.Select(dependency => dependency.Id))}");
            TestContext.WriteLine($"net8.0 tree packages: {string.Join(", ", net8Tree.GetPackageList().Select(package => package.PackageId.Name))}");
            CollectionAssert.AreEqual(new[] { "Net8.Dependency" }, net8Dependencies.Select(dependency => dependency.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "Net48.Dependency" }, net48Dependencies.Select(dependency => dependency.Id).ToArray());
            CollectionAssert.AreEquivalent(new[] { "FrameworkProbe", "Net8.Dependency" }, net8Tree.GetPackageList().Select(package => package.PackageId.Name).ToArray());
        }
        finally
        {
            Directory.Delete(feedDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void NullTreeFailureTrackerUsesExistingLookupLogging()
    {
        string feedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(feedDirectory);
        try
        {
            CreateResolverFeed(feedDirectory);
            var service = new NugetSearchService(feedDirectory, null!);
            var tree = new NugetTreeResolver(service);
            TextWriter originalOutput = Console.Out;
            using var capturedOutput = new StringWriter();
            try
            {
                Console.SetOut(capturedOutput);
                tree.Add(new NugetDependency("FrameworkProbe", VersionRange.Parse("[1.0.0]")));
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            StringAssert.Contains(capturedOutput.ToString(), "A dependency resource was unable to load for package:");
            StringAssert.Contains(capturedOutput.ToString(), "Target framework: null");
            Assert.AreEqual(1, tree.GetPackageList().Count(package => package.PackageId.Name == "FrameworkProbe"));
        }
        finally
        {
            Directory.Delete(feedDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ProjectFileResolversUseValidFrameworksAndMergeTheGraphs()
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
            var service = new NugetSearchService(feedDirectory, null!);
            var cases = new[]
            {
                (Name: "single TFM", Properties: "<TargetFramework>net8.0</TargetFramework>", Children: new[] { "Net8.Dependency" }, PrrFailures: 0, PxrFailures: 0),
                (Name: "multiple TFMs", Properties: "<TargetFrameworks>net8.0;net48</TargetFrameworks>", Children: new[] { "Net8.Dependency", "Net48.Dependency" }, PrrFailures: 0, PxrFailures: 0),
                (Name: "property TFM", Properties: "<MyTfm>net8.0</MyTfm><TargetFramework>$(MyTfm)</TargetFramework>", Children: new[] { "Net8.Dependency" }, PrrFailures: 0, PxrFailures: 1),
                (Name: "unsupported TFM", Properties: "<TargetFramework>foo</TargetFramework>", Children: Array.Empty<string>(), PrrFailures: 1, PxrFailures: 1),
                (Name: "missing TFM", Properties: "", Children: Array.Empty<string>(), PrrFailures: 1, PxrFailures: 1),
                (Name: "multi-TFM with invalid value", Properties: "<TargetFrameworks>net8.0;foo</TargetFrameworks>", Children: new[] { "Net8.Dependency" }, PrrFailures: 0, PxrFailures: 0)
            };

            foreach (bool useXmlResolver in new[] { false, true })
            {
                foreach (var testCase in cases)
                {
                    string caseDirectory = Path.Combine(temporaryRoot, (useXmlResolver ? "PXR-" : "PRR-") + testCase.Name.Replace(' ', '-'));
                    Directory.CreateDirectory(caseDirectory);
                    string projectPath = WriteProject(caseDirectory, testCase.Properties);
                    var failedPackages = new HashSet<string>();
                    TextWriter originalOutput = Console.Out;
                    using var capturedOutput = new StringWriter();
                    DependencyResult result;
                    try
                    {
                        Console.SetOut(capturedOutput);
                        result = useXmlResolver
                            ? new ProjectXmlResolver(projectPath, service, "NONE", failedPackages).Process()
                            : new ProjectReferenceResolver(projectPath, service, "NONE", failedPackages).Process();
                    }
                    finally
                    {
                        Console.SetOut(originalOutput);
                    }

                    string resolverName = useXmlResolver ? "PXR" : "PRR";
                    int expectedFailures = useXmlResolver ? testCase.PxrFailures : testCase.PrrFailures;
                    TestContext.WriteLine($"{resolverName} {testCase.Name}: packages={result.Packages.Count}, failures={failedPackages.Count}");
                    Assert.IsTrue(result.Success, resolverName + " " + testCase.Name);
                    var rootPackages = result.Packages.Where(package => package.PackageId.Name == "FrameworkProbe").ToArray();
                    Assert.AreEqual(1, rootPackages.Length, resolverName + " " + testCase.Name + " root count");
                    string[] expectedChildren = useXmlResolver && testCase.Name == "property TFM"
                        ? Array.Empty<string>()
                        : testCase.Children;
                    CollectionAssert.AreEquivalent(expectedChildren, rootPackages[0].Dependencies.Select(dependency => dependency.Name).ToArray(), resolverName + " " + testCase.Name + " child links");
                    Assert.AreEqual(expectedFailures, failedPackages.Count, resolverName + " " + testCase.Name + " failed lookups");

                    if (expectedFailures == 1)
                    {
                        Assert.AreEqual(1, failedPackages.Count(identity => identity.StartsWith("FrameworkProbe.", StringComparison.Ordinal)));
                        string failureLog = "Unable to resolve transitive dependencies for FrameworkProbe 1.0.0 (TFM: null).";
                        Assert.AreEqual(1, capturedOutput.ToString().Split(failureLog).Length - 1, resolverName + " " + testCase.Name + " failure log");
                    }
                    if (testCase.Name == "unsupported TFM")
                    {
                        StringAssert.Contains(capturedOutput.ToString(), "Ignoring unsupported target framework 'foo'.");
                    }
                    if (useXmlResolver && testCase.Name == "property TFM")
                    {
                        StringAssert.Contains(capturedOutput.ToString(), "Ignoring unsupported target framework '$(MyTfm)'.");
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false, DisplayName = "PRR")]
    [DataRow(true, DisplayName = "PXR")]
    public void ProjectFileResolversExpandSharedPackageForEachFramework(bool useXmlResolver)
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
            CreatePackage(feedDirectory, "SharedRoot", "1.0.0", ("net48", "SharedPackage"), ("net8.0", "SharedPackage"));
            CreatePackage(feedDirectory, "SharedPackage", "1.0.0", ("net48", "Net48Only"), ("net8.0", "Net8Only"));
            CreatePackage(feedDirectory, "Net48Only", "1.0.0");
            CreatePackage(feedDirectory, "Net8Only", "1.0.0");
            var service = new NugetSearchService(feedDirectory, null!);

            string resolverName = useXmlResolver ? "PXR" : "PRR";
            string projectPath = WriteProjectWithReferences(temporaryRoot, "<TargetFrameworks>net48;net8.0</TargetFrameworks>", "SharedRoot");
            var failedPackages = new HashSet<string>();
            DependencyResult result = useXmlResolver
                ? new ProjectXmlResolver(projectPath, service, "NONE", failedPackages).Process()
                : new ProjectReferenceResolver(projectPath, service, "NONE", failedPackages).Process();

            var shared = result.Packages.Single(package => package.PackageId.Name == "SharedPackage");
            string[] children = shared.Dependencies.Select(dependency => dependency.Name).ToArray();
            TestContext.WriteLine($"{resolverName} SharedPackage children: {string.Join(", ", children)}");
            CollectionAssert.AreEquivalent(new[] { "Net48Only", "Net8Only" }, children, resolverName + " SharedPackage children");
            Assert.AreEqual(0, failedPackages.Count, resolverName + " failed lookups");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false, DisplayName = "PRR")]
    [DataRow(true, DisplayName = "PXR")]
    public void ProjectFileResolversKeepDirectReferencesInCrossFrameworkCycle(bool useXmlResolver)
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
            CreatePackage(feedDirectory, "CycleA", "1.0.0", ("net8.0", "CycleB"));
            CreatePackage(feedDirectory, "CycleB", "1.0.0", ("net48", "CycleA"));
            var service = new NugetSearchService(feedDirectory, null!);

            string resolverName = useXmlResolver ? "PXR" : "PRR";
            string projectPath = WriteProjectWithReferences(temporaryRoot, "<TargetFrameworks>net48;net8.0</TargetFrameworks>", "CycleA", "CycleB");
            DependencyResult result = useXmlResolver
                ? new ProjectXmlResolver(projectPath, service, "NONE", new HashSet<string>()).Process()
                : new ProjectReferenceResolver(projectPath, service, "NONE", new HashSet<string>()).Process();

            string[] direct = result.Dependencies.Select(dependency => dependency.Name).ToArray();
            TestContext.WriteLine($"{resolverName} direct dependencies: [{string.Join(", ", direct)}]");
            CollectionAssert.AreEquivalent(new[] { "CycleA", "CycleB" }, direct, resolverName + " direct dependencies");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReviewDirectReferenceAlsoTransitiveKeepsProjectEdge(bool useXmlResolver)
    {
        var fixture = RunReviewResolver(useXmlResolver,
            "<ItemGroup><PackageReference Include=\"OverlapA\" Version=\"1.0.0\" /><PackageReference Include=\"OverlapB\" Version=\"1.0.0\" /></ItemGroup>", feed =>
            {
                CreatePackage(feed, "OverlapA", "1.0.0", ("net48", "OverlapB"), ("net8.0", "OverlapB"));
                CreatePackage(feed, "OverlapB", "1.0.0");
            });
        Assert.IsTrue(fixture.Result.Packages.Single(package => package.PackageId.Name == "OverlapA")
            .Dependencies.Any(package => package.Name == "OverlapB"));
        CollectionAssert.AreEquivalent(new[] { "OverlapA", "OverlapB" },
            fixture.Result.Dependencies.Select(package => package.Name).ToArray());
    }
}
