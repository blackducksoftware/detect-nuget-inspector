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
public partial class ProjectTargetFrameworkBehaviorTests
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
    public void TargetFrameworkHelperNeverReturnsUnsupportedFrameworks()
    {
        var frameworks = TargetFrameworkHelper.Parse("foo;$(MyTfm);", "net8.0;;");

        Assert.AreEqual(1, frameworks.Count);
        Assert.AreEqual("net8.0", frameworks[0].GetShortFolderName());
        Assert.IsTrue(frameworks.All(framework => !framework.IsUnsupported && framework.Version != new Version(0, 0)));
    }
}
