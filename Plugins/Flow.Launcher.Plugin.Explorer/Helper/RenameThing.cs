using System;
using System.IO;

namespace Flow.Launcher.Plugin.Explorer.Helper
{
    public static class RenameThing
    {
        private static void _rename(this FileSystemInfo info, string newName, IPublicAPI api)
        {
            if (info is FileInfo)
            {
                if (!api.IsValidFileName(newName))
                {
                    throw new InvalidNameException();
                }
                FileInfo file = (FileInfo)info;
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
                if (!api.IsValidDirectoryName(newName))
                {
                    throw new InvalidNameException();
                }
                DirectoryInfo directory = (DirectoryInfo)info;
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
        /// <summary>
        /// Renames a file system elemnt (directory or file)
        /// </summary>
        /// <param name="NewFileName">The requested new name</param>
        /// <param name="oldInfo"> The <see cref="FileInfo"/> or <see cref="DirectoryInfo"/> representing the old file</param>
        /// <param name="api">An instance of <see cref="IPublicAPI"/>so this can create msgboxes</param>

        public static void Rename(string NewFileName, FileSystemInfo oldInfo, IPublicAPI api)
        {
            // if it's just whitespace and nothing else
            if (NewFileName.Trim() == "" || NewFileName == "")
            {
                api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_field_may_not_be_empty"), "New file name"));
                return;
            }

            try
            {
                oldInfo._rename(NewFileName, api);
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case FileNotFoundException:

                        api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_file_not_found"), oldInfo.FullName));
                        return;
                    case NotANewNameException:
                        api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_not_a_new_name"), NewFileName));
                        api.ShowMainWindow();
                        return;
                    case InvalidNameException:
                        api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_invalid_name"), NewFileName));
                        return;
                    case IOException iOException:
                        if (iOException.Message.Contains("incorrect"))
                        {
                            api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_invalid_name"), NewFileName));
                            return;
                        }
                        else
                        {
                            goto default;
                        }
                    default:
                        api.ShowMsgError(exception.ToString());
                        return;
                }
            }
            api.ShowMsg(string.Format(api.GetTranslation("plugin_explorer_successful_rename"), NewFileName));
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
    
    


