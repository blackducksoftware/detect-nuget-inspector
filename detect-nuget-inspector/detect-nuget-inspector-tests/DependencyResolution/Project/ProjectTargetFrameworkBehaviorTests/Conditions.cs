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
    public void ProjectReferenceResolverKeepsFrameworkDependentVersionsDirect()
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
            CreatePackage(feedDirectory, "ConditionalVersionA", "1.0.0", ("net48", "Old48Child"), ("net8.0", "Old8Child"));
            CreatePackage(feedDirectory, "ConditionalVersionA", "2.0.0", ("net48", "New48Child"), ("net8.0", "New8Child"));
            foreach (string child in new[] { "Old48Child", "Old8Child", "New48Child", "New8Child" })
            {
                CreatePackage(feedDirectory, child, "1.0.0");
            }
            string projectPath = Path.Combine(temporaryRoot, "fixture.csproj");
            File.WriteAllText(projectPath,
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<TargetFrameworks>net48;net8.0</TargetFrameworks></PropertyGroup><ItemGroup>" +
                "<PackageReference Include=\"ConditionalVersionA\" Version=\"1.0.0\" Condition=\"'$(TargetFramework)' == 'net48'\" />" +
                "<PackageReference Include=\"ConditionalVersionA\" Version=\"2.0.0\" Condition=\"'$(TargetFramework)' == 'net8.0'\" />" +
                "</ItemGroup></Project>");

            var service = new NugetSearchService(feedDirectory, null!);
            var result = new ProjectReferenceResolver(projectPath, service, "NONE", new HashSet<string>()).Process();
            Assert.IsTrue(result.Success);
            string[] resolvedVersions = result.Packages
                .Where(package => package.PackageId.Name == "ConditionalVersionA")
                .Select(package => package.PackageId.Version).ToArray();
            string[] directVersions = result.Dependencies
                .Where(dependency => dependency.Name == "ConditionalVersionA")
                .Select(dependency => dependency.Version).ToArray();
            TestContext.WriteLine($"Resolved versions: {string.Join(", ", resolvedVersions)}");
            TestContext.WriteLine($"Direct versions: {string.Join(", ", directVersions)}");
            CollectionAssert.AreEquivalent(new[] { "1.0.0", "2.0.0" }, resolvedVersions);
            CollectionAssert.AreEquivalent(new[] { "1.0.0", "2.0.0" }, directVersions);
            string[] children = result.Packages.Where(package => package.PackageId.Name != "ConditionalVersionA")
                .Select(package => package.PackageId.Name).ToArray();
            TestContext.WriteLine($"Resolved children: {string.Join(", ", children)}");
            CollectionAssert.AreEquivalent(new[] { "Old48Child", "New8Child" }, children);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReviewConditionalReferenceResolvesOnlyItsFramework(bool conditionOnGroup)
    {
        string condition = "Condition=\"'$(TargetFramework)' == 'net48'\"";
        string items = conditionOnGroup
            ? $"<ItemGroup {condition}><PackageReference Include=\"ConditionalA\" Version=\"1.0.0\" /></ItemGroup>"
            : $"<ItemGroup><PackageReference Include=\"ConditionalA\" Version=\"1.0.0\" {condition} /></ItemGroup>";
        var fixture = RunReviewResolver(false, items, feed =>
        {
            CreatePackage(feed, "ConditionalA", "1.0.0", ("net48", "Net48Child"), ("net8.0", "Net8Child"));
            CreatePackage(feed, "Net48Child", "1.0.0");
            CreatePackage(feed, "Net8Child", "1.0.0");
        });
        CollectionAssert.AreEquivalent(new[] { "ConditionalA", "Net48Child" },
            fixture.Result.Packages.Select(package => package.PackageId.Name).ToArray());
        Assert.IsTrue(fixture.Result.Dependencies.Any(package => package.Name == "ConditionalA"));
    }

    [TestMethod]
    public void ReviewNonFrameworkConditionRetainsReferenceWithOneWarning()
    {
        var fixture = RunReviewResolver(false,
            "<ItemGroup><PackageReference Include=\"ConditionalB\" Version=\"1.0.0\" Condition=\"'$(OS)' == 'NoSuchOS'\" /></ItemGroup>", feed =>
            {
                CreatePackage(feed, "ConditionalB", "1.0.0", ("net48", "B48Child"), ("net8.0", "B8Child"));
                CreatePackage(feed, "B48Child", "1.0.0");
                CreatePackage(feed, "B8Child", "1.0.0");
            });
        Assert.IsTrue(fixture.Result.Dependencies.Any(package => package.Name == "ConditionalB"));
        CollectionAssert.AreEquivalent(new[] { "ConditionalB", "B48Child", "B8Child" },
            fixture.Result.Packages.Select(package => package.PackageId.Name).ToArray());
        TestContext.WriteLine("ConditionalB remains direct and both framework children are present.");
        Assert.AreEqual(1, CountLogLines(fixture.Output,
            "PackageReference 'ConditionalB' was excluded by its condition in every target framework evaluation; resolving it for all target frameworks."), fixture.Output);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReviewXmlConditionsLogLimitationWithoutFiltering(bool conditionOnGroup)
    {
        string condition = "Condition=\"'$(TargetFramework)' == 'net48'\"";
        string items = conditionOnGroup
            ? $"<ItemGroup {condition}><PackageReference Include=\"XmlConditionalA\" Version=\"1.0.0\" /></ItemGroup>"
            : $"<ItemGroup><PackageReference Include=\"XmlConditionalA\" Version=\"1.0.0\" {condition} /></ItemGroup>";
        var fixture = RunReviewResolver(true, items, feed =>
        {
            CreatePackage(feed, "XmlConditionalA", "1.0.0", ("net48", "Xml48Child"), ("net8.0", "Xml8Child"));
            CreatePackage(feed, "Xml48Child", "1.0.0");
            CreatePackage(feed, "Xml8Child", "1.0.0");
        });
        CollectionAssert.AreEquivalent(new[] { "XmlConditionalA", "Xml48Child", "Xml8Child" },
            fixture.Result.Packages.Select(package => package.PackageId.Name).ToArray());
        Assert.AreEqual(1, CountLogLines(fixture.Output,
            "Conditions on PackageReference items are not evaluated in the XML fallback; conditional references are resolved for all target frameworks."), fixture.Output);
    }

    [TestMethod]
    public void ReviewFailedFrameworkEvaluationKeepsOtherFrameworkFiltered()
    {
        var fixture = RunReviewResolver(false,
            "<ItemGroup>" +
            "<PackageReference Include=\"FailedFrameworkOnly\" Version=\"1.0.0\" Condition=\"'$(TargetFramework)' == 'net48'\" />" +
            "<PackageReference Include=\"HealthyFrameworkOnly\" Version=\"1.0.0\" Condition=\"'$(TargetFramework)' == 'net8.0'\" />" +
            "</ItemGroup><Import Project=\"missing.targets\" Condition=\"'$(TargetFramework)' == 'net48'\" />", feed =>
            {
                CreatePackage(feed, "FailedFrameworkOnly", "1.0.0", ("net48", "Failed48Child"), ("net8.0", "Excluded8Child"));
                CreatePackage(feed, "HealthyFrameworkOnly", "1.0.0", ("net48", "Conservative48Child"), ("net8.0", "Healthy8Child"));
                foreach (string child in new[] { "Failed48Child", "Excluded8Child", "Conservative48Child", "Healthy8Child" })
                {
                    CreatePackage(feed, child, "1.0.0");
                }
            }, inspectProject: true);

        CollectionAssert.AreEquivalent(new[]
        {
            "FailedFrameworkOnly", "HealthyFrameworkOnly", "Failed48Child", "Conservative48Child", "Healthy8Child"
        }, fixture.Result.Packages.Select(package => package.PackageId.Name).ToArray());
        CollectionAssert.AreEquivalent(new[] { "FailedFrameworkOnly", "HealthyFrameworkOnly" },
            fixture.Result.Dependencies.Select(package => package.Name).ToArray());
        Assert.AreEqual(1, fixture.Output.Split('\n').Count(line => line.StartsWith(
            "Unable to evaluate target framework 'net48'; resolving all reported references for this framework:", StringComparison.Ordinal)), fixture.Output);
        StringAssert.Contains(fixture.Output, "Reference resolver succeeded.");
        Assert.IsFalse(fixture.Output.Contains("Using backup XML resolver."), fixture.Output);
    }

    [TestMethod]
    public void ReviewAmbiguousVersionRetainsReportedReferenceForAllFrameworks()
    {
        var fixture = RunReviewResolver(false,
            "<PropertyGroup><ReviewVersion>1.0.0</ReviewVersion>" +
            "<ReviewVersion Condition=\"'$(TargetFramework)' == 'net8.0'\">2.0.0</ReviewVersion></PropertyGroup>" +
            "<ItemGroup><PackageReference Include=\"AmbiguousA\" Version=\"$(ReviewVersion)\" />" +
            "<PackageReference Include=\"UnaffectedB\" Version=\"1.0.0\" Condition=\"'$(TargetFramework)' == 'net8.0'\" /></ItemGroup>", feed =>
            {
                CreatePackage(feed, "AmbiguousA", "1.0.0", ("net48", "Reported48Child"), ("net8.0", "Reported8Child"));
                CreatePackage(feed, "AmbiguousA", "2.0.0", ("net48", "Changed48Child"), ("net8.0", "Changed8Child"));
                CreatePackage(feed, "UnaffectedB", "1.0.0", ("net48", "ExcludedB48Child"), ("net8.0", "UnaffectedB8Child"));
                foreach (string child in new[] { "Reported48Child", "Reported8Child", "Changed48Child", "Changed8Child", "ExcludedB48Child", "UnaffectedB8Child" })
                {
                    CreatePackage(feed, child, "1.0.0");
                }
            }, inspectProject: true);

        CollectionAssert.AreEquivalent(new[]
        {
            "AmbiguousA", "UnaffectedB", "Reported48Child", "Reported8Child", "UnaffectedB8Child"
        }, fixture.Result.Packages.Select(package => package.PackageId.Name).ToArray());
        CollectionAssert.AreEquivalent(new[] { "AmbiguousA:1.0.0", "UnaffectedB:1.0.0" },
            fixture.Result.Dependencies.Select(package => package.Name + ":" + package.Version).ToArray());
        Assert.AreEqual(1, CountLogLines(fixture.Output,
            "PackageReference 'AmbiguousA' version '1.0.0' could not be matched unambiguously to target framework evaluations; resolving the reported reference for all target frameworks."), fixture.Output);
        StringAssert.Contains(fixture.Output, "Reference resolver succeeded.");
        Assert.IsFalse(fixture.Output.Contains("Using backup XML resolver."), fixture.Output);
    }
}
