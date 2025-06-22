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
                if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidNameException();
                }
                FileInfo file = new FileInfo(info.FullName);
                DirectoryInfo directory;

                directory = file.Directory ?? new DirectoryInfo(Path.GetPathRoot(file.FullName));
                if (info.FullName == Path.Join(directory.FullName, newName))
                {
                    throw new NotANewNameException("New name was the same as the old name");
                }
                File.Move(info.FullName, Path.Join(directory.FullName, newName));
                return;
            }
            else if (info is DirectoryInfo)
            {
                List<char> invalidChars = Path.GetInvalidPathChars().ToList();
                invalidChars.Add('/');
                invalidChars.Add('\\');
                if (newName.IndexOfAny(invalidChars.ToArray()) >= 0)
                {
                    throw new InvalidNameException();
                }
                DirectoryInfo directory = new DirectoryInfo(info.FullName);
                DirectoryInfo parent;
                parent = directory.Parent ?? new DirectoryInfo(Path.GetPathRoot(directory.FullName));

                if (info.FullName == Path.Join(parent.FullName, newName))
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

    internal class NotANewNameException : IOException
    {
        public NotANewNameException() { }
        public NotANewNameException(string message) : base(message) { }
        public NotANewNameException(string message, Exception inner) : base(message, inner) { }
        protected NotANewNameException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
    internal class FileAlreadyExistsException : IOException
    {
        public FileAlreadyExistsException() { }
        public FileAlreadyExistsException(string message) : base(message) { }
        public FileAlreadyExistsException(string message, Exception inner) : base(message, inner) { }
        protected FileAlreadyExistsException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    internal class InvalidNameException : Exception
    {
        public InvalidNameException() { }
        public InvalidNameException(string message) : base(message) { }
        public InvalidNameException(string message, Exception inner) : base(message, inner) { }
        protected InvalidNameException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
    
    
}

