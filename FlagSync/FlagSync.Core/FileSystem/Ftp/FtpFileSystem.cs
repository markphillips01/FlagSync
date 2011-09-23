﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using FlagFtp;
using FlagLib.IO;
using FlagSync.Core.FileSystem.Abstract;

namespace FlagSync.Core.FileSystem.Ftp
{
    internal class FtpFileSystem : IFileSystem
    {
        private FtpClient client;

        /// <summary>
        /// Occurs when the file copy progress has changed.
        /// </summary>
        public event EventHandler<CopyProgressEventArgs> FileCopyProgressChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpFileSystem"/> class.
        /// </summary>
        /// <param name="serverAddress">The server address.</param>
        /// <param name="credentials">The credentials.</param>
        public FtpFileSystem(Uri serverAddress, NetworkCredential credentials)
        {
            if (serverAddress == null)
                throw new ArgumentNullException("serverAddress");

            if (credentials == null)
                throw new ArgumentNullException("credentials");

            this.client = new FtpClient(credentials);
            this.client.Credentials = credentials;
        }

        /// <summary>
        /// Tries to delete a file.
        /// </summary>
        /// <param name="file">The file to delete.</param>
        /// <returns>
        /// A value indicating whether the file deletion has succeed
        /// </returns>
        public bool TryDeleteFile(IFileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException("file");

            if (!(file is FtpFileInfo))
                throw new ArgumentException("The file must be of type FtpFileInfo.", "file");

            try
            {
                this.client.DeleteFile(new Uri(file.FullName));
            }

            catch (WebException ex)
            {
                Debug.WriteLine(ex.Message);
                Logger.Current.LogError(
                    string.Format("WebException while deleting file: {0}", file.FullName));

                return false;
            }

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

            Uri newDirectory = (new Uri(new Uri(targetDirectory.FullName + "//"), sourceDirectory.Name));

            try
            {
                this.client.CreateDirectory(newDirectory);
            }

            catch (WebException ex)
            {
                Debug.WriteLine(ex.Message);
                Logger.Current.LogError(
                    string.Format("WebException creating directory: {0} in directory: {1}",
                    sourceDirectory.FullName, targetDirectory.FullName));

                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to delete a directory.
        /// </summary>
        /// <param name="directory">The directory to delete.</param>
        /// <returns>
        /// A value indicating whether the deletion has succeed.
        /// </returns>
        public bool TryDeleteDirectory(IDirectoryInfo directory)
        {
            if (directory == null)
                throw new ArgumentNullException("null");

            if (!(directory is FtpDirectoryInfo))
                throw new ArgumentException("The directory must be of type FtpDirectoryInfo.", "directory");
            try
            {
                this.client.DeleteDirectory(new Uri(directory.FullName + "/"));
            }

            catch (WebException ex)
            {
                Debug.WriteLine(ex.Message);
                Logger.Current.LogError(
                    string.Format("WebException while deleting directory: {0}", directory.FullName));

                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to copy a file to specified directory.
        /// </summary>
        /// <param name="sourceFileSystem">The source file system.</param>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="targetDirectory">The target directory.</param>
        /// <returns>
        /// True, if the copy operation has succeed; otherwise, false
        /// </returns>
        public bool TryCopyFile(IFileSystem sourceFileSystem, IFileInfo sourceFile, IDirectoryInfo targetDirectory)
        {
            if (sourceFileSystem == null)
                throw new ArgumentNullException("sourceFileSystem");

            if (sourceFile == null)
                throw new ArgumentNullException("sourceFile");

            if (targetDirectory == null)
                throw new ArgumentNullException("targetDirectory");

            if (!(targetDirectory is FtpDirectoryInfo))
                throw new ArgumentException("The target directory must be of type FtpDirectoryInfo.", "targetDirectory");

            bool succeed = false;

            var uri = new Uri(targetDirectory.FullName);

            Uri targetFilePath = new Uri(this.CombinePath(targetDirectory.FullName, sourceFile.Name));

            try
            {
                bool canceled = false;

                using (Stream sourceStream = sourceFileSystem.OpenFileStream(sourceFile))
                {
                    using (Stream targetStream = this.client.OpenWrite(targetFilePath))
                    {
                        StreamCopyOperation copyOperation = new StreamCopyOperation(sourceStream, targetStream, 8 * 1024, true);

                        copyOperation.CopyProgressChanged += (sender, e) =>
                        {
                            if (this.FileCopyProgressChanged != null)
                            {
                                this.FileCopyProgressChanged(this, e);

                                canceled = e.Cancel;
                            }
                        };

                        copyOperation.Execute();
                    }
                }

                succeed = !canceled;
            }

            catch (WebException ex)
            {
                Debug.WriteLine(ex.Message);
                Logger.Current.LogError(
                    string.Format("WebException while copying file: {0} to directory: {1}",
                    sourceFile.FullName, targetDirectory.FullName));
            }

            return succeed;
        }

        /// <summary>
        /// Gets the file info at the specified path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns></returns>
        public IFileInfo GetFileInfo(string path)
        {
            if (path == null)
                throw new ArgumentException("path");

            path = FtpFileSystem.NormalizePath(path);
            FlagFtp.FtpFileInfo file = this.client.GetFileInfo(new Uri(path));

            return new FtpFileInfo(file.FullName, file.LastWriteTime, file.Length, this.client);
        }

        /// <summary>
        /// Gets the directory info at the specified path.
        /// </summary>
        /// <param name="path">The path of the directory.</param>
        /// <returns></returns>
        public IDirectoryInfo GetDirectoryInfo(string path)
        {
            if (path == null)
                throw new ArgumentException("path");

            path = FtpFileSystem.NormalizePath(path);
            FlagFtp.FtpDirectoryInfo directory = this.client.GetDirectoryInfo(new Uri(path));

            return new FtpDirectoryInfo(directory.FullName.Replace("%20", " "), this.client);
        }

        /// <summary>
        /// Checks if the file at the specified path exists.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        /// True, if the file exists; otherwise, false
        /// </returns>
        public bool FileExists(string path)
        {
            if (path == null)
                throw new ArgumentException("path");

            path = FtpFileSystem.NormalizePath(path);
            return this.client.FileExists(new Uri(path));
        }

        /// <summary>
        /// Checks if the directory at the specified path exists.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        /// True, if the directory exists; otherwise, false
        /// </returns>
        public bool DirectoryExists(string path)
        {
            if (path == null)
                throw new ArgumentException("path");

            path = FtpFileSystem.NormalizePath(path);
            return this.client.DirectoryExists(new Uri(path));
        }

        /// <summary>
        /// Opens the stream of the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public Stream OpenFileStream(IFileInfo file)
        {
            if (file == null)
                throw new ArgumentException("file");

            return this.client.OpenRead(new Uri(file.FullName));
        }

        public string CombinePath(string path1, string path2)
        {
            return path1 + "/" + path2;
        }

        /// <summary>
        /// Normalizes the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        private static string NormalizePath(string path)
        {
            if (path == null)
                throw new ArgumentException("path");

            return path.Replace("//", "/").Replace("/", "//").Replace("\\", "//").Replace("%20", " ");
        }
    }
}