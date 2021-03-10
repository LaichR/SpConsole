using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using SP = Microsoft.SharePoint.Client;

namespace SpLib
{
    public class SharePointFolder : ISharepointList
    {
        
        public static readonly string TagFileName = "FileLeafRef";
        public static readonly string TagUrl = "FileRef";
        public static readonly string TagComment = "_CheckinComment";

        readonly SP.ClientContext _context;
        readonly SP.Folder _folder;
        readonly SharepointDocLibrary _lib;
        internal SharePointFolder(SharepointDocLibrary lib, SP.Folder folder)
        {
            _context = lib.Context;
            _folder = folder;
            _lib = lib;
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
                    spList.Add(new SharePointFolder(_lib, f));
                }
                return spList.ToArray();
            }
        }

        public void FindFileOrFolder(StringHelper.FolderSnippet[] folderSnippets, int index, bool recursiv, Action<SP.Folder> folderAction, Action<SP.File> fileAction)
        {
            SP.Folder folder = _folder;

            if( index >= folderSnippets.Length - 1)
            {
                index = folderSnippets.Length - 1; 
                _context.Load(folder.Files); _context.ExecuteQuery();
                var re = new Regex(folderSnippets[index].FolderPart);
                foreach( var f in folder.Files.Where(x=> re.IsMatch(x.Name)))
                {
                    fileAction(f);
                }
                _context.Load(folder.Folders); _context.ExecuteQuery();
                    
                foreach (var f in folder.Folders.Where(x => re.IsMatch(x.Name)))
                {
                    folderAction(f);

                }
                if( recursiv)
                { 
                    foreach( var f in folder.Folders)
                    {
                        var spFolder = new SharePointFolder(_lib, f);
                        spFolder.FindFileOrFolder(folderSnippets, index + 1, recursiv, folderAction, fileAction);
                    }
                }
                return;
            }
            

            if( folderSnippets[index].UsesRegex)
            {
                var re = new Regex(folderSnippets[index].FolderPart);
                _context.Load(folder.Folders);
                _context.ExecuteQuery();
                foreach( var f in folder.Folders)
                {
                    if (re.IsMatch(f.Name))
                    {
                        folder = f;
                        var spFolder = new SharePointFolder(_lib, f);
                        spFolder.FindFileOrFolder(folderSnippets, index + 1, recursiv, folderAction, fileAction);
                    }
                }
            }
            else 
            {
                var serverRelativeUrl = folder.ServerRelativeUrl+ "/" + folderSnippets[index].FolderPart;
                folder = _context.Web.GetFolderByServerRelativeUrl(serverRelativeUrl);
                try
                {
                    _context.Load(folder);
                    _context.ExecuteQuery();
                    var spFolder = new SharePointFolder(_lib, folder);
                    spFolder.FindFileOrFolder(folderSnippets, index + 1, recursiv, folderAction, fileAction);
                }
                catch (Exception e)
                {
                    _lib.NotifyException(new DirectoryNotFoundException($"The folder <{serverRelativeUrl}> was not found!", e));
                }
            }                        
        }

        public SharePointFolder GetOrCreateSubfolder(string name)
        {
            //_folder.
            var matchingFolders = _context.LoadQuery(_folder.Folders.Where(x=>x.Name==name));
            _context.ExecuteQuery();
            //foreach( var f in _folder.Folders)
            foreach( var f in matchingFolders )
            {
                if( f.Name == name)
                {
                    return new SharePointFolder(_lib, f);
                }
            }
            _context.Load(_folder.Folders);
            var newFolder = _folder.Folders.Add(name);
            _context.Load(newFolder);
            _context.ExecuteQuery();
            return new SharePointFolder(_lib, newFolder);
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

        
    }
}
