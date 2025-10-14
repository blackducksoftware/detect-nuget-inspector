using System.Diagnostics;

namespace detect_nuget_inspector_tests.versioned_dotnet_tests
{
    public class TestSolutionBuilder
    {
        private readonly TestEnvironmentManager _environment;
        private string _solutionDirectory;
        private string _solutionName;

        public TestSolutionBuilder(TestEnvironmentManager environment)
        {
            _environment = environment;
        }

        public TestSolutionBuilder CreateSolution(string solutionName)
        {
            try
            {
                _solutionName = solutionName;
                _solutionDirectory = Path.Combine(_environment.WorkingDirectory, solutionName);
                Directory.CreateDirectory(_solutionDirectory);

                // Create solution
                RunDotNetCommand($"new sln -n {solutionName}", _solutionDirectory);

                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Test solution creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }

        }
        
        public TestSolutionBuilder CreateNestedSolution(string parentDirectory, string solutionName)
        {
            try
            {
                var nestedSolutionDir = Path.Combine(parentDirectory, solutionName);
                Directory.CreateDirectory(nestedSolutionDir);

                RunDotNetCommand($"new sln -n {solutionName}", nestedSolutionDir);

                // Optionally update _solutionDirectory/_solutionName if you want to work with this nested solution next
                _solutionDirectory = nestedSolutionDir;
                _solutionName = solutionName;

                return this;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Nested solution creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }

        public TestSolutionBuilder CreateAndAddProject(string projectName)
        {
            try {
                // Create a minimal project with a class
                RunDotNetCommand($"new classlib -n {projectName}", _solutionDirectory);

                // Add project to solution
                RunDotNetCommand($"sln add {projectName}/{projectName}.csproj", _solutionDirectory);
                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Test subproject creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }
        
        public TestSolutionBuilder AddDependencyToProject(string projectName, string packageName, string version)
        {
            try {
                var projectDir = Path.Combine(_solutionDirectory, projectName);
                
                // First, restore the project to ensure it's in a proper state
                RunDotNetCommand("restore", projectDir);
                
                // Then add the package
                var args = $"add package {packageName} --version {version}";
                RunDotNetCommand(args, projectDir);
                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Adding dependency to subproject failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }

        public TestSolutionBuilder AddPackageReferenceToCsprojManually(string projectName, string packageName, string version)
        {
            // Adding the same package with a different version will cause the dotnet add package command to update the
            // existing reference to the new version, not add a duplicate. So we need to manually update the project file.
            var projectDir = Path.Combine(_solutionDirectory, projectName);
            var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
            var csprojContent = File.ReadAllText(csprojPath);
            var packageReference = $"  <PackageReference Include=\"{packageName}\" Version=\"{version}\" />";
            csprojContent = csprojContent.Replace("</ItemGroup>", $"{packageReference}\n  </ItemGroup>");
            File.WriteAllText(csprojPath, csprojContent);

            return this;
        }

        public TestSolutionBuilder RemoveBuildArtifacts()
        {
            try
            {
                // Remove bin and obj directories
                foreach (var dir in Directory.GetDirectories(_solutionDirectory, "*", SearchOption.AllDirectories))
                {
                    if (dir.EndsWith("bin") || dir.EndsWith("obj"))
                    {
                        Directory.Delete(dir, true);
                    }
                }

                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Removing build artifacts failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }
        
        public TestSolutionBuilder EnableCentralPackageManagementWithDesiredStructure()
        {
            // This method creates a CPM enabled project with the following structure:
            // (root)
            // ├─Directory.Packages.props (13.0.3)
            // |
            // ├─Solution1
            // |  ├─Directory.Packages.props (12.0.3)
            // |  |
            // |  └─ProjectA
            // |      └─ProjectA.csproj
            // |
            // └─Solution2
            //    └─ProjectB
            //        └─ProjectB.csproj
            
            // 1. Create Directory.Packages.props at solution root directory
            // Each time we create a new solution, the builder switches context to that solution. So here we will keep
            // track of our root solution:
            var rootSolutionDir = _solutionDirectory;
            var rootSolutionName = _solutionName;
            CreateBlankDirectoryPackagesPropsFile(_solutionDirectory);
            AddCentrallyManagedPackageToPropsFile("Newtonsoft.Json", "13.0.3");
            
            // 2. Create Solution1
            CreateNestedSolution(rootSolutionDir, "Solution1");
            var nestedSolution1Dir = _solutionDirectory;
            var nestedSolution1Name = _solutionName;
            // 2a. Inside Solution1, create its own Directory.Packages.props
            CreateBlankDirectoryPackagesPropsFile(_solutionDirectory);
            AddCentrallyManagedPackageToPropsFile("Newtonsoft.Json", "12.0.3");
            // 2b. Inside Solution1, create ProjectA
            CreateAndAddProject("ProjectA");
            EnableCentralPackageManagementForProject("ProjectA");
            AddCentrallyManagedPackageReferenceToProject("ProjectA", "Newtonsoft.Json");

            // 3. Create Solution2
            CreateNestedSolution(rootSolutionDir, "Solution2");
            var nestedSolution2Dir = _solutionDirectory;
            var nestedSolution2Name = _solutionName;
            // 3a. Inside Solution2, create ProjectB
            CreateAndAddProject("ProjectB");
            EnableCentralPackageManagementForProject("ProjectB");
            AddCentrallyManagedPackageReferenceToProject("ProjectB", "Newtonsoft.Json");
            
            // Switch context back to root solution
            _solutionDirectory = rootSolutionDir;
            _solutionName = rootSolutionName;

            return this;
        }
        
        public TestSolutionBuilder CreateBlankDirectoryPackagesPropsFile(string directory)
        {
            // Create Directory.Packages.props manually
            var propsPath = Path.Combine(directory, "Directory.Packages.props");
            var propsContent = @"<Project>
  <ItemGroup>
    <!-- Central package versions go here -->
  </ItemGroup>
</Project>";
            File.WriteAllText(propsPath, propsContent);
            return this;
        }

        public TestSolutionBuilder AddCentrallyManagedPackageToPropsFile(string packageName, string version)
        { 
            var propsPath = Path.Combine(_solutionDirectory, "Directory.Packages.props");
            var propsXml = System.Xml.Linq.XDocument.Load(propsPath);
            var itemGroup = propsXml.Root.Elements("ItemGroup").FirstOrDefault();
            if (itemGroup == null)
            {
                itemGroup = new System.Xml.Linq.XElement("ItemGroup");
                propsXml.Root.Add(itemGroup);
            }
            itemGroup.Add(new System.Xml.Linq.XElement("PackageVersion",
                new System.Xml.Linq.XAttribute("Include", packageName),
                new System.Xml.Linq.XAttribute("Version", version)));
            propsXml.Save(propsPath);
            return this;
        }
        
        public TestSolutionBuilder AddCentrallyManagedPackageReferenceToProject(string projectName, string packageName)
        {
            var projectDir = Path.Combine(_solutionDirectory, projectName);
            var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
            var csprojContent = File.ReadAllText(csprojPath);
            var packageReference = $"  <PackageReference Include=\"{packageName}\" />";
            
            if (csprojContent.Contains("<ItemGroup>"))
            {
                // So we can reuse this method for a test tht adds more than one dependency
                csprojContent = csprojContent.Replace("</ItemGroup>", $"{packageReference}\n  </ItemGroup>");
            }
            else
            {
                // Add a new ItemGroup before </Project>
                csprojContent = csprojContent.Replace("</Project>", $"  <ItemGroup>\n{packageReference}\n  </ItemGroup>\n</Project>");
            }

            File.WriteAllText(csprojPath, csprojContent);

            return this;
        }
        
        public TestSolutionBuilder EnableCentralPackageManagementForProject(string projectName)
        {
            var projectDir = Path.Combine(_solutionDirectory, projectName);
            var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
            var csprojContent = File.ReadAllText(csprojPath);

            var propertyToAdd = "    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>\n";
            if (csprojContent.Contains("<PropertyGroup>"))
            {
                csprojContent = csprojContent.Replace(
                    "<PropertyGroup>",
                    "<PropertyGroup>\n" + propertyToAdd
                );
            }
            else
            {
                // If no PropertyGroup exists, add one at the top after <Project ...>
                var projectTagEnd = csprojContent.IndexOf('>') + 1;
                csprojContent = csprojContent.Insert(
                    projectTagEnd,
                    "\n  <PropertyGroup>\n" + propertyToAdd + "  </PropertyGroup>\n"
                );
            }
            File.WriteAllText(csprojPath, csprojContent);

            return this;
        }

        
        private void RunDotNetCommand(string arguments, string workingDirectory)
        {
            var command = $"{_environment.DotNetCommand} {arguments}";
            Console.WriteLine($"> {command}");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _environment.DotNetCommand,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet {arguments} failed: {process.StandardError.ReadToEnd()}");
            }
        }

        public string Build()
        {
            return _solutionDirectory;
        }
    }
}