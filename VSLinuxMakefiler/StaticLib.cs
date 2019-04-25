using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VSLinuxMakefiler
{
    public class StaticLib: VSProject
    {
        public override ProjectType Type() { return ProjectType.StaticLibrary; }
        public override string SolutionRelativeOutputFile()
        {
            return TempProjectFolder + Name + ".a";
        }

        public StaticLib(string name, string projectPath, string solutionPath) : base(name, projectPath, solutionPath)
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
            return "";
        }

        protected override void WriteLinkSources(StreamWriter writer)
        {
            string linkCommand = string.Format("ar rcs {0} {1}*.o {2}", SolutionRelativeOutputFile(), TempProjectFolder, LinkerFlags());
            writer.WriteLine(linkCommand);
        }
    }
}
