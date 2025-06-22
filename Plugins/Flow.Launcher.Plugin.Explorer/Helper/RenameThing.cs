using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Explorer.Helper
{
    public static class RenameThing
    {
        public static void Rename(this FileSystemInfo info, string newName)
        {
            if (info is FileInfo)
            {
                FileInfo file = new FileInfo(info.FullName);
                DirectoryInfo directory;
                
                directory = file.Directory ?? new DirectoryInfo(Path.GetPathRoot(file.FullName));
                if (Path.Join(info.FullName, directory.Name) == newName)
                {
                    throw new NotANewNameException("New name was the same as the old name");
                }
                File.Move(info.FullName, Path.Join(directory.FullName, newName));
                return;
            }
            else if (info is DirectoryInfo)
            {
                DirectoryInfo directory = new DirectoryInfo(info.FullName);
                DirectoryInfo parent;
                parent = directory.Parent ?? new DirectoryInfo(Path.GetPathRoot(directory.FullName));
                if (Path.Join(parent.FullName, directory.Name) == newName)
                {
                    throw new NotANewNameException("New name was the same as the old name");
                }
                Directory.Move(info.FullName, Path.Join(parent.FullName, newName));

            }
            else
            {
                throw new ArgumentException($"{nameof(info)} must be either, {nameof(FileInfo)} or {nameof(DirectoryInfo)}");
            }
        }
    }
    [Serializable]
    public class NotANewNameException : IOException
    {
        public NotANewNameException() { }
        public NotANewNameException(string message) : base(message) { }
        public NotANewNameException(string message, Exception inner) : base(message, inner) { }
        protected NotANewNameException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
