using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpLib;
using System.Configuration;
using System.Security;
using System.Text.RegularExpressions;
using CommandLine;

namespace SpConsole
{
    class CommandRunner: IOptionVisitor
    {
        const string DocumentLibTag = "DokumentLibrary";
        const string DefaultUserTag = "DefaultUser";
        const string DefaultSiteTag = "DefaultSite";
        const string DefaultDowloadDestinationTag = "DowloadDest";
        const string DefaultUploadSourceTag = "UploadSrc";
        const string DefaultCmdRootTag = "CmdRoot";
        const string SiteSection = "SiteSectionGroup/Sites";
        
        readonly string _user;
        readonly string _password;
        readonly Options _options;

        public CommandRunner(Options options)
        {
            _user = options.User ?? ConfigurationManager.AppSettings[DefaultUserTag];
            if (string.IsNullOrEmpty(_user)) throw new ArgumentException("A user needs to be specified!");
            _password = options.Password;
            _options = options;
        }

        private CommandRunner(CommandRunner parent, object options)
        {
            if (options is Options opt)
            {
                _options = opt;
            }
            else
            {
                throw new ArgumentException("no valid command options defined");
            }
            _user = parent._user;
            _password = parent._password;
        }

        public void Run()
        {
            _options.Accept(this);
        }

        public void Visit(FindOptions findOptions)
        {
            bool InitSearch(string name, out string folders, out string expr)
            {
                folders = null; expr = null;
                var nameParts = name.Trim('"', '\'').Split('/');
                if (!nameParts.Any()) return false;
                if (nameParts.Length > 1)
                {

                    folders = string.Join("/", nameParts.Take(nameParts.Length - 1));
                }
                expr = nameParts.Last();
                var pos = 0; var wildcards = "*?".ToCharArray();
                var index = expr.IndexOfAny(wildcards, pos);
                List<int> wildcardPositions = new List<int>();
                while ( index >= 0 )
                {
                    wildcardPositions.Add(index);
                    pos = index + 1;
                    index = expr.IndexOfAny(wildcards, pos);
                }
                if( wildcardPositions.Any()) // form a  valid regular expression
                {
                    pos = 0;
                    StringBuilder sb = new StringBuilder();
                    foreach( var wp in wildcardPositions)
                    {
                        while( pos < wp )
                        {
                            if( ".".Contains(expr[pos]))
                            {
                                sb.Append('\\');
                            }
                            sb.Append(expr[pos++]);
                        }
                        switch( expr[pos++])
                        {
                            case '*':
                                
                                
                                if (wp + 1 < expr.Length)
                                {
                                    var followingChar = expr[wp + 1];
                                    var escape = "";
                                    if (".".Contains(followingChar))
                                    {
                                        escape = "\\";
                                    }
                                    var stopChar = $"{escape}{followingChar}";
                                    sb.Append($"([^{stopChar}]*)");
                                }
                                else sb.Append("(.*)");
                                break;
                            case '?': sb.Append('.'); break;
                            default: throw new NotSupportedException();
                        }
                    }
                    while (pos < expr.Length)
                    {
                        if (".".Contains(expr[pos]))
                        {
                            sb.Append('\\');
                        }
                        sb.Append(expr[pos++]);
                    }
                    expr = sb.ToString();
                }
                return true;
            }

            var sites = GetSites(findOptions.Sites);
            Contract.Requires<ArgumentException>(
                InitSearch(findOptions.Name, out string initialFolder, out string searchExpression),
                "invalid argument {0}", findOptions.Name);
            
            foreach( var site in sites)
            {
                var lib = new SharepointDocLibrary(site, DocumentLibrary, _user, _password);

                var folder = lib.GetFolder(initialFolder);
                folder.FindFileOrFolder(searchExpression, findOptions.Recursiv,
                    (x) => Console.WriteLine($"Folder: {x.Name}"), (x) => Console.WriteLine($"File: {x.Name},\n\t url:{x.ServerRelativeUrl}"));
               
            }
        }

