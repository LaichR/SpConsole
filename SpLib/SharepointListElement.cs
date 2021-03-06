using System.IO;
using SP = Microsoft.SharePoint.Client;

namespace SpLib
{
    public class SharepointListElement
    {
        readonly SP.ListItem _me;
        readonly string _name;
        readonly string _fullPath;
        readonly ISharepointList _parent;

        internal SharepointListElement(ISharepointList parent, SP.ListItem item)
        {
            _parent = parent;
            _me = item;
            _name = item["FileLeafRef"].ToString();
            _fullPath = item["FileRef"].ToString();
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public string Url
        {
            get
            {
                return _fullPath;
            }
        }
        /// <summary>
        /// loads a file from sharepoint
        /// </summary>
        /// <param name="path">visible path to sharepoint file </param>
        /// <param name="version">requested version; if null the latest version is returned</param>
        public void DownloadFromSharepoint(string path, string version = null)
        {
            Contract.RequiresArgumentNotNull(path, "path");

            if (_name != null)
            {
                Stream stream = null;
                try
                {
                    _parent.Context.Load(_me.File);
                    _parent.Context.ExecuteQuery();

                    var url = _me.File.ServerRelativeUrl;

                    // note: the latest version is handled differently than the others!
                    if (version != null && version != (string)_me["_UIVersionString"])
                    {
                        _parent.Context.Load(_me.File.Versions);
                        _parent.Context.ExecuteQuery();

                        foreach (var fv in _me.File.Versions)
                        {
                            if (fv.VersionLabel == version)
                            {
                                // note! here we do use the standard webclient to access the file
                                // this does not work with  the sharepoint client; 
                                // all posts i found in the internet suggest this solution
                                url = string.Join("/", _parent.Url, fv.Url);
                                using (System.Net.WebClient client = new System.Net.WebClient())
                                {
                                    client.UseDefaultCredentials = true;
                                    stream = client.OpenRead(url);
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        // note! here we use the sharepoint client functionality to access the file!
                        var fileInfo = SP.File.OpenBinaryDirect(_parent.Context, url);
                        stream = fileInfo.Stream;
                    }

                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            char[] buffer = new char[1024];
                            using (var writer = new StreamWriter(path, false))
                            {
                                int length = 0;
                                do
                                {
                                    length = reader.ReadBlock(buffer, 0, buffer.Length);
                                    writer.Write(buffer, 0, length);
                                } while (length == buffer.Length);
                            }
                        }
                    }
                }
                finally
                {
                    if (stream != null)
                    {
                        stream.Close();
                    }
                }

            }
        }

    }
}
