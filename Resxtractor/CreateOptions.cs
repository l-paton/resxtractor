using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using ResXResourceWriter = ICSharpCode.Decompiler.Util.ResXResourceWriter;

namespace Resxtractor
{
    [Verb("create", HelpText = "Create directory.")]
    public class CreateOptions
    {
        [Option('d', "directory", HelpText = "Input directory of cshtml files", Default = "", Required = true)]
        public string Directory { get; set; }

        [Option('r', "resourceDirectory", HelpText = "Input directory of resources", Required = true)]
        public string ResourceDirectory { get; set; }

        private static int ReferenciasPuestas = 0;
        private static int ReferenciasNoPuestas = 0;

        public static int Create(CreateOptions opts)
        {
            Console.WriteLine("1. Crear recursos y poner referencias");
            Console.WriteLine("2. Crear recursos");
            Console.WriteLine("3. Poner referencias");
            Console.WriteLine("¿1, 2 o 3?");

            var r = Console.ReadLine();

            if (r.Equals("1"))
            {
                CreateReSources(opts);
                CreateReferences(opts);
            }
            else if (r.Equals("2"))
            {
                CreateReSources(opts);
            }
            else if (r.Equals("3"))
            {
                CreateReferences(opts);
            }
            else
            {
                Console.WriteLine("Opción inválida");
                return 1;
            }

            Console.WriteLine("\n\n----------------- En total se han puesto " + ReferenciasPuestas + " referencias -----------------");
            Console.WriteLine("\n\n----------------- En total no se han puesto " + ReferenciasNoPuestas + " referencias -----------------");

            return 0;
        }
        public static int CreateReSources(CreateOptions opts)
        {
            if (System.IO.Directory.Exists(opts.Directory))
            {
                if (!System.IO.Directory.Exists(opts.ResourceDirectory))
                {
                    System.IO.Directory.CreateDirectory(opts.ResourceDirectory);
                }

                IterateFiles(System.IO.Directory.EnumerateFileSystemEntries(opts.Directory).ToArray(), opts.ResourceDirectory, false);
            }
            else
            {
                Console.WriteLine("Directory: " + opts.Directory + " doesn't exists.");
            }
            return 0;
        }

        public static int CreateReferences(CreateOptions opts)
        {
            if (System.IO.Directory.Exists(opts.Directory))
            {
                IterateFiles(System.IO.Directory.EnumerateFileSystemEntries(opts.Directory).ToArray(), opts.ResourceDirectory, true);
            }
            else
            {
                Console.WriteLine("Directory: " + opts.Directory + " doesn't exists.");
            }

            return 0;
        }

        public static void CshtmlToResource(string resourceFile, string file, string nameFile)
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            Stream stream = System.IO.File.OpenRead(file);
            var document = context.OpenAsync(req => req.Content(stream)).Result;

            var regx = new Regex(@"[(*)][)]"); 
            var nodes = document.Body.Descendents()
                .Where(o => o.NodeType == NodeType.Text
                && !(o.ParentElement is AngleSharp.Html.Dom.IHtmlScriptElement)
                && o.Text().Trim() != ""
                && !o.Text().Contains("}")
                && !o.Text().Contains("{")
                && !o.Text().Contains("//")
                && !o.Text().Contains("@")
                && !regx.IsMatch(o.Text().Trim())
                && !(o.Text().Trim().Length == 1 && Char.IsPunctuation(o.Text().Trim()[0])))
                .ToList();

            var dictionary = new Dictionary<string, string>();
            int id = 0;

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
            int id = 0;

            ResXResourceWriter writer = new ResXResourceWriter(path);

            if (System.IO.File.Exists(path))
            {
                ResXResourceReader reader = new ResXResourceReader(path);

                var enumerator = reader.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    writer.AddResource(enumerator.Key.ToString(), enumerator.Value.ToString());
                    id++;
                }
            }

            foreach (var r in resource.Keys)
            {
                string key = r + "_" + id.ToString();
                writer.AddResource(key, resource[r]);
            }

            writer.Generate();
            writer.Close();

