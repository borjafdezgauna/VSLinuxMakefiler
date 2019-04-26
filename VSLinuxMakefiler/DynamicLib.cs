using System;
using System.Collections.Generic;
using System.Text;

namespace VSLinuxMakefiler
{
    public class DynamicLib: VSProject
    {
        public override ProjectType Type() { return ProjectType.DynamicLibrary; }
        public override string SolutionRelativeOutputFile()
        {
            return TempProjectFolder + Name + ".so";
        }

        public DynamicLib(string name, string projectPath, string solutionPath) : base(name, projectPath, solutionPath)
        {
        }

        public override string CompilerFlags(string sourceFile)
        {
            string langFlags = "";
            if (sourceFile.EndsWith(".c")) langFlags = "-x c -std=c11 ";
            else if (sourceFile.EndsWith(".cpp")) langFlags = "-x c++ -std=c++11 ";

            string flags = m_commonCompilerFlags + " " + langFlags + m_compileLibraryFlags + " ";

            return flags;
        }

        public override string LinkerFlags()
        {
            return "-Wl,--no-undefined -Wl,-z,relro -Wl,-z,now -Wl,-z,noexecstack -shared ";
        }
    }
}
