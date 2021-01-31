using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPChild
{
    public class Common
    {
        /// <summary>
        /// Get child directories.
        /// </summary>
        /// <param name="rootDir">Root directory where the search begins.</param>
        /// <returns>The directory list.</returns>
        public static List<string> GetAllChildDir(string rootDir)
        {
            try
            {
                List<string> dirList = new List<string>();
                if (!Directory.Exists(rootDir))
                    throw new DirectoryNotFoundException("Directory doesn't exist.");

                DirectoryInfo directoryInfo = new DirectoryInfo(rootDir);
                foreach (DirectoryInfo d in directoryInfo.GetDirectories())
                {
                    dirList.Add(d.FullName);
                }

                return dirList;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Check folder exists, if not then create.
        /// </summary>
        /// <param name="path">Path to be checked.</param>
        public static void CheckExists(string path)
        {
            try
            {
                if ((string.IsNullOrWhiteSpace(path)) || (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0))
                    throw new FormatException("Invalid folder name!");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
        }
    }
}
