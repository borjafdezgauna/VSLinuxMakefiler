using System;
using System.Collections.Generic;

namespace VSLinuxMakefiler
{
    class Program
    {
        static string solutionFilenameArg = "solution=";
        static string solutionFilename = null;

        static string linuxCppUnitTestHeaderDirArg = "cppUnitTestHeaderDir=";
        static string linuxCppUnitTestHeaderDir = null;

        static void Main(string[] args)
        {
            //Parse arguments
            foreach(string arg in args)
            {
                if (arg.StartsWith(solutionFilenameArg)) solutionFilename = arg.Substring(solutionFilenameArg.Length).Replace('\\','/');
                else if (arg.StartsWith(linuxCppUnitTestHeaderDirArg)) linuxCppUnitTestHeaderDir = arg.Substring(linuxCppUnitTestHeaderDirArg.Length).Replace('\\','/');
            }
            
            //Check required arguments
            if (solutionFilename == null)
            {
                Console.WriteLine("ERROR. Usage VSLinuxMaker " + solutionFilenameArg + "<*.sln>");
                return;
            }

            VSSolution solutionParser = new VSSolution();

            if (solutionParser.Parse(solutionFilename, linuxCppUnitTestHeaderDir))
                solutionParser.GenerateBuildFile();
        }
    }
}
