using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;

namespace VSLinuxMakefiler
{
    public class VSLinuxProject
    {
        public enum ConfigurationType { Executable, StaticLibrary, DynamicLibrary };

        public ConfigurationType Type { get; set; } = ConfigurationType.Executable;
        public string Name { get; } = null;
        public string SolutionRelativePath { get; } = null;
        public string AbsolutePath { get; } = null;
        readonly string SolutionPath = null;

        public string SolutionRelativeOutputFile
        {
            get
            {
                switch (Type)
                {
                    case ConfigurationType.StaticLibrary: return TempProjectFolder + Name + ".a";
                    case ConfigurationType.DynamicLibrary: return TempProjectFolder + Name + ".so";
                    case ConfigurationType.Executable: default: return TempProjectFolder + Name + ".exe";
                }
            }
        }

        string TempProjectFolder { get { return TmpFolder + "/" + Name + "/"; } }
        string ProjectFolder { get { return Path.GetDirectoryName(SolutionRelativePath).Replace('\\','/'); } }

        public List<string> SourceFiles { get; } = new List<string>();
        public List<string> ReferencedProjects { get; } = new List<string>();
        public List<string> ReferencedProjectsOutputs { get; } = new List<string>();
        public List<string> LibraryDependencies { get; } = new List<string>();
        public List<string> AdditionalSources { get; } = new List<string>();
        public List<string> AdditionalLibraryDirectories { get; } = new List<string>();
        public string AdditionalLinkOptions { get; set; } = "";
        public Dictionary<string, string> AdditionalSourcesToCopyMapping { get; } = new Dictionary<string, string>();

        public bool SuccessfullyParsed { get; set; } = false;

        public VSLinuxProject(string name, string projectPath, string solutionPath)
        {
            Name = name;
            SolutionRelativePath = projectPath;
            AbsolutePath = solutionPath + "/" + projectPath;
            SolutionPath = solutionPath;

            ParseVSLinuxProject(AbsolutePath);
        }

        const string ApplicationTypeXPath = "/MsBuild:Project/MsBuild:PropertyGroup/MsBuild:ApplicationType";
        const string ConfigurationTypeXPath = "/MsBuild:Project/MsBuild:PropertyGroup/MsBuild:ConfigurationType";
        const string SourceFileXPath = "/MsBuild:Project/MsBuild:ItemGroup/MsBuild:ClCompile";
        const string ProjectReferenceFileXPath = "/MsBuild:Project/MsBuild:ItemGroup/MsBuild:ProjectReference";
        const string AdditionalSourcesToCopyMappingXPath = "/MsBuild:Project/MsBuild:PropertyGroup/MsBuild:AdditionalSourcesToCopyMapping";
        const string LibraryDependenciesXPath = "MsBuild:Project/MsBuild:ItemDefinitionGroup/MsBuild:Link/MsBuild:LibraryDependencies";
        const string AdditionalLibraryDirectoriesXPath= "MsBuild:Project/MsBuild:ItemDefinitionGroup/MsBuild:Link/MsBuild:AdditionalLibraryDirectories";
        const string AdditionalLinkOptionsXPath = "MsBuild:Project/MsBuild:ItemDefinitionGroup/MsBuild:Link/MsBuild:AdditionalOptions";

        const string SourceFileAttr = "Include";

        void ParseVSLinuxProject(string filename)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);

            //add a namespace manager if xmlns is defined
            if (doc.DocumentElement.Attributes["xmlns"] != null)
            {
                string xmlns = doc.DocumentElement.Attributes["xmlns"].Value;
                nsmgr.AddNamespace("MsBuild", xmlns);
            }

            //check it is actually a VSLinux project
            foreach (XmlNode node in doc.SelectNodes(ApplicationTypeXPath, nsmgr))
            {
                if (node.InnerText != "Linux")
                    return;
            }
            //Configuration type: executable by default
            foreach (XmlNode node in doc.SelectNodes(ConfigurationTypeXPath, nsmgr))
            {
                if (node.InnerText == "StaticLibrary") Type = ConfigurationType.StaticLibrary;
                else if (node.InnerText == "DynamicLibrary") Type = ConfigurationType.DynamicLibrary;
            }

            //Source files
            foreach (XmlNode node in doc.SelectNodes(SourceFileXPath, nsmgr))
            {
                string sourceFilename;
                XmlNode sourceFile = node.Attributes.GetNamedItem(SourceFileAttr);
                if (sourceFile != null)
                {
                    sourceFilename = sourceFile.Value;
                    if (sourceFilename.EndsWith(".c") || sourceFilename.EndsWith(".cpp"))
                        SourceFiles.Add(sourceFilename.Replace('\\','/'));
                }
            }

            //Project references
            foreach (XmlNode node in doc.SelectNodes(ProjectReferenceFileXPath, nsmgr))
            {
                XmlNode sourceFile = node.Attributes.GetNamedItem(SourceFileAttr);
                if (sourceFile != null)
                    ReferencedProjects.Add(Path.GetFileNameWithoutExtension(sourceFile.Value));
            }

            //Library dependencies
            foreach (XmlNode node in doc.SelectNodes(LibraryDependenciesXPath, nsmgr))
            {
                string dependenciesString = node.InnerText;
                string[] dependencies = dependenciesString.Split(';');
                foreach (string dependency in dependencies)
                {
                    if (!LibraryDependencies.Contains(dependency) && dependency.Trim(' ').Length>0)
                        LibraryDependencies.Add(dependency);
                }
            }

