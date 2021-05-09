using CommandLine;
using System;

namespace Resxtractor
{
    public class Program
    {
        static int Main(string[] args)
        {
            try
            {
                return Parser.Default.ParseArguments<CreateOptions>(args)
                    .MapResult(
                        (CreateOptions opts) => CreateOptions.Create(opts),
                        errs => 1
                    );
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