        public void Visit(ExecOptions exec)
        {
            Contract.RequiresArgumentNotNull(exec.Batch, "batch");
            string[] SplitLine(string line)
            {
                List<string> arguments = new List<string>();
                StringBuilder sb = new StringBuilder();
                bool withinString = false;
                char strDelim = '"';
                foreach( var ch in line)
                {
                    if ( withinString )
                    {
                        sb.Append(ch);
                        if (ch == strDelim)
                        {
                            withinString = false;
                            arguments.Add(sb.ToString());
                            sb.Clear();
                        }
                        
                    }
                    else // not within string 
                    {
                        if (ch != ' ')
                        {
                            sb.Append(ch);
                            if ("'\"".Contains(ch))
                            {
                                strDelim = ch; withinString = true;
                            }
                        }
                        else
                        {
                            if (sb.Length > 0)
                            {
                                arguments.Add(sb.ToString());
                                sb.Clear();
                            }
                        }
                    }
                }
                arguments.Add(sb.ToString());

                return arguments.ToArray();
            }

            var fileName = exec.Batch.Replace($"${DefaultCmdRootTag}", ConfigurationManager.AppSettings[DefaultCmdRootTag]);
            if( !System.IO.File.Exists(fileName))
            {
                throw new System.IO.FileNotFoundException($"The command file {fileName} could not be found");
            }
            var helpWriter = new System.IO.StringWriter();
            var parser = new CommandLine.Parser(with => with.HelpWriter = helpWriter);
            

            var lines = System.IO.File.ReadAllLines(fileName);
            foreach( var l in lines)
            {
                if (l.StartsWith("#")) continue;
                string[] args = SplitLine(l);

                var result = parser.ParseArguments<FindOptions, UploadOptions, DownloadOptions, ExecOptions>(args)
                .WithParsed(options => new CommandRunner(this, options).Run())
                .WithNotParsed(
                errs =>
                    Console.WriteLine(helpWriter.ToString()));

            }
        }

        public void Visit(DownloadOptions downloadOptions)
        {
            var sites = GetSites(downloadOptions.Sites);
            foreach (var s in sites)
            {
                try
                {
                    var lib = new SharepointDocLibrary(s, DocumentLibrary, _user, _password);
                    
                    foreach (var dir in downloadOptions.Folders)
                    {
                        var src = lib.GetFolder(dir);
                        var dest = downloadOptions.Destination.Replace($"${DefaultDowloadDestinationTag}",
                            ConfigurationManager.AppSettings[DefaultDowloadDestinationTag]).Trim('"','\'');

                        ExecuteAction(()=> src.DownloadAllFiles(dest), $"download {dest}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error downloading files from {s}");
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void Visit(UploadOptions uploadOptions)
        {
            var sites = GetSites(uploadOptions.Sites);
            var destination = uploadOptions.Destination;
            foreach( var s in sites)
            {
                try
                {
                    var lib = new SharepointDocLibrary(s, DocumentLibrary, _user, _password);
                    SharePointFolder dest;
                    if (uploadOptions.DoCreateMissingFolder)
                    {
                        dest = lib.GetOrCreateFolder(uploadOptions.Destination);
                    }
                    else
                    {
                        dest = lib.GetFolder(destination);
                    }
                    foreach (var src in uploadOptions.Files)
                    {
                        var fileName = src.Replace($"${DefaultUploadSourceTag}", ConfigurationManager.AppSettings[DefaultUploadSourceTag]).Trim('"','\'');
                        if(!System.IO.File.Exists(fileName))
                        {
                            throw new System.IO.FileNotFoundException($"File {fileName} does not exisit");
                        }
                        
                        ExecuteAction(()=> dest.UploadDocument(fileName),$"Upload {fileName}");
                    }
                }
                catch( Exception e)
                {
                    Console.WriteLine($"Error uploading files to {s}");
                    Console.WriteLine(e.Message);
                }
            }
        }

        IEnumerable<string> GetSites(IEnumerable<string> sites )
        {
            List<string> siteUrls = new List<string>();
            var siteSection = ConfigurationManager.GetSection(SiteSection) as System.Collections.Specialized.NameValueCollection;
            var siteSectionKeys = new List<string>(siteSection.Keys.OfType<string>());
            
            if ( sites != null && sites.Any())
            {

                foreach( var s in sites )
                {
                    if( s.ToLower().StartsWith("https://"))
                    {
                        siteUrls.Add(s);
                    }
                    else if( !string.IsNullOrEmpty(siteSection[s]))
                    {
                        siteUrls.Add(siteSection[s]);
                    }
                    else if( s.EndsWith("*"))
                    {
                        var searchString = s.Replace("*", "(.*)");
                        var re = new Regex(searchString);
                        foreach( var k in siteSectionKeys)
                        {
                            if( re.IsMatch(k))
                            {
                                siteUrls.Add(siteSection[k]);
                            }
                        }
                    }
                }
            }
            else
            {
                var defaultSite = ConfigurationManager.AppSettings[DefaultSiteTag];
                siteUrls.Add(siteSection[defaultSite]);
            }
            return siteUrls;
        }

        string DocumentLibrary
        {
            get => ConfigurationManager.AppSettings[DocumentLibTag];
        }

        void ExecuteAction( Action action, string startInfo)
        {
            Console.Write(startInfo);
            var timer = new System.Timers.Timer()
            {
                Interval = 1000,
                AutoReset = true,
                Enabled = true,
            };
            timer.Elapsed += CommandRunner_TimerElapsed;
            timer.Start();
            action();
            timer.Stop();
            timer.Elapsed -= CommandRunner_TimerElapsed;
            Console.WriteLine();
            Console.WriteLine("done");
        }

        private void CommandRunner_TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.Write(".");
        }
    }
}
