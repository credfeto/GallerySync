// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ElementItemListLoader.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The element item list loader.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using System.Xml;

#endregion

namespace OutputBuilderClient.Metadata
{
    /// <summary>
    ///     The element item list loader.
    /// </summary>
    internal sealed class ElementItemListLoader : IItemLoader
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
        ///     Initializes a new instance of the <see cref="ElementItemListLoader" /> class.
        /// </summary>
        /// <param name="property">
        ///     The property.
        /// </param>
        /// <param name="pathToItem">
        ///     The path To Item.
        /// </param>
        public ElementItemListLoader(string property, string pathToItem)
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
        /// <param name="nameSpaceManager">
        ///     The name space manager.
        /// </param>
        /// <returns>
        ///     The value at the specified document location.
        /// </returns>
        public string Read(XmlDocument document, XmlNamespaceManager nameSpaceManager)
        {
            Contract.Requires(document != null);
            Contract.Requires(nameSpaceManager != null);

            XmlNodeList imageNodes = document.SelectNodes(_pathToItem, nameSpaceManager);
            if (imageNodes == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (XmlElement imageNode in imageNodes)
            {
                if (!string.IsNullOrWhiteSpace(imageNode.Value))
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(";");
                    }

                    sb.Append(imageNode.Value.Trim());
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(imageNode.InnerText))
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(";");
                    }

                    sb.Append(imageNode.InnerText.Trim());
                    continue;
                }
            }

            return sb.ToString();
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