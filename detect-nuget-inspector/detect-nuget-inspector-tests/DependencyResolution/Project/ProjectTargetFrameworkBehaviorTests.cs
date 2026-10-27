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

[TestClass]
public class ProjectTargetFrameworkBehaviorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NuGetFrameworkParseReportsFrameworkValues()
    {
        var inputs = new[]
        {
            (Input: "net8.0", Framework: ".NETCoreApp", Version: "8.0.0.0", IsUnsupported: false, ShortFolderName: "net8.0"),
            (Input: "net48", Framework: ".NETFramework", Version: "4.8.0.0", IsUnsupported: false, ShortFolderName: "net48"),
            (Input: "netstandard2.0", Framework: ".NETStandard", Version: "2.0.0.0", IsUnsupported: false, ShortFolderName: "netstandard2.0"),
            (Input: "net8.0-windows", Framework: ".NETCoreApp", Version: "8.0.0.0", IsUnsupported: false, ShortFolderName: "net8.0-windows"),
            (Input: "foo", Framework: "Unsupported", Version: "0.0.0.0", IsUnsupported: true, ShortFolderName: "unsupported"),
            (Input: "$(MyTfm)", Framework: "Unsupported", Version: "0.0.0.0", IsUnsupported: true, ShortFolderName: "unsupported"),
            (Input: "", Framework: "Unsupported", Version: "0.0.0.0", IsUnsupported: true, ShortFolderName: "unsupported"),
            (Input: " net8.0 ", Framework: "Unsupported", Version: "0.0.0.0", IsUnsupported: true, ShortFolderName: "unsupported")
        };

        foreach (var expected in inputs)
        {
            try
            {
                NuGetFramework framework = NuGetFramework.Parse(expected.Input);
                TestContext.WriteLine(
                    $"Input=[{expected.Input}] Framework={framework.Framework} Version={framework.Version} " +
                    $"IsUnsupported={framework.IsUnsupported} ShortFolderName={framework.GetShortFolderName()}");
                Assert.AreEqual(expected.Framework, framework.Framework, expected.Input);
                Assert.AreEqual(expected.Version, framework.Version.ToString(), expected.Input);
                Assert.AreEqual(expected.IsUnsupported, framework.IsUnsupported, expected.Input);
                Assert.AreEqual(expected.ShortFolderName, framework.GetShortFolderName(), expected.Input);
            }
            catch (Exception exception)
            {
                TestContext.WriteLine(
                    $"Input=[{expected.Input}] THREW {exception.GetType().FullName}: {exception.Message}");
                Assert.Fail($"NuGetFramework.Parse threw for input [{expected.Input}]: {exception.Message}");
            }
        }
    }

    [TestMethod]
    public void ProjectResolverInputsReportEvaluatedAndRawFrameworkValues()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        string temporaryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        var cases = new[]
        {
            (Name: "Single target framework", Directory: "case1", Project: "<TargetFramework>net8.0</TargetFramework>", Props: (string?)null, Nested: false, Prr: "net8.0", Pxr: "net8.0", Prrs: "", Pxrs: ""),
            (Name: "Plural target frameworks", Directory: "case2", Project: "<TargetFrameworks>net48;net8.0</TargetFrameworks>", Props: (string?)null, Nested: false, Prr: "", Pxr: "", Prrs: "net48;net8.0", Pxrs: "net48;net8.0"),
            (Name: "Property in project", Directory: "case3", Project: "<MyTfm>net8.0</MyTfm><TargetFramework>$(MyTfm)</TargetFramework>", Props: (string?)null, Nested: false, Prr: "net8.0", Pxr: "$(MyTfm)", Prrs: "", Pxrs: ""),
            (Name: "Property in Directory.Build.props", Directory: "case4", Project: "<TargetFramework>$(MyTfm)</TargetFramework>", Props: "<Project><PropertyGroup><MyTfm>net8.0</MyTfm></PropertyGroup></Project>", Nested: true, Prr: "net8.0", Pxr: "$(MyTfm)", Prrs: "", Pxrs: ""),
            (Name: "No target framework", Directory: "case5", Project: "", Props: (string?)null, Nested: false, Prr: "", Pxr: "", Prrs: "", Pxrs: ""),
            (Name: "Plural target frameworks from Directory.Build.props", Directory: "case6", Project: "<TargetFrameworks>$(MyTfms)</TargetFrameworks>", Props: "<Project><PropertyGroup><MyTfms>net48;net8.0</MyTfms></PropertyGroup></Project>", Nested: true, Prr: "", Pxr: "", Prrs: "net48;net8.0", Pxrs: "$(MyTfms)")
        };

        try
        {
            foreach (var testCase in cases)
            {
                string caseDirectory = Path.Combine(temporaryRoot, testCase.Directory);
                string projectDirectory = testCase.Nested
                    ? Path.Combine(caseDirectory, "project")
                    : caseDirectory;
                Directory.CreateDirectory(projectDirectory);

                if (testCase.Props != null)
                {
                    File.WriteAllText(
                        Path.Combine(caseDirectory, "Directory.Build.props"),
                        testCase.Props);
                }

                string projectPath = Path.Combine(projectDirectory, "fixture.csproj");
                File.WriteAllText(
                    projectPath,
                    $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{testCase.Project}</PropertyGroup></Project>");

                string evaluatedTargetFramework = EvaluateTargetFramework(projectPath);
                string evaluatedTargetFrameworks = EvaluateProperty(projectPath, "TargetFrameworks");

                var projectXml = new XmlDocument();
                projectXml.Load(projectPath);
                XmlNodeList rawTargetFrameworkNodes = projectXml.GetElementsByTagName("TargetFramework");
                XmlNodeList rawTargetFrameworksNodes = projectXml.GetElementsByTagName("TargetFrameworks");
                string? rawTargetFramework = rawTargetFrameworkNodes.Count == 1
                    ? rawTargetFrameworkNodes[0]!.InnerText.Trim()
                    : null;
                string? rawTargetFrameworks = rawTargetFrameworksNodes.Count == 1
                    ? rawTargetFrameworksNodes[0]!.InnerText.Trim()
                    : null;

                TestContext.WriteLine(
                    $"Case={testCase.Name}; PRR evaluation-point TargetFramework=" +
                    $"{FormatValue(evaluatedTargetFramework)}; PXR XML-point TargetFramework={FormatValue(rawTargetFramework)}; " +
                    $"PRR evaluation-point TargetFrameworks={FormatValue(evaluatedTargetFrameworks)}; " +
                    $"PXR XML-point TargetFrameworks={FormatValue(rawTargetFrameworks)}");
                Assert.AreEqual(testCase.Prr, evaluatedTargetFramework, testCase.Name + " PRR");
                Assert.AreEqual(testCase.Pxr, rawTargetFramework ?? "", testCase.Name + " PXR");
                Assert.AreEqual(testCase.Prrs, evaluatedTargetFrameworks, testCase.Name + " PRR TargetFrameworks");
                Assert.AreEqual(testCase.Pxrs, rawTargetFrameworks ?? "", testCase.Name + " PXR TargetFrameworks");
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

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

    [TestMethod]
    public void TargetFrameworkHelperNeverReturnsUnsupportedFrameworks()
    {
        var frameworks = TargetFrameworkHelper.Parse("foo;$(MyTfm);", "net8.0;;");

        Assert.AreEqual(1, frameworks.Count);
        Assert.AreEqual("net8.0", frameworks[0].GetShortFolderName());
        Assert.IsTrue(frameworks.All(framework => !framework.IsUnsupported && framework.Version != new Version(0, 0)));
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