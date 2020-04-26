// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ElementItemListLoader.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The element item list loader.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using System.Xml;

namespace Credfeto.Gallery.OutputBuilder.Metadata
{
    /// <summary>
    ///     The element item list loader.
    /// </summary>
    internal sealed class ElementItemListLoader : IItemLoader
    {
        /// <summary>
        ///     The path to item.
        /// </summary>
        private readonly string _pathToItem;

        /// <summary>
        ///     The property.
        /// </summary>
        private readonly string _property;

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
            this._property = property;
            this._pathToItem = pathToItem;
        }

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

                return this._property;
            }
        }

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
        ///     The value at the specified document location.
        /// </returns>
        public string Read(XmlDocument document, XmlNamespaceManager nameManager)
        {
            Contract.Requires(document != null);
            Contract.Requires(nameManager != null);

            XmlNodeList imageNodes = document.SelectNodes(this._pathToItem, nameManager);

            if (imageNodes == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            foreach (XmlElement imageNode in imageNodes)
            {
                if (!string.IsNullOrWhiteSpace(imageNode.Value))
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(value: ";");
                    }

                    sb.Append(imageNode.Value.Trim());

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(imageNode.InnerText))
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(value: ";");
                    }

                    sb.Append(imageNode.InnerText.Trim());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [ContractInvariantMethod]
        [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1811:AvoidUncalledPrivateCode", Justification = "Invoked by Code Contracts")]
        [SuppressMessage(category: "SubMain.CodeItRight.Rules.Performance", checkId: "PE00004:RemoveUnusedPrivateMethods", Justification = "Invoked by Code Contracts")]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(this._property));
            Contract.Invariant(!string.IsNullOrEmpty(this._pathToItem));
        }
    }
}