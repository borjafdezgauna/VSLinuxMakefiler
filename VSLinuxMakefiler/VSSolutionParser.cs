using System;
using System.Collections.Generic;
using System.Text;

namespace VSLinuxMakefiler
{
    public class VSSolutionParser
    {
        const string ProjDefPattern = "Project\\([^\\)]+\\)\s*=\\s*\"([^\"]+)\"\\s*\\,\\s*\"([^\"]+)\"\\s*\\,\\s*";

        public VSSolutionParser(string solutionFilename)
        {

        }
    }
}
