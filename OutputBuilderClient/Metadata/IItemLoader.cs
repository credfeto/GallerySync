// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IItemLoader.cs" company="Twaddle Software">
//   Copyright (c) Twaddle Software
// </copyright>
// <summary>
//   The item loader.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives

using System.Diagnostics.CodeAnalysis;
using System.Xml;

#endregion

namespace OutputBuilderClient.Metadata
{
    /// <summary>
    ///     The item loader.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes",
        MessageId = "System.Xml.XmlDocument", Justification = "Better API")]
    internal interface IItemLoader
    {
        #region Properties

        /// <summary>
        ///     Gets the name.
        /// </summary>
        /// <value>
        ///     The name of the property.
        /// </value>
        string Name { get; }

        #endregion

        #region Public Methods

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
        string Read(XmlDocument document, XmlNamespaceManager nameManager);

        #endregion
    }
}