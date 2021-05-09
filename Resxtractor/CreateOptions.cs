using AngleSharp;
using AngleSharp.Dom;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using ResXResourceWriter = ICSharpCode.Decompiler.Util.ResXResourceWriter;

namespace Resxtractor
{
    [Verb("create", HelpText = "Create directory.")]
    public class CreateOptions
    {

        [Option('o', "out", HelpText = "Output path", Default = "", Required = true)]
        public string Output { get; set; }

        [Option('d', "directory", HelpText = "Input directory of cshtml files", Default = "", Required = true)]
        public string Directory { get; set; }

        public static int id = 0;

        public static int Create(CreateOptions opts)
        {
            if (System.IO.Directory.Exists(opts.Directory))
            {
                IterateFiles(System.IO.Directory.EnumerateFileSystemEntries(opts.Directory).ToArray());
                CreateReferences(opts);
            }
            else
            {
                Console.WriteLine("Directory: " + opts.Directory + " doesn't exists.");
            }

            return 0;
        }

        public static void IterateFiles(string[] files)
        {
            foreach (var file in files)
            {
                if (System.IO.File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    IterateFiles(System.IO.Directory.GetFiles(file));
                }
                else
                {
                    string languageDir = System.IO.Path.GetDirectoryName(file) + "\\..\\..\\" + "Language";

                    if (!System.IO.Directory.Exists(languageDir))
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file) + "\\..\\..\\" + "Language");
                    }

                    string[] dirName = System.IO.Path.GetDirectoryName(file).Split('\\');
                    string resourceFile = System.IO.Path.GetDirectoryName(file) + "\\..\\..\\" + "Language\\" + dirName[dirName.Length - 1] + ".resx";
                    CshtmlToResource(resourceFile, file, System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
        }


        public static void CshtmlToResource(string resourceFile, string file, string nameFile)
        {

            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            Stream stream = System.IO.File.OpenRead(file);
            var document = context.OpenAsync(req => req.Content(stream)).Result;
            var regx = new Regex(@"[(*)]");
            var nodes = document.Body.Descendents()
                .Where(o => o.NodeType == NodeType.Text
                && o.Text().Trim() != ""
                && !o.Text().Contains("@")
                && !o.Text().Contains("{")
                && !o.Text().Contains("}")
                && !regx.IsMatch(o.Text().Trim())
                && !(o.Text().Trim().Length == 1 && Char.IsPunctuation(o.Text().Trim()[0])))
                .ToList();

            var dictionary = new Dictionary<string, string>();

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    dictionary.Add(nameFile + "_" + id, node.Text().Trim());
                    id++;
                }
            }

            stream.Close();
            CreateOrUpdateResxFile(resourceFile, dictionary);

        }

        public static void CreateOrUpdateResxFile(string path, Dictionary<string, string> resource)
        {

            ResXResourceWriter writer = new ResXResourceWriter(path);

            if (System.IO.File.Exists(path))
            {
                ResXResourceReader reader = new ResXResourceReader(path);

                var enumerator = reader.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    writer.AddResource(enumerator.Key.ToString(), enumerator.Value.ToString());
                }
            }

            foreach (var r in resource.Keys)
            {
                writer.AddResource(r.ToString(), resource[r]);
            }

            writer.Generate();
            writer.Close();

            Console.WriteLine(path + " \nCREATED");
        }

        /***************** REFERENCIAS AL RECURSO ********************/

        public static int CreateReferences(CreateOptions opts)
        {
            if (System.IO.Directory.Exists(opts.Directory))
            {
                CheckWords(System.IO.Directory.EnumerateFileSystemEntries(opts.Directory).ToArray());
            }
            else
            {
                Console.WriteLine("Directory: " + opts.Directory + " doesn't exists.");
            }

            return 0;
        }

        public static void CheckWords(string[] files)
        {
            foreach (var file in files)
            {
                if (System.IO.File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    CheckWords(System.IO.Directory.GetFiles(file));
                }
                else
                {
                    CreateReferences(file);
                }
            }
        }


        public static void CreateReferences(string file)
        {

            string[] dirName = System.IO.Path.GetDirectoryName(file).Split('\\');
            string resourceFile = System.IO.Path.GetDirectoryName(file) + "\\..\\..\\" + "Language\\" + dirName[dirName.Length - 1] + ".resx";

            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            Stream stream = System.IO.File.OpenRead(file);
            var document = context.OpenAsync(req => req.Content(stream)).Result;


            string contents = document.Source.Text;
            contents = Regex.Replace(contents, "(?<!\r)\r", "");

            var regx = new Regex(@"[(*)]");
            var nodes = document.Body.Descendents()
                .Where(o => o.NodeType == NodeType.Text
                && o.Text().Trim() != ""
                && !o.Text().Contains("@")
                && !o.Text().Contains("{")
                && !o.Text().Contains("}")
                && !regx.IsMatch(o.Text().Trim())
                && !(o.Text().Trim().Length == 1 && Char.IsPunctuation(o.Text().Trim()[0])))
                .ToList();

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    string key = CheckIfExists(resourceFile, node.Text().Trim(), System.IO.Path.GetFileNameWithoutExtension(file));
                    if (key != null)
                    {
                        string[] resourceDirName = System.IO.Path.GetFullPath(resourceFile).Split('\\');
                        var parentWithoutModify = node.ParentElement.OuterHtml;
                        node.TextContent = node.TextContent.Replace(node.Text().Trim(), "@" + resourceDirName[resourceDirName.Length - 3] + "." + resourceDirName[dirName.Length - 2] + "." + resourceDirName[resourceDirName.Length - 1].Replace(".resx", "") + "." + key);
                        contents = contents.Replace(parentWithoutModify, node.ParentElement.OuterHtml);
                    }
                }
            }

            document.Close();
            stream.Close();
            //System.IO.File.WriteAllText(System.IO.Path.GetFullPath(file), contents);
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(file), document.Body.OuterHtml);
        }

        private static string CheckIfExists(string resourceFile, string htmlValue, string keySubName)
        {

            ResXResourceReader reader = new ResXResourceReader(resourceFile);

            var enumerator = reader.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (htmlValue == enumerator.Value.ToString())
                {
                    if (enumerator.Key.ToString().Contains(keySubName))
                    {
                        return enumerator.Key.ToString();
                    }
                }
            }

            return null;
        }
    }
}
