﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlagSync.Core.FileSystem.Abstract;

namespace FlagSync.Core.Test.VirtualFileSystem
{
    class VirtualFileSystem : IFileSystem
    {
        private List<IFileSystemInfo> fileSystemInfos;

        /// <summary>
        /// Occurs when the file copy progress has changed.
        /// </summary>
        public event EventHandler<FlagLib.FileSystem.CopyProgressEventArgs> FileCopyProgressChanged;

        /// <summary>
        /// Tries to delete a file.
        /// </summary>
        /// <param name="file">The file to delete.</param>
        /// <returns>
        /// A value indicating whether the file deletion has succeed
        /// </returns>
        public bool TryDeleteFile(IFileSystemInfo file)
        {
            if (file == null)
                throw new ArgumentNullException("file");

            VirtualDirectoryInfo parent =
                (VirtualDirectoryInfo)this.GetDirectoryInfo(Path.GetDirectoryName(file.FullName));

            if (!parent.IsLocked)
            {
                this.fileSystemInfos.Remove(file);
            }

            else { throw new UnauthorizedAccessException("The parent directory is locked!"); }

            return true;
        }

        /// <summary>
        /// Tries to create a directory in the specified directory (low level operation).
        /// </summary>
        /// <param name="sourceDirectory">The source directory.</param>
        /// <param name="targetDirectory">The target directory.</param>
        /// <returns>
        /// Returns a value indicating whether the directory creation has succeed
        /// </returns>
        public bool TryCreateDirectory(IDirectoryInfo sourceDirectory, IDirectoryInfo targetDirectory)
        {
            if (sourceDirectory == null)
                throw new ArgumentNullException("sourceDirectory");

            if (targetDirectory == null)
                throw new ArgumentNullException("targetDirectory");

            string newDirectoryPath = Path.Combine(targetDirectory.FullName, sourceDirectory.Name);

            if (!this.DirectoryExists(newDirectoryPath))
            {
                VirtualDirectoryInfo parent = (VirtualDirectoryInfo)this.GetDirectoryInfo(targetDirectory.FullName);

                if (!parent.IsLocked)
                {
                    this.fileSystemInfos.Add(new VirtualDirectoryInfo(
                        sourceDirectory.Name, parent, false, true));
                }

                else { throw new UnauthorizedAccessException("The parent directory is locked!"); }
            }

            return true;
        }

        /// <summary>
        /// Tries to delete a directory (low level operation).
        /// </summary>
        /// <param name="directory">The directory to delete.</param>
        /// <returns>
        /// A value indicating whether the deletion has succeed.
        /// </returns>
        public bool TryDeleteDirectory(IDirectoryInfo directory)
        {
            if (directory == null)
                throw new ArgumentNullException("directory");

            if (!((VirtualDirectoryInfo)directory.Parent).IsLocked)
            {
                this.fileSystemInfos.Remove(directory);
            }

            else { throw new UnauthorizedAccessException("The parent directory is locked!"); }

            return true;
        }

        /// <summary>
        /// Tries to copy a file to specified directory (low level operation).
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetDirectory">The target directory.</param>
        /// <returns></returns>
        public bool TryCopyFile(IFileInfo sourceFile, IDirectoryInfo targetDirectory)
        {
            if (sourceFile == null)
                throw new ArgumentNullException("sourceFile");

            if (targetDirectory == null)
                throw new ArgumentNullException("targetDirectory");

            VirtualDirectoryInfo tarDir = (VirtualDirectoryInfo)targetDirectory;

            if (!tarDir.IsLocked)
            {
                string newFilePath = Path.Combine(targetDirectory.FullName, sourceFile.Name);

                this.fileSystemInfos.Add(new VirtualFileInfo(newFilePath, sourceFile.Length, DateTime.Now, tarDir));
            }

            else { throw new UnauthorizedAccessException("The parent directory is locked!"); }

            return true;
        }

        /// <summary>
        /// Creates a new file info with specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public IFileInfo GetFileInfo(string path)
        {
            path = Path.GetFullPath(path);

            IFileSystemInfo file =
                this.fileSystemInfos.FirstOrDefault(f => f.FullName == path);

            if (file != null)
            {
                return (IFileInfo)file;
            }

            else
            {
                return new VirtualFileInfo(path, 0, DateTime.MinValue,
                    (VirtualDirectoryInfo)this.GetDirectoryInfo(Path.GetDirectoryName(path)));
            }
        }

        /// <summary>
        /// Creates a new directory info with specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public IDirectoryInfo GetDirectoryInfo(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            path = Path.GetFullPath(path);

            IFileSystemInfo directory =
                this.fileSystemInfos.FirstOrDefault(dir => dir.FullName == path);

            if (directory != null)
            {
                return (IDirectoryInfo)directory;
            }

            else
            {
                return new VirtualDirectoryInfo(Path.GetFileName(path),
                    (VirtualDirectoryInfo)this.GetDirectoryInfo(Path.GetDirectoryName(path)),
                    false, false);
            }
        }

        /// <summary>
        /// Checks if the file at the specified path exists.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public bool FileExists(string path)
        {
            path = Path.GetFullPath(path);

            return this.fileSystemInfos.Any(file => file.FullName == path);
        }

        /// <summary>
        /// Checks if the directory at the specified path exists.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public bool DirectoryExists(string path)
        {
            path = Path.GetFullPath(path);

            return this.fileSystemInfos.Any(directory => directory.FullName == path);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFileSystem"/> class.
        /// </summary>
        public VirtualFileSystem()
        {
            this.fileSystemInfos = new List<IFileSystemInfo>();
        }
    }
}