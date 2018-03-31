using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PublishHelp
{
    class PageInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string FileName { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    class UniqueFileNames
    {
        HashSet<string> unique = new HashSet<string>();

        public string Add(string filename)
        {
            if (!unique.Contains(filename.ToLowerInvariant()))
            {
                unique.Add(filename.ToLowerInvariant());
                return filename;
            }

            int i = 1;
            while (true)
            {
                string uniqueName = filename + i;
                if (!unique.Contains(uniqueName.ToLowerInvariant()))
                {
                    unique.Add(uniqueName.ToLowerInvariant());
                    return uniqueName;
                }
                i++;
            }
        }
    }
}
