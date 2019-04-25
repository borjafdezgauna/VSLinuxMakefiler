using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VSLinuxMakefiler
{
    public class UnitTest : VSProject
    {
        public override ProjectType Type() { return ProjectType.UnitTest; }
        public override string SolutionRelativeOutputFile()
        {
            return TempProjectFolder + Name + ".exe";
        }

        string PreprocessedSourceFile
        {
            get { return ProjectFolder + "/main-linux.cpp"; }
        }

        string LinuxCppUnitTestHeaderDir { get; }

        public UnitTest(string name, string projectPath, string solutionPath, string linuxCppUnitTestHeaderDir): base(name, projectPath, solutionPath)
        {
            LinuxCppUnitTestHeaderDir = linuxCppUnitTestHeaderDir;
            //Preprocess source files and create the source file to actually be compiled
        }

        public override string CompilerFlags(string sourceFile)
        {
             return "";
        }

        public override string LinkerFlags()
        {
            return "-Wl,--no-undefined ";
        }

        protected override void WriteCompileSources(StreamWriter writer)
        {
            //Do nothing, we wanto to compile/link in a single command
        }

        protected override void WriteLinkSources(StreamWriter writer)
        {
            //This base implementation works for dynamic libs and executables
            string linkCommand;

            linkCommand = m_compilerExecutable + " -o " + SolutionRelativeOutputFile() + " -I" + LinuxCppUnitTestHeaderDir + " " + PreprocessedSourceFile + " " + LinkerFlags();
            foreach (string referencedProjectOutput in ReferencedProjectsOutputs)
                linkCommand += " \"" + referencedProjectOutput + "\""; //don't use in the linking phase unless it's a static
            foreach (string dependency in LibraryDependencies)
                linkCommand += " -l\"" + dependency + "\"";
            foreach (string additionalDir in AdditionalLibraryDirectories)
                linkCommand += " -Wl,-L\"" + ProjectFolder + "/" + additionalDir + "\"";
            if (AdditionalSourcesToCopyMapping.Keys.Count > 0)
                linkCommand += " -Wl,-L\"" + TempProjectFolder + "\""; //manually add the temp project as an additional library dir because we are copying there the additional libraries
            if (AdditionalLinkOptions != "") linkCommand += " " + AdditionalLinkOptions;

            writer.WriteLine(linkCommand);
        }
    }
}
