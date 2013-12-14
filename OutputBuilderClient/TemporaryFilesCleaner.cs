// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TemporaryFilesCleaner.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Removes temporary files of specific mask.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

#endregion

namespace OutputBuilderClient
{
    /// <summary>
    ///     Removes temporary files of specific mask.
    /// </summary>
    internal sealed class TemporaryFilesCleaner : IDisposable
    {
        #region Constants and Fields

        /// <summary>
        ///     The mask to search for the files with.
        /// </summary>
         private readonly string _fileMask;

        /// <summary>
        ///     The path to the temporary files.
        /// </summary>
         private readonly string _path;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        /// <param name="fileMask">
        ///     The file mask.
        /// </param>
        public TemporaryFilesCleaner( string fileMask)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileMask));

            _path = Path.GetTempPath();
            _fileMask = fileMask;
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        ~TemporaryFilesCleaner()
        {
            Dispose(false);
        }

        #endregion

        #region Implemented Interfaces

        #region IDisposable

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        ///     Silently removes the file, ignoring any failures.
        /// </summary>
        /// <param name="fullFileName">
        ///     Full name of the file.
        /// </param>
        private static void SilentlyRemoveFile( string fullFileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fullFileName));

            try
            {
                File.Delete(fullFileName);
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

            string[] files = Directory.GetFiles(_path, _fileMask);
            foreach (string fullFileName in files.Select(file => Path.Combine(_path, file)))
            {
                SilentlyRemoveFile(fullFileName);
            }
        }

        #endregion

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
            Contract.Invariant(!string.IsNullOrEmpty(_fileMask));
            Contract.Invariant(!string.IsNullOrEmpty(_path));
        }
    }
}