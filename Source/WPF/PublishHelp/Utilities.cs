using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace PublishHelp
{
    public class Utilities
    {

        public static char FromOctet(char high, char low)
        {
            return Convert.ToChar((HexDigit(high) * 16) + HexDigit(low));
        }

        public static int HexDigit(char ch)
        {
            if (ch >= '0' && ch <= '9')
            {
                return (int)(ch - '0');
            }
            else if (ch >= 'A' && ch <= 'F')
            {
                return (int)(ch - 'A') + 10;
            }
            else if (ch >= 'a' && ch <= 'f')
            {
                return (int)(ch - 'a') + 10;
            }
            throw new Exception("'" + ch + "' is not a hexidecimal digit");
        }

        public static bool ExtractEmbeddedResourceAsFile(string name, string path)
        {
            using (Stream s = typeof(Utilities).Assembly.GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[64000];
                    int len = 0;
                    while ((len = s.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, len);
                    }
                    fs.Close();
                }
            }
            return true;
        }

    }
}
