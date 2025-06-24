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
                if (!SharedCommands.FilesFolders.IsValidFileName(newName))
                {
                    throw new InvalidNameException();
                }
                FileInfo file = (FileInfo)info;
                DirectoryInfo directory;

                directory = file.Directory ?? new DirectoryInfo(Path.GetPathRoot(file.FullName));
                string newPath = Path.Join(directory.FullName, newName);
                if (info.FullName == newPath)
                {
                    throw new NotANewNameException("New name was the same as the old name");
                }
                if (File.Exists(newPath)) throw new ElementAlreadyExistsException();
                File.Move(info.FullName, newPath);
                return;
            }
            else if (info is DirectoryInfo)
            {
                if (!SharedCommands.FilesFolders.IsValidDirectoryName(newName))
                {
                    throw new InvalidNameException();
                }
                DirectoryInfo directory = (DirectoryInfo)info;
                DirectoryInfo parent;
                parent = directory.Parent ?? new DirectoryInfo(Path.GetPathRoot(directory.FullName));
                string newPath = Path.Join(parent.FullName, newName);
                if (info.FullName == newPath)
                {
                    throw new NotANewNameException("New name was the same as the old name");
                }
                if (Directory.Exists(newPath)) throw new ElementAlreadyExistsException();
                
                Directory.Move(info.FullName, newPath);

            }
            else
            {
                throw new ArgumentException($"{nameof(info)} must be either, {nameof(FileInfo)} or {nameof(DirectoryInfo)}");
            }
            
        }
        /// <summary>
        /// Renames a file system element (directory or file)
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
                        return;
                    case InvalidNameException:
                        api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_invalid_name"), NewFileName));
                        return;
                    case ElementAlreadyExistsException:
                        api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_element_already_exists"), NewFileName));
                        break;
                    default:
                        string msg = exception.Message;
                        if (!string.IsNullOrEmpty(msg))
                        {
                            api.ShowMsgError(string.Format(api.GetTranslation("plugin_explorer_exception"), exception.Message));
                            return;
                        }
                        else
                        {
                            api.ShowMsgError(api.GetTranslation("plugin_explorer_no_reason_given_exception"));
                        }
                        
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
        internal class ElementAlreadyExistsException : IOException {
        public ElementAlreadyExistsException() { }
        public ElementAlreadyExistsException(string message) : base(message) { }
        public ElementAlreadyExistsException(string message, Exception inner) : base(message, inner) { }
        protected ElementAlreadyExistsException(
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
    
    


