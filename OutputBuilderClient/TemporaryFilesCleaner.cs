// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TemporaryFilesCleaner.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Removes temporary files of specific mask.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.IO;
using StorageHelpers;

namespace OutputBuilderClient
{
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

            _path = Path.GetTempPath();
            _fileMask = fileMask;
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        ~TemporaryFilesCleaner()
        {
            Dispose(false);
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
                return;

            var files = Directory.GetFiles(_path, _fileMask);
            foreach (
                var fullFileName in files.Select(file => Path.Combine(_path, file))
            )
                FileHelpers.DeleteFile(fullFileName);
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
            Contract.Invariant(!string.IsNullOrEmpty(_fileMask));
            Contract.Invariant(!string.IsNullOrEmpty(_path));
        }
    }
}