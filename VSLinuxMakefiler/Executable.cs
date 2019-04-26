using System;
using System.Collections.Generic;
using System.Text;

namespace VSLinuxMakefiler
{
    public class Executable: VSProject
    {
        public override ProjectType Type() { return ProjectType.Executable; }
        public override string SolutionRelativeOutputFile()
        {
            return TempProjectFolder + Name + ".exe";
        }

        public Executable(string name, string projectPath, string solutionPath) : base(name, projectPath, solutionPath)
        {
        }

        public override string CompilerFlags(string sourceFile)
        {
            string langFlags = "";
            if (sourceFile.EndsWith(".c")) langFlags = "-x c -std=c11 ";
            else if (sourceFile.EndsWith(".cpp")) langFlags = "-x c++ -std=c++11 ";

            string flags = m_commonCompilerFlags + " " + langFlags;

            return flags;
        }

        public override string LinkerFlags()
        {
            return "-Wl,--no-undefined ";
        }
    }
}
