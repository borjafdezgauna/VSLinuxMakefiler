using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

namespace VSLinuxMakefiler
{
    public class UnitTestClass
    {
        public string Name { get; } = null;
        public List<string> Methods { get; } = new List<string>();

        const string testMethodRegEx = @"TEST_METHOD\s*\((\w+)\)\s*\r*\n*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";

        public UnitTestClass(string name,string classContent)
        {
            Name = name;
            foreach (Match match in Regex.Matches(classContent, testMethodRegEx))
                Methods.Add(match.Groups[1].Value);
        }
    }
    public class UnitTestNamespace
    {
        public string Name { get; } = null;
        public List<UnitTestClass> Classes { get; } = new List<UnitTestClass>();

        const string classRegEx = @"TEST_CLASS\s*\((\w+)\)\s*\r*\n*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";

        public UnitTestNamespace(string name, string namespaceContent)
        {
            Name = name;
            foreach (Match match in Regex.Matches(namespaceContent, classRegEx))
                Classes.Add(new UnitTestClass(match.Groups[1].Value, match.Groups[2].Value));
        }
    }

    public class VSUnitTestProjectParser
    {
        const string namespaceRegEx = @"namespace\s+([\w\.]+)\s*\r*\n*\{((?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!)))\}";

        static public List<UnitTestNamespace> Parse (string sourceFile)
        {
            string sourceFileCode = File.ReadAllText(sourceFile);

            List<UnitTestNamespace> namespaces = new List<UnitTestNamespace>();

            foreach (Match match in Regex.Matches(sourceFileCode, namespaceRegEx))
                namespaces.Add(new UnitTestNamespace(match.Groups[1].Value, match.Groups[2].Value));

            return namespaces;
        }
    }
}
