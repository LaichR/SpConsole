using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using SP = Microsoft.SharePoint.Client;


namespace SpLib
{
    public class SharepointDocLibrary : ISharepointList
    {
        static readonly byte[] _downloadBuffer = new byte[1024 * 16];
        readonly SP.ClientContext _context;
        readonly SP.List _me;

        public EventHandler<Exception> OnException;

        class DownloadStatus
        {
            readonly SortedSet<string> _downloadedFiles = new SortedSet<string>();
            readonly SharepointDocLibrary _lib;

            public DownloadStatus(SharepointDocLibrary library)
            {
                _lib = library;
            }

            public void DownloadAllFilesInFolder(string targetDir, SP.Folder folder, Action<SP.File> fileAction, int take = -1)
            {
                _lib._context.Load(folder.Files);
                _lib._context.Load(folder.Folders);
                _lib._context.ExecuteQuery();
                foreach( var f in folder.Files )
                {
                    _lib._context.Load(f); _lib._context.ExecuteQuery();
                    fileAction(f);
                    DowloadDocument(targetDir, f, take);
                }
                foreach( var f in folder.Folders)
                {
                    DownloadAllFilesInFolder(targetDir, f, fileAction, take);
                }
            }

            public void DowloadDocument( string targetDir, SP.File file, int take=-1)
            {
                var serverPath = file.ServerRelativeUrl;
                if (!_downloadedFiles.Contains(serverPath))
                {
                    _downloadedFiles.Add(serverPath);
                    _lib.DownloadDocument(targetDir, file, take);
                }
            }
            
        }

        public SharepointDocLibrary(string url, string docLibName, string user, string password)
        {
            
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(user), "user must not be null", "user");
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(user), "password must not be null", "user");

