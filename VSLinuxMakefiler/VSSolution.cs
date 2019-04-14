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
                        m_projects.Add(project);
                }
            }
            return true;
        }

        string CreateFolderStructure = "echo mkdir tmp\n";

        public void GenerateBuildFile()
        {
            string outputFilename = m_solutionPath + "/build-linux.sh";

            Console.WriteLine("Generating build script " + outputFilename);
            Console.WriteLine("VSLinux projects: ");
            foreach (VSLinuxProject project in m_projects)
            {
                Console.WriteLine("[" + project.Type + "] " + project.Name + " (" + project.SolutionRelativePath + ")");
                //foreach (string referencedProjects in project.ReferencedProjects)
                //    Console.WriteLine("  References: " + referencedProjects);
                //foreach (string dependency in project.LibraryDependencies)
                //    Console.WriteLine("  Depends on: " + dependency);
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
