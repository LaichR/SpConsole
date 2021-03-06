using System;

using CommandLine;
using CommandLine.Text;
using System.Configuration;
using SpLib;
using System.IO;

namespace SpConsole
{


    class Program
    {
        
        static void Main(string[] args)
        {            
            var helpWriter = new StringWriter();
            var parser = new CommandLine.Parser(with =>  with.HelpWriter = helpWriter);
            var result = parser.ParseArguments<FindOptions, UploadOptions, DownloadOptions, ExecOptions>(args)
                .WithParsed(options=> ProcessOptions(options))
                .WithNotParsed(
                errs =>
                    Console.WriteLine(helpWriter.ToString()));
            
        }

        static void ProcessOptions(object obj)
        {
            if (obj is Options options)
            {
                var cr = new CommandRunner(options);
                cr.Run();
            }
        }

    }
}
