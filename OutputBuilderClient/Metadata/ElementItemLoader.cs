// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ElementItemLoader.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The element item loader.
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
    ///     The element item loader.
    /// </summary>
    internal sealed class ElementItemLoader : IItemLoader
    {
        #region Constants and Fields

        /// <summary>
        ///     The path to item.
        /// </summary>
        private readonly string _pathToItem;

        /// <summary>
        ///     The property.
        /// </summary>
        private readonly string _property;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="ElementItemLoader" /> class.
        /// </summary>
        /// <param name="property">
        ///     The property.
        /// </param>
        /// <param name="pathToItem">
        ///     The path to item.
        /// </param>
        public ElementItemLoader(string property, string pathToItem)
        {
            Contract.Requires(!string.IsNullOrEmpty(property));
            Contract.Requires(!string.IsNullOrEmpty(pathToItem));

            _property = property;
            _pathToItem = pathToItem;
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
        public string Read(XmlDocument document, XmlNamespaceManager nameManager)
        {
            Contract.Requires(document != null);
            Contract.Requires(nameManager != null);

            var imageNode = document.SelectSingleNode(_pathToItem, nameManager) as XmlElement;
            if (imageNode == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(imageNode.Value))
            {
                return imageNode.Value;
            }

            if (!string.IsNullOrWhiteSpace(imageNode.InnerText))
            {
                return imageNode.InnerText.Trim();
            }

            return string.Empty;
        }

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
            Contract.Invariant(!string.IsNullOrEmpty(_pathToItem));
        }

        #endregion
    }
}