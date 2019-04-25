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

        List<UnitTestNamespace> m_unitTestNamespaces = new List<UnitTestNamespace>();
        List<string> m_unitTestSourceFiles = new List<string>();

        public UnitTest(string name, string projectPath, string solutionPath, string linuxCppUnitTestHeaderDir): base(name, projectPath, solutionPath)
        {
            LinuxCppUnitTestHeaderDir = linuxCppUnitTestHeaderDir;

            //Parse source files to extract test unit Namespace/Class/Methods
            foreach (string sourceFile in SourceFiles)
            {
                string solutionRelPathToSourceFile = SolutionRelativePathToSourceFile(sourceFile);
                string absolutePathToSourceFile = solutionPath + "\\" + solutionRelPathToSourceFile;
                List<UnitTestNamespace> namespaces= VSUnitTestProjectParser.Parse(absolutePathToSourceFile);

                if (namespaces != null && namespaces.Count>0)
                {
                    m_unitTestNamespaces.AddRange(namespaces);
                    m_unitTestSourceFiles.Add(sourceFile);
                }
            }

            //Create the ready-to-compile preprocessed source file
            string outputPreprocessedSourceFile = solutionPath + "\\" + PreprocessedSourceFile;
            using (StreamWriter writer = File.CreateText(outputPreprocessedSourceFile))
            {
                writer.WriteLine(OutputFileHeader);
                foreach (string unitTestSourceFile in m_unitTestSourceFiles)
                    writer.WriteLine(OutputFileIncludeUnitTestSource, unitTestSourceFile);
                writer.WriteLine(OutputFileBeginMain);
                foreach (UnitTestNamespace unitTestNamespace in m_unitTestNamespaces)
                {
                    foreach(UnitTestClass unitTestClass in unitTestNamespace.Classes)
                    {
                        foreach (string unitTestMethod in unitTestClass.Methods)
                        {
                            writer.WriteLine(string.Format(OutputFileTestMethod, unitTestNamespace.Name, unitTestClass.Name, unitTestMethod));
                        }
                    }
                }
                writer.WriteLine(OutputFileEndMain);
            }
        }

        //Constants used to generate the preprocessed source file
        public const string OutputFileHeader = "#include <iostream>";
        public const string OutputFileIncludeUnitTestSource = "#include \"{0}\"";
        public const string OutputFileIncludeUnit = "#include \"{0}\"";
        public const string OutputFileBeginMain = "int main()\n{\n  bool success= true;\n";
        public const string OutputFileEndMain = "  return success;\n}";
        public const string OutputFileTestMethod = "  try\n  {{\n    {0}::{1}::{2}();\n    std::cout << \"Passed {2}()\\n\";\n  }}\n  catch(std::runtime_error error)\n  {{\n    success= false;\n"
            + "    std::cout << \"Failed {2}()\\n\";\n  }}";

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
