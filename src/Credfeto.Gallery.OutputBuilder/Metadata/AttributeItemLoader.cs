// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AttributeItemLoader.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The attribute item loader.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Xml;

namespace Credfeto.Gallery.OutputBuilder.Metadata
{
    /// <summary>
    ///     The attribute item loader.
    /// </summary>
    internal sealed class AttributeItemLoader : ItemLoaderBase
    {
        /// <summary>
        ///     The path to item.
        /// </summary>
        private readonly string _pathToItem;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AttributeItemLoader" /> class.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="pathToItem">The path to item.</param>
        public AttributeItemLoader(string property, string pathToItem)
            : base(property)
        {
            Contract.Requires(!string.IsNullOrEmpty(property));
            Contract.Requires(!string.IsNullOrEmpty(pathToItem));

            this._pathToItem = pathToItem;
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
        ///     The value at the specified doc.
        /// </returns>
        public override string Read(XmlDocument document, XmlNamespaceManager nameManager)
        {
            Contract.Requires(document != null);
            Contract.Requires(nameManager != null);

            XmlAttribute imageNode = document.SelectSingleNode(this._pathToItem, nameManager) as XmlAttribute;

            if (imageNode == null)
            {
                return string.Empty;
            }

            return imageNode.Value;
        }

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [ContractInvariantMethod]
        [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1811:AvoidUncalledPrivateCode", Justification = "Invoked by Code Contracts")]
        [SuppressMessage(category: "SubMain.CodeItRight.Rules.Performance", checkId: "PE00004:RemoveUnusedPrivateMethods", Justification = "Invoked by Code Contracts")]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(this._pathToItem));
        }
    }
}