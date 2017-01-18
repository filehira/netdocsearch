using LibGit2Sharp;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GitClone
{
    class Program
    {
        private static string _localPath = @"C:\Users\safern\Desktop\tempClone\";
        static void Main(string[] args)
        {
            Colorizer.WriteLine("[Yellow!What repo do you want to clone?]");
            var repoUrl = Console.ReadLine();
            //if (!Repository.IsValid(repoUrl))
            //{
            //    Colorizer.WriteLine("[Red!Invalid repo url]");
            //    return;
            //}

            Console.WriteLine();
            Colorizer.WriteLine($"[Cyan!Cloning repo {repoUrl}...]");
            Console.WriteLine();

            //var result = Repository.Clone(repoUrl, _localPath);

            Colorizer.WriteLine($"[Green!Done cloning repo in {_localPath} ]");

            Console.WriteLine();
            Console.WriteLine("Indexing md files...");
            AddMDFilesToData(new DirectoryInfo(_localPath));
            LuceneSearch.AddUpdateLuceneIndex(LuceneIndexData.Data);
            Console.WriteLine();
            Colorizer.WriteLine("[Green!Done Indexing files!]");

            StartSearch();
        }

        public static void StartSearch()
        {
            string query = string.Empty;
            do
            {
                Console.WriteLine();
                Colorizer.WriteLine("[Yellow!What do you want to search?]");
                query = Console.ReadLine();
                var results = LuceneSearch.Search(query);
                Colorizer.WriteLine("Here are the results:");
                PrintResults(results);

            } while (!query.Equals("quit"));
            
        }

        public static void PrintResults(IEnumerable<LuceneDataInfo> results)
        {
            Console.WriteLine();
            Console.WriteLine("==========================");
            var i = 1;
            foreach (var result in results)
            {
                Console.WriteLine($"{i++} File: {result.Name} -- Path: {result.FullName} -- Score: {result.Score}");
            }
            Console.WriteLine("==========================");
        }

        public static void AddMDFilesToData(DirectoryInfo currentDir)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            try
            {
                files = currentDir.GetFiles();
                subDirs = currentDir.GetDirectories();
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Extension.Equals(".md", StringComparison.InvariantCultureIgnoreCase))
                    {
                        LuceneIndexData.Data.Add(new LuceneDataInfo(file.Name, file.FullName, File.ReadAllText(file.FullName)));
                    }
                }
            }

            if (subDirs != null)
            {
                foreach (var subdir in subDirs)
                {
                    AddMDFilesToData(subdir);
                }
            }
        }

    }
}