            Console.WriteLine(path + " \nCREATED");
        }

        public static void CreateReferences(string resourceFile, string file)
        {
            int count1 = 0;
            int count2 = 0;

            Console.WriteLine("¿Poner de forma automática en " + file + "? Y/n");
            string r1 = Console.ReadLine();
            string r2 = "";

            string[] dirName = System.IO.Path.GetDirectoryName(file).Split('\\');

            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            Stream stream = System.IO.File.OpenRead(file);

            var document = context.OpenAsync(req => req.Content(stream)).Result;

            string contents = document.Source.Text;

            contents = Regex.Replace(contents, "(?<!\r)\r", "");

            var regx = new Regex(@"[(*)][)]");

            var nodes = document.Body.Descendents()
                .Where(o => o.NodeType == NodeType.Text
                && o.Text().Trim() != ""
                && !o.Text().Contains("}")
                && !o.Text().Contains("{")
                && !o.Text().Contains("@")
                && !regx.IsMatch(o.Text().Trim())
                && !(o.Text().Trim().Length == 1 && Char.IsPunctuation(o.Text().Trim()[0])))
                .ToList();

            if (nodes != null)
            {
                string[] resourceDirName = System.IO.Path.GetFullPath(resourceFile).Split('\\');

                foreach (var node in nodes)
                {
                    string key = CheckIfExists(resourceFile, node.Text().Trim(), System.IO.Path.GetFileNameWithoutExtension(file));
                    if (key != null)
                    {

                        if (!r1.ToUpper().Equals("y".ToUpper()))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n\n-Quieres cambiar: \n\n");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine(node.Text().Trim());
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n\n-POR: \n\n");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine("@" + resourceDirName[resourceDirName.Length - 4] + "." + resourceDirName[resourceDirName.Length - 3] + "." + resourceDirName[resourceDirName.Length - 2] + "." + resourceDirName[resourceDirName.Length - 1].Replace(".resx", "") + "." + key);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n\n-Y/n");
                            Console.ForegroundColor = ConsoleColor.White;
                            r2 = Console.ReadLine();
                        }

                        var parentWithoutModify = node.ParentElement.OuterHtml.Replace("<br>", "<br />").Replace("<hr>", "<hr />");
                        string lastcontents = contents;

                        if (r1.ToUpper().Equals("y".ToUpper()) || r2.ToUpper().Equals("y".ToUpper()))
                        {
                            node.TextContent = node.TextContent.Replace(node.Text().Trim(), "@" + resourceDirName[resourceDirName.Length - 4] + "." + resourceDirName[resourceDirName.Length - 3] + "." + resourceDirName[resourceDirName.Length - 2] + "." + resourceDirName[resourceDirName.Length - 1].Replace(".resx", "") + "." + key);
                            contents = contents.ReplaceFirst(parentWithoutModify, node.ParentElement.OuterHtml.Replace("<br>", "<br />").Replace("<hr>", "<hr />"));
                        }

                        if (lastcontents.Equals(contents))
                        {
                            count1++;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n\n" + parentWithoutModify + " \n\n ----------- No se va a cambiar -----------\n\n");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else
                        {
                            count2++;
                            ReferenciasPuestas++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("\n\n----------- Se va a cambiar -----------\n\n");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }
                }
            }

            document.Close();
            stream.Close();

            if (count1 > 0)
            {
                Console.WriteLine("\n\n" + count1.ToString() + " referencias no se han puesto en " + file);
                ReferenciasNoPuestas += count1;
            }

            if (count2 > 0)
            {
                System.IO.File.WriteAllText(System.IO.Path.GetFullPath(file), contents, Encoding.UTF8);
            }

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

        public static void IterateFiles(string[] files, string resourceDir, bool references)
        {
            foreach (var file in files)
            {
                if (System.IO.File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    IterateFiles(System.IO.Directory.GetFiles(file), resourceDir, references);
                }
                else
                {
                    string[] dirName = System.IO.Path.GetDirectoryName(file).Split('\\');

                    if (!System.IO.Directory.Exists(resourceDir + "\\" + dirName[dirName.Length - 1]))
                    {
                        System.IO.Directory.CreateDirectory(resourceDir + "\\" + dirName[dirName.Length - 1]);
                    }

                    string resourceFile = resourceDir + "\\" + dirName[dirName.Length - 1] + "\\" + dirName[dirName.Length - 1] + ".resx";

                    if (references)
                    {
                        if (System.IO.File.Exists(resourceFile))
                        {
                            CreateReferences(resourceFile, file);
                        }
                    }
                    else
                    {
                        CshtmlToResource(resourceFile, file, System.IO.Path.GetFileNameWithoutExtension(file));
                    }
                }
            }
        }
    }
}