            Contract.RequiresArgumentNotNull(docLibName, "docLibName");
            Contract.Requires<ArgumentException>(
                Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute), 
                "{0} is not a well formed url", url);
            Contract.RequiresArgumentNotNull(url, "url");
            _context = new SP.ClientContext(url);
            System.Security.SecureString pw = new System.Security.SecureString();
            foreach (var ch in password) pw.AppendChar(ch);


            _context.Credentials = new SP.SharePointOnlineCredentials(user, pw);

            _context.Load(_context.Web.CurrentUser);
            _context.ExecuteQuery();

            _me = _context.Web.Lists.GetByTitle(docLibName);

            _context.Load(_me);
            _context.ExecuteQuery();
        }

        public SP.ClientContext Context
        {
            get { return _context; }
        }

        public string Url
        {
            get { return _context.Url; }
        }

        /// <summary>
        /// Selects a document based of some properties; the current filter only takes into acount the visible file name;
        /// This is of course very primitive. If a user decides to use the documentId this should work as well and it would allow to change
        /// the name of the document without breaking the link!
        /// 
        /// </summary>
        /// <param name="searchFilter"></param>
        /// <returns></returns>
        public IEnumerable<SharepointListElement> SelectElements(IEnumerable<Tuple<string, string, string>> searchFilter)
        {
            List<SharepointListElement> elements = new List<SharepointListElement>();
            _context.Load(_context.Web);
            _context.ExecuteQuery();
            SP.CamlQuery query = new SP.CamlQuery()
            {
                ViewXml = $@"<View>
                            <Query>
                                <Where>{MakeCamlFilter(searchFilter)}
                                </Where>
                            </Query>
                        </View>",
                FolderServerRelativeUrl = _context.Web.ServerRelativeUrl

            };

            SP.ListItemCollection listItems = _me.GetItems(query);
            _context.Load(listItems);
            _context.ExecuteQuery();

            foreach (var li in listItems)
            {
                elements.Add(new SharepointListElement(this, li));
            }

            return elements;
        }



        string MakeCamlFilter(IEnumerable<Tuple<string, string, string>> searchFilter)
        {
            if (searchFilter == null) return "";
            var filterTemplate = @"
                                    <Eq>
                                        <FieldRef Name='{0}' />
                                        <Value Type='{1}'>{2}</Value>
                                    </Eq>";
            StringBuilder camlFilter = new StringBuilder();
            foreach (var t in searchFilter)
            {
                camlFilter.Append(string.Format(filterTemplate, t.Item1, t.Item2, t.Item3));
            }
            return camlFilter.ToString();
        }


        SP.ListItemCollection QueryItems(IEnumerable<Tuple<string, string, string>> searchFilter)
        {
            SP.CamlQuery query = new SP.CamlQuery()
            {
                ViewXml =
                    string.Format(@"<View>
                            <Query>
                                <Where>{0}
                                </Where>
                            </Query>
                        </View>", MakeCamlFilter(searchFilter))
            };

            query.FolderServerRelativeUrl = _context.Web.ServerRelativeUrl;
            SP.ListItemCollection listItems = _me.GetItems(query);
            _context.Load(listItems);
            _context.ExecuteQuery();
            return listItems;
        }

        public SharePointFolder GetFolder(string folderPath)
        {
            var folderSnipptets = StringHelper.SplitIntoFolderSnippets(folderPath);
            return GetFolder(folderSnipptets);
        }

        public SharePointFolder GetFolder(StringHelper.FolderSnippet[] folders)
        {
            var folder = _context.Web.RootFolder;
            if( folders.Any())
            {
                if( folders[0].UsesRegex )
                {
                    var re = new Regex(folders[0].FolderPart);
                    _context.Load(folder.Folders);
                    _context.ExecuteQuery();
                    var matchingFolder = folder.Folders.Where(x => re.IsMatch(x.Name));
                    if(!matchingFolder.Any()) 
                        throw new System.IO.DirectoryNotFoundException($"No folder name is matching the regular expression{folders[0].FolderPart}");
                    folder = matchingFolder.First();
                }
                else
                {
                    folder = _context.Web.GetFolderByServerRelativeUrl( folders[0].FolderPart );
                    _context.Load(folder);
                    try
                    {
                        _context.ExecuteQuery();
                    }
                    catch(Exception ex)
                    {
                        throw new System.IO.DirectoryNotFoundException(
                            $"No folder with name {folders[0].FolderPart} found", ex);
                    }

                }
            }
            return new SharePointFolder(this, folder);
        }

        public SharePointFolder GetOrCreateFolder(string folderPath)
        {
            var folderNames = StringHelper.Unquote(folderPath).Split('/'); // when we create new folders, we cannot deal with wildcards
            var firstName = "";
            if( folderNames.Any() )
            {
                firstName = folderNames.First();
            }
            SharePointFolder spFolder;
            try
            {
                spFolder = GetFolder(firstName);
            }
            catch(System.IO.DirectoryNotFoundException)
            {
                spFolder = CreateFolder("", firstName);
            }
            for(int i = 1; i< folderNames.Length; i++)
            {
                spFolder = spFolder.GetOrCreateSubfolder(folderNames[i]);
            }
            
            return spFolder;
        }

        public SharePointFolder CreateFolder(string folderPath, string newFolderName)
        {
            var folder = GetFolder(folderPath);
            folder = folder.GetOrCreateSubfolder(newFolderName);
            return folder;
        }



        public void DownloadFiles(string targetDir, int maxurlExt, string searchPath, bool recursiv, 
            Action<SP.File> fileAction)
        {

            var pathSnippets = StringHelper.SplitIntoFolderSnippets(searchPath);

            var folder = GetFolder(pathSnippets);
            var status = new DownloadStatus(this);
            folder.FindFileOrFolder(pathSnippets, 1, recursiv,
                x => { status.DownloadAllFilesInFolder(targetDir, x, fileAction, maxurlExt); },
                x => { fileAction(x); status.DowloadDocument(targetDir, x, maxurlExt);} );
        }

        public void DownloadDocument(string targetDir, SP.File f, int take = -1)
        {
            //_context.Load(f);
            //_context.ExecuteQuery();

            var result = f.OpenBinaryStream();
            _context.Load(f);
            _context.ExecuteQuery();

            var offset = 0;
            List<string> pathComponents = new List<string>();
            pathComponents.AddRange(targetDir.Trim('"', '\'').Split('\\'));
            var serverRelativePath = f.ServerRelativeUrl.Split('/');
            if (take == -1)
            {
                take = serverRelativePath.Length - 2;
            }
            pathComponents.AddRange(serverRelativePath.Skip(serverRelativePath.Length - take)); // sites must not be in the path, it's the second element!
            var fileName = string.Join("\\", pathComponents.ToArray());
            SharepointDocLibrary.AssertValidPath(System.IO.Path.GetDirectoryName(fileName));
            using (var stream = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                var nrOfBytes = result.Value.Read(_downloadBuffer, offset, _downloadBuffer.Length);

                while (nrOfBytes > 0)
                {
                    stream.Write(_downloadBuffer, 0, nrOfBytes);
                    nrOfBytes = result.Value.Read(_downloadBuffer, offset, _downloadBuffer.Length);
                }
            }
        }

        //public void DownloadFolder(string targetDir, SharePointFolder folder, bool recursive)
        //{

        //    folder.DownloadAllFiles(targetDir, recursive, -1);

        //}

        static public void AssertValidPath(string directoryName)
        {
            Contract.RequiresArgumentNotNull(directoryName, "directoryName");
            if (!System.IO.Directory.Exists(directoryName))
            {
                System.IO.Directory.CreateDirectory(directoryName);
            }
        }

        internal void NotifyException( Exception ex)
        {
            OnException?.Invoke(this, ex);
        }

    }

}