            //Additional library directories
            foreach (XmlNode node in doc.SelectNodes(AdditionalLibraryDirectoriesXPath, nsmgr))
            {
                string dirsString = node.InnerText;
                string[] dirs = dirsString.Split(';');
                foreach (string dir in dirs)
                {
                    if (!dir.StartsWith('%') && !AdditionalLibraryDirectories.Contains(dir) && dir.Trim(' ').Length > 0)
                        AdditionalLibraryDirectories.Add(dir.Trim(' '));
                }
            }

            //Additional link options
            foreach (XmlNode node in doc.SelectNodes(AdditionalLinkOptionsXPath, nsmgr))
            {
                AdditionalLinkOptions = node.InnerText; //if there are different configurations, just take the last one
            }

            //Additional sources to copy mapping
            foreach (XmlNode node in doc.SelectNodes(AdditionalSourcesToCopyMappingXPath, nsmgr))
            {
                string mappingString = node.InnerText;
                string[] mappings = mappingString.Split(';');
                foreach (string mapping in mappings)
                {
                    int separatorIndex= mapping.IndexOf(":=");
                    if (separatorIndex>0)
                    {
                        string source = mapping.Substring(0, separatorIndex);
                        string dst = mapping.Substring(separatorIndex + 2);
                        AdditionalSourcesToCopyMapping[source] = dst;
                    }
                }
            }


            SuccessfullyParsed = true;
        }

        string SolutionRelativePathToSourceFile(string sourceFile)
        {
            return Path.GetDirectoryName(SolutionRelativePath).Replace('\\','/') + "/" + sourceFile;
        }

        string CompilerFlags (string sourceFile)
        {
            string langFlags = "";
            if (sourceFile.EndsWith(".c")) langFlags = "-x c -std=c11 ";
            else if (sourceFile.EndsWith(".cpp")) langFlags = "-x c++ -std=c++11 ";

            if (Type == ConfigurationType.StaticLibrary || Type == ConfigurationType.DynamicLibrary)
                return m_libraryCompilerFlags + " " + m_commonCompilerFlags + " " + langFlags;
            return m_commonCompilerFlags + " " + langFlags;
        }

        string LinkerFlags()
        {
            if (Type == ConfigurationType.StaticLibrary)
                return "";
            else if (Type == ConfigurationType.DynamicLibrary)
                return "-Wl,--no-undefined -Wl,-z,relro -Wl,-z,now -Wl,-z,noexecstack -shared ";
            else return "-Wl,--no-undefined ";
        }

        string m_copyFileCommand = "cp {0} {1}";
        string m_compilingMsg = "echo Compiling {0}...";
        string m_createFolderScript = "mkdir {0}/{1}";
        string m_finishedMsg = "echo ...Finished";
        string m_commonCompilerFlags = "-w";/* -g2 -gdwarf-2 -w -Wswitch -W\"no-deprecated-declarations\" -W\"empty-body\" -W\"return-type\" -Wparentheses -W\"no-format\""
            + " -Wuninitialized -W\"unreachable-code\" -W\"unused-function\" -W\"unused-value\" -W\"unused-variable\" -Wswitch -W\"no-deprecated-declarations\" -W\"empty-body\""
            + " -Wconversion -W\"return-type\" -Wparentheses -W\"no-format\" -Wuninitialized -W\"unreachable-code\" -W\"unused-function\" -W\"unused-value\" -W\"unused-variable\""
            + " -O0 -fno-strict-aliasing -fno-omit-frame-pointer -fthreadsafe-statics -fexceptions -frtti";*/
        string m_libraryCompilerFlags = "-c -fPIC";
        string m_compilerExecutable = "g++";
        const string TmpFolder = "tmp";

        public void WriteBuildScript(StreamWriter writer)
        {
            writer.WriteLine(m_compilingMsg, Name);
            writer.WriteLine(m_createFolderScript, TmpFolder, Name);

            //0. Copy additional sources to temp folder
            foreach(string additionalSource in AdditionalSourcesToCopyMapping.Keys)
            {
                writer.WriteLine(m_copyFileCommand, SolutionRelativePathToSourceFile(additionalSource), TempProjectFolder);
            }

            //1. Compile sources
            foreach(string sourceFile in SourceFiles)
            {
                writer.WriteLine(m_compilerExecutable + " " + CompilerFlags(sourceFile) + SolutionRelativePathToSourceFile(sourceFile)
                    + " -o " + TempProjectFolder + Path.GetFileNameWithoutExtension(sourceFile) + ".o");
            }

            //2. Link sources
            string linkCommand;
            if (Type == ConfigurationType.StaticLibrary)
                linkCommand = string.Format("ar rcs {0} {1}*.o {2}", SolutionRelativeOutputFile, TempProjectFolder, LinkerFlags());
            else
            {
                linkCommand = m_compilerExecutable + " -o " + SolutionRelativeOutputFile + " " + TempProjectFolder + "*.o "
                    + LinkerFlags();
                foreach(string referencedProjectOutput in ReferencedProjectsOutputs)
                    linkCommand += " \"" + referencedProjectOutput + "\"";
                foreach (string dependency in LibraryDependencies)
                    linkCommand += " -l\"" + dependency + "\"";
                foreach (string additionalDir in AdditionalLibraryDirectories)
                    linkCommand += " -Wl,-L\"" + ProjectFolder + "/" + additionalDir + "\"";
                if (AdditionalSourcesToCopyMapping.Keys.Count > 0)
                    linkCommand += " -Wl,-L\"" + TempProjectFolder + "\""; //manually add the temp project as an additional library dir because we are copying there the additional libraries
                if (AdditionalLinkOptions != "") linkCommand += " " + AdditionalLinkOptions;
            }
            writer.WriteLine(linkCommand);

            writer.WriteLine(m_finishedMsg);
            writer.WriteLine();
        }
    }
}
