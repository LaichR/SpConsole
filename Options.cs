using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

using SpLib;

namespace SpConsole
{
    abstract class Options
    {
        [Option('u', "user", HelpText = "User name of sharepoint repo")]
        
        public string User { get; set; }

        [Option( 'p', "password", HelpText ="Password of specified user")]
        public string Password { get; set; }

        public abstract void Accept(IOptionVisitor visitor);
    }

    [Verb("find", HelpText = "Find one or more Items item within the document library")]

    class FindOptions: Options
    {
        [Option('s', "site", HelpText="List of sites that shall be searched.")]
        public IEnumerable<string> Sites { get; set; }

        [Option('n', "name", HelpText ="Literal of regular search expression that shall match the name of the searched item")]
        public string Name { get; set; }

        [Option("re", HelpText ="Search as regular expression", Default =false )]
        public bool IsRegex { get; set; }

        [Option('r', HelpText = "Search recursive")]
        public bool Recursiv { get; set; }

        public override void Accept(IOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    [Verb("exec", HelpText = "Execute the contents of the command file.")]
    class ExecOptions : Options
    {
        [Option('b',"batch", Required=true, HelpText ="Name of file containing instructions that shall be executed.")]
        public string Batch { get; set; }

        public override void Accept(IOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    [Verb("push", HelpText = "Uplaod files to a sharepoint folder of one or many sites")]
    class UploadOptions : Options
    {
        [Option('s', "site", HelpText = "List of sites.")]
        public IEnumerable<string> Sites { get; set; }

        [Option('f', "files", HelpText ="List of files that shall be loaded to sharepoint.")]
        public IEnumerable<string> Files { get; set; }

        [Option('d', "dest", HelpText ="Destination folder where the files shall be pushed.")]
        public string Destination { get; set; }

        [Option('c', "create", HelpText = "Flag to indicate that a missing folder shall be created", Default = false)]
        public bool DoCreateMissingFolder
        {
            get;
            set;
        }

        public override void Accept(IOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

    }
    
    [Verb("pull", HelpText = "Download files from a sharepoint folder of one or many sites to a destination folder")]
    class DownloadOptions : Options
    {
        [Option('s', "site", HelpText = "List of sites.")]
        public IEnumerable<string> Sites { get; set; }

        [Option('f', "folders", HelpText = "List of folders that shall be downloaded.")]
        public IEnumerable<string> Folders { get; set; }

        [Option('d', "dest", HelpText = "Name of folder where the content shall be stored locally.")]
        public string Destination { get; set; }

        public override void Accept(IOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }



}