using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace VSLinuxMakefiler
{
    public class VSSolution
    {
        const string projDefPattern = "Project\\([^\\)]+\\)\\s*=\\s*\"([^\"]+)\"\\s*\\,\\s*\"([^\"]+)\"\\s*\\,\\s*";

        List<VSProject> m_projects = new List<VSProject>();
        Dictionary<string, VSProject> m_projectsByName = new Dictionary<string, VSProject>();
        string m_solutionPath = null;

        public bool Parse(string solutionFilename, string linuxCppUnitTestHeaderDir)
        {
            if (!File.Exists(solutionFilename))
                return false;

            string solutionAsText = File.ReadAllText(solutionFilename);
            m_solutionPath = Path.GetDirectoryName(solutionFilename);

            foreach (Match match in Regex.Matches(solutionAsText, projDefPattern))
            {
                string projectName, projectPath;
                projectName = match.Groups[1].Value.Replace('\\','/');
                projectPath = match.Groups[2].Value.Replace('\\', '/');

                if (projectPath.EndsWith(".vcxproj"))
                {
                    VSProject.ProjectType type = VSProject.GetProjectType(m_solutionPath + "/" + projectPath);
                    VSProject project = null;
                    switch (type)
                    {
                        case VSProject.ProjectType.DynamicLibrary: project = new DynamicLib(projectName, projectPath, m_solutionPath); break;
                        case VSProject.ProjectType.StaticLibrary: project = new StaticLib(projectName, projectPath, m_solutionPath); break;
                        case VSProject.ProjectType.Executable: project = new Executable(projectName, projectPath, m_solutionPath); break;
                        case VSProject.ProjectType.UnitTest:
                            //we only parse unit tests if the dir of the linux cppUnitTest headers was given
                            if (linuxCppUnitTestHeaderDir != null)
                                project = new UnitTest(projectName, projectPath, m_solutionPath, linuxCppUnitTestHeaderDir); break;
                        default: break;
                    }
                    if (project != null && project.SuccessfullyParsed)
                    {
                        m_projects.Add(project);
                        m_projectsByName[projectName] = project;
                    }
                }
            }
            return true;
        }

        int ProjectIndex(string projectName)
        {
            int index = m_projects.FindIndex(project => project.Name == projectName);
            return index;
        }

        bool FixFirstDependencyOrderError()
        {
            for(int i= 0; i<m_projects.Count; i++)
            {
                foreach(string referencedProject in m_projects[i].ReferencedProjects)
                {
                    int referencedProjectIndex = ProjectIndex(referencedProject);
                    if (referencedProjectIndex > i)
                    {
                        //swap
                        VSProject tmp;
                        tmp = m_projects[i];
                        m_projects[i] = m_projects[referencedProjectIndex];
                        m_projects[referencedProjectIndex] = tmp;
                        return false;
                    }
                }
            }
            return true;
        }

        void SortProjectsAndReferences()
        {
            //1. Pre-process referenced projects so that unit tests reference the linux version of the projects (ending with "-linux")
            foreach (VSProject project in m_projects)
            {
                for (int i= 0; i<project.ReferencedProjects.Count; i++)
                {
                    string reference = project.ReferencedProjects[i];
                    //if the referenced project hasn't been loaded (is unsupported) and the linux version has been loaded, fix the reference
                    if (!m_projectsByName.ContainsKey(reference) && m_projectsByName.ContainsKey(reference + "-linux"))
                        project.ReferencedProjects[i] = reference + "-linux";
                }
            }
            //2. Add secondary project references
            List<string> secondaryReferences = new List<string>();
            List<string> secondaryReferenceLibDependencies = new List<string>();
            List<string> secondaryReferenceAdditionalDirectories = new List<string>();
            foreach (VSProject project in m_projects)
            {
                foreach (string referencedProject in project.ReferencedProjects)
                {
                    foreach (string secondaryReference in m_projectsByName[referencedProject].ReferencedProjects)
                    {
                        if (!project.ReferencedProjects.Contains(secondaryReference) && !secondaryReferences.Contains(secondaryReference))
                            secondaryReferences.Add(secondaryReference);

                        //If it is a unit test, we expect referenced projects to have all the options to compile it. Get them
                        if (project.Type() == VSProject.ProjectType.UnitTest)
                        {
                            //add secondary lib dependencies
                            foreach (string dependency in m_projectsByName[referencedProject].LibraryDependencies)
                                if (!project.LibraryDependencies.Contains(dependency))
                                    project.LibraryDependencies.Add(dependency);
                            //add secondary additional directories
                            foreach (string additionalLibraryDirectory in m_projectsByName[referencedProject].AdditionalLibraryDirectories)
                                if (!project.AdditionalLibraryDirectories.Contains(additionalLibraryDirectory))
                                    project.LibraryDependencies.Add(additionalLibraryDirectory);
                            //add secondary additional link options
                            if (m_projectsByName[referencedProject].AdditionalLinkOptions != null && m_projectsByName[referencedProject].AdditionalLinkOptions != "")
                                project.AdditionalLinkOptions += m_projectsByName[referencedProject].AdditionalLinkOptions;
                        }
                    }
                }
                project.ReferencedProjects.AddRange(secondaryReferences);
                secondaryReferences.Clear();
            }
            //3. Sort the projects according to dependencies
            const int maxNumIterations = 100;
            int numIterations = 0;
            bool projectsOrdered = FixFirstDependencyOrderError();
            while (numIterations<maxNumIterations && !projectsOrdered)
            {
                projectsOrdered = FixFirstDependencyOrderError();
                numIterations++;
            }
            if (numIterations == maxNumIterations)
            {
                Console.WriteLine("Warning: maximum number of iterations reached while sorting projects and references");
            }
            //3. Reorder referenced projects and resolve references
            foreach(VSProject project in m_projects)
            {
                project.ReferencedProjects.Sort((x, y) => ProjectIndex(y).CompareTo(ProjectIndex(x)));

                //Resolve dependencies
                foreach (string reference in project.ReferencedProjects)
                {
                    //for simplicity, we only include referenced static libraries in the linking phase
                    if (m_projectsByName[reference].Type() == VSProject.ProjectType.StaticLibrary)
                        project.ReferencedProjectsOutputs.Add(m_projectsByName[reference].SolutionRelativeOutputFile());
                }
            }
        }

        readonly string CreateFolderStructure = "mkdir tmp\n";

        public void GenerateBuildFile()
        {
            //1. Resolve referenced projects, and sort the projects/references them according to the dependencies
            SortProjectsAndReferences();

            //2. Generate the build file
            string outputFilename = m_solutionPath + "/linux-ci-build.sh";

            Console.WriteLine("Generating CI script " + outputFilename);
            Console.WriteLine("VSLinux projects: ");
            foreach (VSProject project in m_projects)
            {
                Console.WriteLine("[" + project.Type() + "] " + project.Name + " (" + project.SolutionRelativePath + ")");
            }

            using (StreamWriter writer = File.CreateText(outputFilename))
            {
                writer.WriteLine(CreateFolderStructure);

                //Build all the projects
                writer.WriteLine("echo #### 1. Compile the projects");
                foreach (VSProject project in m_projects) project.WriteBuildScript(writer);

                //Run all the tests
                writer.WriteLine("echo #### 2. Run unit tests");
                foreach (VSProject project in m_projects)
                    if (project.Type() ==VSProject.ProjectType.UnitTest)
                        writer.WriteLine (project.SolutionRelativeOutputFile());
            }
        }
    }
}
