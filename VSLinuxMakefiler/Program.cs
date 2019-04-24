using System;
using System.Collections.Generic;

namespace VSLinuxMakefiler
{
    class Program
    {
        static string solutionFilenameArg = "solution=";
        static string solutionFilename = null;

        static string linuxCppUnitTestFilePathArg = "cppUnitTestHeader=";
        static string linuxCppUnitTestFilePath = null;

        static void Main(string[] args)
        {
            //Parse arguments
            foreach(string arg in args)
            {
                if (arg.StartsWith(solutionFilenameArg)) solutionFilename = arg.Substring(solutionFilenameArg.Length);
                else if (arg.StartsWith(linuxCppUnitTestFilePathArg)) linuxCppUnitTestFilePath = arg.Substring(linuxCppUnitTestFilePathArg.Length);
            }
            
            //Check required arguments
            if (solutionFilename == null)
            {
                Console.WriteLine("ERROR. Usage VSLinuxMaker " + solutionFilenameArg + "<*.sln>");
                return;
            }

            VSSolution solutionParser = new VSSolution();

            if (solutionParser.Parse(solutionFilename))
                solutionParser.GenerateBuildFile(linuxCppUnitTestFilePath);
        }
    }
}
