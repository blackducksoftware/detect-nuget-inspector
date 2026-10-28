using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project.Test;

[TestClass]
public class ProjectXmlResolverTests
{
    [TestMethod]
    public void ProcessSkipsUnresolvedVersionWithWarning()
    {
        VerifyPackages("<PackageReference Include=\"Bad.Package\" Version=\"$(PkgVersion)\" />", null, true);
    }

    [DataTestMethod]
    [DataRow("13.0.3", "13.0.3")]
    [DataRow("[13.0.3,14.0.0)", "13.0.3")]
    [DataRow("13.0.*", "13.0.0")]
    public void ProcessPreservesValidVersions(string version, string expectedVersion)
    {
        VerifyPackages($"<PackageReference Include=\"Good.Package\" Version=\"{version}\" />", expectedVersion, false);
    }

    [TestMethod]
    public void ProcessKeepsValidPackageWhenAnotherVersionIsUnresolved()
    {
        VerifyPackages("<PackageReference Include=\"Bad.Package\" Version=\"$(PkgVersion)\" /><PackageReference Include=\"Good.Package\" Version=\"13.0.3\" />", "13.0.3", true);
    }

    private static void VerifyPackages(string references, string? expectedVersion, bool expectWarning)
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        TextWriter originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            string projectPath = Path.Combine(directory, "Fixture.csproj");
            string configPath = Path.Combine(directory, "NuGet.Config");
            File.WriteAllText(projectPath, $"<Project><PropertyGroup><PkgVersion>13.0.3</PkgVersion></PropertyGroup><ItemGroup>{references}</ItemGroup></Project>");
            File.WriteAllText(configPath, "<configuration><packageSources><clear /></packageSources></configuration>");
            Console.SetOut(output);
            var service = new NugetSearchService(string.Empty, configPath);

            var result = new ProjectXmlResolver(projectPath, service, "NONE").Process();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(expectedVersion == null ? 0 : 1, result.Packages.Count);
            Assert.AreEqual(expectedVersion == null ? 0 : 1, result.Dependencies.Count);
            if (expectedVersion != null)
            {
                Assert.AreEqual("Good.Package", result.Packages.Single().PackageId.Name);
                Assert.AreEqual(expectedVersion, result.Packages.Single().PackageId.Version);
            }
            string warning = "WARNING: Skipping package Bad.Package: unresolved version '$(PkgVersion)' (MSBuild properties can't be evaluated without a .NET SDK).";
            Assert.AreEqual(expectWarning, output.ToString().Contains(warning));
        }
        finally
        {
            Console.SetOut(originalOutput);
            Directory.Delete(directory, true);
        }
    }
}