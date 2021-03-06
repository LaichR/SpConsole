using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using SP = Microsoft.SharePoint.Client;

namespace SpLib
{
    public class SharepointDocLibrary : ISharepointList
    {

        readonly SP.ClientContext _context;
        readonly SP.List _me;


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

        public SharePointFolder GetOrCreateFolder(string folderPath)
        {
            var folderNames = folderPath.Trim('"', '\'').Split('/');
            var firstName = "";
            if( folderNames.Any() )
            {
                firstName = folderNames.First();
            }
            SharePointFolder spFolder;
            try
            {
                spFolder = GetFolder(firstName);
            }catch(System.IO.DirectoryNotFoundException ex)
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

        public SharePointFolder GetFolder(string folderPath)
        {
            bool FindFolder(SP.Folder root, string[] folderPathNames, out SP.Folder aFolder)
            {

                bool nameInPath = true;

                for (int pos = 0; pos < folderPathNames.Length && nameInPath; pos++)
                {
                    aFolder = root;
                    nameInPath = false;
                    var folderCollection = root.Folders;
                    _context.Load(root.Folders);
                    _context.ExecuteQuery();
                    foreach (var f in folderCollection)
                    {
                        if (folderPathNames[pos] == f.Name)
                        {
                            root = f;
                            nameInPath = true;
                            break;
                        }
                    }
                }
                aFolder = root;
                return nameInPath;
            }

            if( string.IsNullOrEmpty(folderPath))
            {
                return new SharePointFolder(_context, _context.Web.RootFolder);
            }

            if (FindFolder(_context.Web.RootFolder, folderPath.Trim('"','\'').Split('/'), out SP.Folder folder))
            {
                return new SharePointFolder(_context, folder);
            }
            throw new System.IO.DirectoryNotFoundException($"Sharepoint Folder {folderPath} not found");
        }

        public void DownloadFolder(string targetDir, string folderPath, bool recursive)
        {
            var folder = GetFolder(folderPath);
            DownloadFolder(targetDir, folder, recursive);
            //_context.Load(folder.Directories);
            //foreach
        }

        public void DownloadFolder(string targetDir, SharePointFolder folder, bool recursive)
        {

            folder.DownloadAllFiles(targetDir);
            if (recursive)
            {
                foreach (var f in folder.Folders)
                {
                    DownloadFolder(targetDir, f, recursive);
                }
            }
        }

        static public void AssertValidPath(string directoryName)
        {
            Contract.RequiresArgumentNotNull(directoryName, "directoryName");
            if (!System.IO.Directory.Exists(directoryName))
            {
                System.IO.Directory.CreateDirectory(directoryName);
            }
        }

    }

}
