// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ItemLoaderBase.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The item loader base.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Xml;

namespace Credfeto.Gallery.OutputBuilder.Metadata
{
    /// <summary>
    ///     The item loader base.
    /// </summary>
    internal abstract class ItemLoaderBase : IItemLoader
    {
        /// <summary>
        ///     The property.
        /// </summary>
        private readonly string _property;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ItemLoaderBase" /> class.
        /// </summary>
        /// <param name="property">
        ///     The property.
        /// </param>
        protected ItemLoaderBase(string property)
        {
            Contract.Requires(!string.IsNullOrEmpty(property));

            this._property = property;
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
        ///     The value at the specified doc.
        /// </returns>
        public abstract string Read(XmlDocument document, XmlNamespaceManager nameManager);

        /// <summary>
        ///     The object invariant.
        /// </summary>
        [ContractInvariantMethod]
        [SuppressMessage(category: "Microsoft.Performance", checkId: "CA1811:AvoidUncalledPrivateCode", Justification = "Invoked by Code Contracts")]
        [SuppressMessage(category: "SubMain.CodeItRight.Rules.Performance", checkId: "PE00004:RemoveUnusedPrivateMethods", Justification = "Invoked by Code Contracts")]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(this._property));
        }
    }
}