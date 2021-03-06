using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SP = Microsoft.SharePoint.Client;

namespace SpLib
{
    public class SharePointFolder : ISharepointList
    {
        static readonly byte[] buffer = new byte[1024 * 16];
        public static readonly string TagFileName = "FileLeafRef";
        public static readonly string TagUrl = "FileRef";
        public static readonly string TagComment = "_CheckinComment";

        readonly SP.ClientContext _context;
        readonly SP.Folder _folder;
        internal SharePointFolder(SP.ClientContext context, SP.Folder folder)
        {
            _context = context;
            _folder = folder;
        }
        public ClientContext Context => _context;

        public string Url => _folder.ServerRelativeUrl;

        public string[] FileNames
        {
            get
            {
                List<string> files = new List<string>();
                _context.Load(_folder.Files);
                _context.ExecuteQuery();
                files.AddRange(_folder.Files.Select<SP.File, string>((x) => x.Name));
                return files.ToArray();
            }
        }

        public string[] FolderNames
        {
            get
            {
                List<string> subDirs = new List<string>();
                _context.Load(_folder.Folders);
                _context.ExecuteQuery();
                subDirs.AddRange(_folder.Folders.Select<SP.Folder, string>((x) => x.Name));
                return subDirs.ToArray();
            }
        }

        public SharePointFolder[] Folders
        {
            get
            {
                List<SharePointFolder> spList = new List<SharePointFolder>();
                _context.Load(_folder.Folders);
                _context.ExecuteQuery();
                foreach (var f in _folder.Folders)
                {
                    spList.Add(new SharePointFolder(_context, f));
                }
                return spList.ToArray();
            }
        }

        public void FindFileOrFolder(string regex, bool recursiv, Action<SP.Folder> folderAction, Action<SP.File> fileAction)
        {
            _context.Load(_folder.Folders);
            _context.Load(_folder.Files);
            _context.ExecuteQuery();
            var re = new System.Text.RegularExpressions.Regex(regex);
            foreach(var file in _folder.Files)
            {
                if (re.IsMatch(file.Name))
                {
                    _context.Load(file);
                    _context.ExecuteQuery();
                    fileAction(file);
                }
            }
            foreach (var folder in _folder.Folders)
            {
                if( re.IsMatch(folder.Name))
                {
                    _context.Load(folder);
                    _context.ExecuteQuery();
                    folderAction(folder);
                }
                if (recursiv)
                {
                    var spFolder = new SharePointFolder(_context, folder);
                    spFolder.FindFileOrFolder(regex, recursiv, folderAction, fileAction);
                }
            }
        }

        public SharePointFolder GetOrCreateSubfolder(string name)
        {
            _context.Load(_folder.Folders);
            _context.ExecuteQuery();
            foreach( var f in _folder.Folders)
            {
                if( f.Name == name)
                {
                    return new SharePointFolder(_context, f);
                }
            }
            var newFolder = _folder.Folders.Add(name);
            _context.Load(newFolder);
            _context.ExecuteQuery();
            return new SharePointFolder(_context, newFolder);
        }

        public void UploadDocument(string path)
        {

            var fileName = System.IO.Path.GetFileName(path);
            

            SP.File spFile = null;
            _context.Load(_folder.Files);
            _context.ExecuteQuery();
            foreach (var f in _folder.Files)
            {
                if (f.Name == fileName)
                {
                    spFile = f;
                }
            }
            
            if( spFile == null )
            {
                var creationInformation = new SP.FileCreationInformation()
                {
                    Url = fileName,
                    Content = new byte[] { 0 }
                };
                spFile = _folder.Files.Add(creationInformation);
            }
            _context.Load(spFile);
            _context.ExecuteQuery();

            
            using (var stream = new System.IO.FileStream(path, FileMode.Open))
            {
                
                var saveParams = new SP.FileSaveBinaryInformation()
                {
                    ContentStream = stream
                };
                spFile.SaveBinary(saveParams);
                _context.Load(spFile);
                _context.ExecuteQuery();
            }

            
        }

        public void DownloadAllFiles(string targetDir)
        {
            _context.Load(_folder);
            _context.ExecuteQuery();
            _context.Load(_folder.Files);
            _context.ExecuteQuery();
            foreach (var f in _folder.Files)
            {
                DownloadDocument(targetDir, f);
            }
        }

        public void DownloadDocument(string targetDir, string fileName)
        {
            _context.Load(_folder.Files);
            _context.ExecuteQuery();
            foreach (var f in _folder.Files)
            {
                if (f.Name == fileName)
                {
                    DownloadDocument(targetDir, f);
                    break;
                }
            }
        }

        public void DownloadDocument(string targetDir, SP.File f)
        {
            //_context.Load(f);
            //_context.ExecuteQuery();
            var result = f.OpenBinaryStream();
            _context.Load(f);
            _context.ExecuteQuery();

            var offset = 0;
            List<string> pathComponents = new List<string>();
            pathComponents.AddRange(targetDir.Trim('"','\'').Split('\\'));
            pathComponents.AddRange(f.ServerRelativeUrl.Split('/').Skip(2)); // sites must not be in the path, it's the second element!
            var fileName = string.Join("\\", pathComponents.ToArray());
            SharepointDocLibrary.AssertValidPath(System.IO.Path.GetDirectoryName(fileName));
            using (var stream = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                var nrOfBytes = result.Value.Read(buffer, offset, buffer.Length);

                while (nrOfBytes > 0)
                {
                    stream.Write(buffer, 0, nrOfBytes);
                    nrOfBytes = result.Value.Read(buffer, offset, buffer.Length);
                }
            }
        }
    }
}
