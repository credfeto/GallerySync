// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SupportedExtensionAttribute.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   Supported file extension.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

#endregion

namespace OutputBuilderClient
{
    /// <summary>
    ///     Supported file extension.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedExtensionAttribute : Attribute
    {
        #region Constants and Fields

        /// <summary>
        ///     The file extension.
        /// </summary>
        private readonly string _extension;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="SupportedExtensionAttribute" /> class.
        /// </summary>
        /// <param name="extension">
        ///     The extension.
        /// </param>
        public SupportedExtensionAttribute(string extension)
        {
            Contract.Requires(!string.IsNullOrEmpty(extension));

            _extension = extension;
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the extension.
        /// </summary>
        /// <value>
        ///     The extension.
        /// </value>
        public string Extension
        {
            get
            {
                Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));
                return _extension;
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
            Contract.Invariant(!string.IsNullOrEmpty(_extension));
        }
    }
}