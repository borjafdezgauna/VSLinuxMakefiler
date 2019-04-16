using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace VSLinuxMakefiler
{
    public class VSSolution
    {
        const string projDefPattern = "Project\\([^\\)]+\\)\\s*=\\s*\"([^\"]+)\"\\s*\\,\\s*\"([^\"]+)\"\\s*\\,\\s*";

        List<VSLinuxProject> m_projects = new List<VSLinuxProject>();
        Dictionary<string, VSLinuxProject> m_projectsByName = new Dictionary<string, VSLinuxProject>();
        string m_solutionPath = null;

        public bool Parse(string solutionFilename)
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

                if (projectPath.EndsWith("-linux.vcxproj"))
                {
                    VSLinuxProject project = new VSLinuxProject(projectName, projectPath, m_solutionPath);
                    if (project.SuccessfullyParsed)
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
                        VSLinuxProject tmp;
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
            //1. Sort the projects
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
            //2. Reorder referenced projects and resolve references
            foreach(VSLinuxProject project in m_projects)
            {
                project.ReferencedProjects.Sort((x, y) => ProjectIndex(y).CompareTo(ProjectIndex(x)));

                //Resolve dependencies
                foreach (string reference in project.ReferencedProjects)
                {
                    //for simplicity, we only include referenced static libraries in the linking phase
                    if (m_projectsByName[reference].Type == VSLinuxProject.ConfigurationType.StaticLibrary)
                        project.ReferencedProjectsOutputs.Add(m_projectsByName[reference].SolutionRelativeOutputFile);
                }
            }
        }

        string CreateFolderStructure = "mkdir tmp\n";

        public void GenerateBuildFile()
        {
            //1. Resolve referenced projects, and sort the projects/references them according to the dependencies
            SortProjectsAndReferences();

            //2. Generate the build file
            string outputFilename = m_solutionPath + "/build-linux.sh";

            Console.WriteLine("Generating build script " + outputFilename);
            Console.WriteLine("VSLinux projects: ");
            foreach (VSLinuxProject project in m_projects)
            {
                Console.WriteLine("[" + project.Type + "] " + project.Name + " (" + project.SolutionRelativePath + ")");
            }

            using (StreamWriter writer = File.CreateText(outputFilename))
            {
                writer.WriteLine(CreateFolderStructure);

                foreach (VSLinuxProject project in m_projects)
                {
                    project.WriteBuildScript(writer);
                }
            }
        }
    }
}
