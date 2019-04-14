﻿using System;
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
                    case ConfigurationType.DynamicLibrary: return TempProjectFolder + "lib" + Name + ".so";
                    case ConfigurationType.Executable: default: return TempProjectFolder + Name + ".exe";
                }
            }
        }

        string TempProjectFolder { get { return TmpFolder + "/" + Name + "/"; } }

        public List<string> SourceFiles { get; } = new List<string>();
        public List<string> ReferencedProjects { get; } = new List<string>();
        public List<string> LibraryDependencies { get; } = new List<string>();

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
        const string AdditionalSourcesXPath = "/MsBuild:Project/MsBuild:PropertyGroup/MsBuild:AdditionalSourcesToCopyMapping";
        const string LibraryDependenciesXPath = "MsBuild:Project/MsBuild:ItemDefinitionGroup/MsBuild:Link/MsBuild:LibraryDependencies";
       //     AdditionalLibraryDirectories

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

            SuccessfullyParsed = true;
        }

        string SolutionRelativePathToSourceFile(string sourceFile)
        {
            return Path.GetDirectoryName(SolutionRelativePath).Replace('\\','/') + "/" + sourceFile;
        }

        string Compiling = "echo Compiling {0}";
        string CreateFolder = "mkdir {0}/{1}";
        string Finished = "echo Compilation finished";
        string CommonCompilerFlags = "-std=c++11";
        string LibraryCompilerFlags = "-c -fPIC";
        string CompilerExecutable = "g++";
        const string TmpFolder = "tmp";

        public void WriteBuildScript(StreamWriter writer)
        {
            writer.WriteLine(Compiling, Name);
            writer.WriteLine(CreateFolder, TmpFolder, Name);

            //1. Compile sources

            string flags = "";
            if (Type == ConfigurationType.StaticLibrary)
                flags = LibraryCompilerFlags + " " + CommonCompilerFlags + " ";
            foreach(string sourceFile in SourceFiles)
            {
                writer.WriteLine(CompilerExecutable + " " + flags + SolutionRelativePathToSourceFile(sourceFile)
                    + " -o " + TempProjectFolder + Path.GetFileNameWithoutExtension(sourceFile) + ".o");
            }

            //2. Link sources
            if (Type == ConfigurationType.StaticLibrary)
                writer.WriteLine("ar rcs {0} {1}*.o", SolutionRelativeOutputFile, TempProjectFolder);

            writer.WriteLine(Finished);
            writer.WriteLine();
        }
    }
}
