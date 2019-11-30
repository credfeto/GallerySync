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
using System.IO;
using System.Linq;
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

            this._path = Path.GetTempPath();
            this._fileMask = fileMask;
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="TemporaryFilesCleaner" /> class.
        /// </summary>
        ~TemporaryFilesCleaner()
        {
            this.Dispose(disposing: false);
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

            foreach (string fullFileName in files.Select(selector: file => Path.Combine(this._path, file)))
            {
                FileHelpers.DeleteFile(fullFileName);
            }
        }

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1811:AvoidUncalledPrivateCode", Justification = "Required for Code Contracts")]
        [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1822:MarkMembersAsStatic", Justification = "Required for Code Contracts")]
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(this._fileMask));
            Contract.Invariant(!string.IsNullOrEmpty(this._path));
        }
    }
}