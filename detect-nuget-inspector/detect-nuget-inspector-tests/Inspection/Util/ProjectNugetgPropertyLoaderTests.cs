using Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors;

namespace detect_nuget_inspector_tests.Inspection.Util
{
    [TestClass]
    public class ProjectNugetgPropertyLoaderTests
    {
        [TestMethod]
        public void ProcessExpandsMSBuildThisFileDirectoryInProjectAssetsFile()
        {
            string propsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(propsDirectory);
            try
            {
                string propsPath = Path.Combine(propsDirectory, "Fixture.csproj.nuget.g.props");
                File.WriteAllText(propsPath, @"<Project>
  <PropertyGroup>
    <ProjectAssetsFile Condition="" '$(ProjectAssetsFile)' == '' "">$(MSBuildThisFileDirectory)project.assets.json</ProjectAssetsFile>
  </PropertyGroup>
</Project>");
                string assetsPath = Path.Combine(propsDirectory, "project.assets.json");
                File.WriteAllText(assetsPath, "{}");

                string result = new ProjectNugetgPropertyLoader(propsPath, null!).Process();

                Assert.AreEqual(assetsPath, result);
            }
            finally
            {
                Directory.Delete(propsDirectory, true);
            }
        }
    }
}
