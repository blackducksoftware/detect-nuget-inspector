using System.Reflection;
using Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;

namespace detect_nuget_inspector_tests.Inspection.Inspectors
{
    [TestClass]
    public class SolutionInspectorTests
    {
        private string GetFilePath(string fileName)
        {
            string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string filePath = Path.Combine(projectDirectory, "Files", fileName);
            return filePath;
        }

        private List<ProjectFile> InvokePrivateMethod(SolutionInspector inspector, string methodName, string filePath)
        {
            var method = typeof(SolutionInspector).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null
            );

            return (List<ProjectFile>)method.Invoke(inspector, new object[] { filePath });
        }

        /// <summary>
        /// Parametrized test that compares project lists from equivalent .sln and .slnx files.
        /// To test additional file pairs, add new [DataRow] entries with the .sln filename, .slnx filename, and solution name.
        /// </summary>
        [DataTestMethod]
        [DataRow("PowerToys.sln", "PowerToys.slnx", "PowerToys")]
        //[DataRow("Simple.sln", "Simple.slnx", "Simple")]  // Add your new pair here
        public void CompareSlnAndSlnxProjectLists(string slnFileName, string slnxFileName, string solutionName)
        {
            // Arrange
            string slnFilePath = GetFilePath(slnFileName);
            string slnxFilePath = GetFilePath(slnxFileName);

            List<ProjectFile> slnProjects = SolutionInspector.FindProjectFilesFromSlnFile(slnFilePath);
            // .slnx files list projects with forward slashes, so we need to sanitize .sln project paths for a fair comparison
            slnProjects = slnProjects.Select(p => new ProjectFile { Name = p.Name, Path = PathUtil.Sanitize(p.Path) }).ToList();
            List<ProjectFile> slnxProjects = SolutionInspector.FindProjectFilesFromSlnxFile(slnxFilePath);

            // Assert
            Assert.IsNotNull(slnProjects, "SLN projects list should not be null");
            Assert.IsNotNull(slnxProjects, "SLNX projects list should not be null");
            Assert.AreEqual(slnProjects.Count, slnxProjects.Count,
                $"Project count mismatch for {solutionName}: SLN has {slnProjects.Count} projects, SLNX has {slnxProjects.Count} projects");

            // Verify projects match by name and path (order does not matter)
            var slnProjectsByName = slnProjects.OrderBy(p => p.Name).ToList();
            var slnxProjectsByName = slnxProjects.OrderBy(p => p.Name).ToList();

            for (int i = 0; i < slnProjectsByName.Count; i++)
            {
                Assert.AreEqual(slnProjectsByName[i].Name, slnxProjectsByName[i].Name,
                    $"Project name mismatch at index {i} for {solutionName}");
                Assert.AreEqual(slnProjectsByName[i].Path, slnxProjectsByName[i].Path,
                    $"Project path mismatch for project '{slnProjectsByName[i].Name}' in {solutionName}");
            }
        }


    }
}
