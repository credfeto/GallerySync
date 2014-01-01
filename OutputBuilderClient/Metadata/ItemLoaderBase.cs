// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ItemLoaderBase.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The item loader base.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Xml;

#endregion

namespace OutputBuilderClient.Metadata
{
    /// <summary>
    ///     The item loader base.
    /// </summary>
    internal abstract class ItemLoaderBase : IItemLoader
    {
        #region Constants and Fields

        /// <summary>
        ///     The property.
        /// </summary>
        private readonly string _property;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="ItemLoaderBase" /> class.
        /// </summary>
        /// <param name="property">
        ///     The property.
        /// </param>
        protected ItemLoaderBase(string property)
        {
            Contract.Requires(!string.IsNullOrEmpty(property));

            _property = property;
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the Name.
        /// </summary>
        /// <value>
        ///     The name of the property.
        /// </value>
        public string Name
        {
            get
            {
                Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));

                return _property;
            }
        }

        #endregion

        #region Implemented Interfaces

        #region IItemLoader

        /// <summary>
        ///     Reads the value out of the specified document.
        /// </summary>
        /// <param name="document">
        ///     The XML document.
        /// </param>
        /// <param name="nameManager">
        ///     The name space manager.
        /// </param>
        /// <returns>
        ///     The value at the specified doc.
        /// </returns>
        public abstract string Read(XmlDocument document, XmlNamespaceManager nameManager);

        #endregion

        #endregion

        #region Methods

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [ContractInvariantMethod]
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Invoked by Code Contracts")]
        [SuppressMessage("SubMain.CodeItRight.Rules.Performance", "PE00004:RemoveUnusedPrivateMethods",
            Justification = "Invoked by Code Contracts")]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(_property));
        }

        #endregion
    }
}