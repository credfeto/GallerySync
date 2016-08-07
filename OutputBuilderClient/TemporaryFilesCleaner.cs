// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TemporaryFilesCleaner.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Removes temporary files of specific mask.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace OutputBuilderClient
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Removes temporary files of specific mask.
    /// </summary>
    internal sealed class TemporaryFilesCleaner : IDisposable
    {
        /// <summary>
        ///     The mask to search for the files with.
        /// </summary>
        private readonly string _fileMask;

        /// <summary>
        ///     The path to the temporary files.
        /// </summary>
        private readonly string _path;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        /// <param name="fileMask">
        ///     The file mask.
        /// </param>
        public TemporaryFilesCleaner(string fileMask)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileMask));

            this._path = Alphaleonis.Win32.Filesystem.Path.GetTempPath();
            this._fileMask = fileMask;
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        ~TemporaryFilesCleaner()
        {
            this.Dispose(false);
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Silently removes the file, ignoring any failures.
        /// </summary>
        /// <param name="fullFileName">
        ///     Full name of the Alphaleonis.Win32.Filesystem.File.
        /// </param>
        private static void SilentlyRemoveFile(string fullFileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fullFileName));

            try
            {
                Alphaleonis.Win32.Filesystem.File.Delete(fullFileName);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            string[] files = Directory.GetFiles(this._path, this._fileMask);
            foreach (
                string fullFileName in files.Select(file => Alphaleonis.Win32.Filesystem.Path.Combine(this._path, file))
                )
            {
                SilentlyRemoveFile(fullFileName);
            }
        }

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Required for Code Contracts")]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
            Justification = "Required for Code Contracts")]
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(this._fileMask));
            Contract.Invariant(!string.IsNullOrEmpty(this._path));
        }
    }
}