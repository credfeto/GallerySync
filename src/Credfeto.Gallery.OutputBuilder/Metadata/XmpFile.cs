using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Xml;
using FileNaming;

namespace OutputBuilderClient.Metadata
{
    /// <summary>
    ///     The XMP File.
    /// </summary>
    public static class XmpFile
    {
        /// <summary>
        ///     Extracts the properties from the file.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <returns>
        ///     The extracted properties.
        /// </returns>
        public static Dictionary<string, string> ExtractProperties(string fileName)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));

            Contract.Ensures(Contract.Result<Dictionary<string, string>>() != null);

            Dictionary<string, string> props = new Dictionary<string, string>();

            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);

            XmlNamespaceManager nsmgr = CreateNamespaceManager(doc);

            IEnumerable<IItemLoader> loaders = CreateLoaders();

            foreach (IItemLoader loader in loaders)
            {
                if (loader == null)
                {
                    continue;
                }

                string value = loader.Read(doc, nsmgr);
                StoreValue(loader, props, value);
            }

            return props;
        }

        /// <summary>
        ///     Sets the property.
        /// </summary>
        /// <param name="fileName">
        ///     The filename.
        /// </param>
        /// <param name="propertyName">
        ///     Name of the property.
        /// </param>
        /// <param name="value">
        ///     The value.
        /// </param>
        /// <returns>
        ///     True, if the property was written; false, otherwise.
        /// </returns>
        public static bool SetProperty(string fileName, string propertyName, string value)
        {
            Contract.Requires(!string.IsNullOrEmpty(fileName));
            Contract.Requires(!string.IsNullOrEmpty(propertyName));
            Contract.Requires(!string.IsNullOrEmpty(value));

            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);

            XmlNamespaceManager nsmgr = CreateNamespaceManager(doc);

            if (StringComparer.InvariantCultureIgnoreCase.Equals(propertyName, MetadataNames.Keywords))
            {
                ElementItemListLoader keywordLoader = new ElementItemListLoader(MetadataNames.Keywords, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/dc:subject/rdf:Bag/rdf:li");

                string existingRawKeywords = keywordLoader.Read(doc, nsmgr);
                IEnumerable<string> existingKeywords = from record in existingRawKeywords.Split(separator: ';') select record.ToUpperInvariant();
                string[] newKeywords = value.Replace(oldChar: ';', newChar: ',')
                                            .Split(separator: ',');

                IEnumerable<string> keywordsToAdd = from record in newKeywords where !existingKeywords.Contains(record.ToUpperInvariant()) select record;

                XmlNode baseNode = doc.SelectSingleNode(xpath: "/x:xmpmeta/rdf:RDF/rdf:Description", nsmgr);

                if (baseNode == null)
                {
                    return false;
                }

                XmlElement node = SelectOrCreateSingleNode(doc, nsmgr, baseNode, path: "dc:subject/rdf:Bag");

                foreach (string word in keywordsToAdd)
                {
                    XmlElement keywordElement = CreateElement(doc, nsmgr, node: "rdf:li");
                    XmlText textEntry = doc.CreateTextNode(word);
                    keywordElement.AppendChild(textEntry);

                    node.AppendChild(keywordElement);
                }

                XmlWriterSettings xmlWriterSerttings = new XmlWriterSettings
                                                       {
                                                           OmitXmlDeclaration = true,
                                                           Encoding = Encoding.UTF8,
                                                           Indent = true,
                                                           NewLineHandling = NewLineHandling.Entitize,
                                                           NewLineOnAttributes = true
                                                       };

                using (XmlWriter w = XmlWriter.Create(fileName, xmlWriterSerttings))
                {
                    doc.Save(w);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Creates the element.
        /// </summary>
        /// <param name="document">
        ///     The document.
        /// </param>
        /// <param name="namespaceManager">
        ///     The namespace manager.
        /// </param>
        /// <param name="node">
        ///     The node to create.
        /// </param>
        /// <returns>
        ///     The created element.
        /// </returns>
        private static XmlElement CreateElement(XmlDocument document, XmlNamespaceManager namespaceManager, string node)
        {
            Contract.Requires(document != null);
            Contract.Requires(namespaceManager != null);
            Contract.Requires(!string.IsNullOrEmpty(node));
            Contract.Ensures(Contract.Result<XmlElement>() != null);

            string[] nodeParts = node.Split(separator: ':');

            if (nodeParts.Length == 2)
            {
                string namespaceUri = namespaceManager.LookupNamespace(nodeParts[0]);

                return document.CreateElement(nodeParts[0], nodeParts[1], namespaceUri);
            }

            return document.CreateElement(nodeParts[0]);
        }

        /// <summary>
        ///     The create loaders.
        /// </summary>
        /// <returns>
        ///     The extracted properties.
        /// </returns>
        private static IEnumerable<IItemLoader> CreateLoaders()
        {
            Contract.Ensures(Contract.Result<IEnumerable<IItemLoader>>() != null);

            yield return new AttributeItemLoader(MetadataNames.CameraManufacturer, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@tiff:Make");
            yield return new ElementItemLoader(MetadataNames.CameraManufacturer, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/tiff:Make");
            yield return new AttributeItemLoader(MetadataNames.CameraModel, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@tiff:Model");
            yield return new ElementItemLoader(MetadataNames.CameraModel, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/tiff:Model");
            yield return new AttributeItemLoader(MetadataNames.Orientation, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@tiff:Orientation");
            yield return new ElementItemLoader(MetadataNames.Orientation, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/tiff:Orientation");
            yield return new AttributeItemLoader(MetadataNames.ExposureTime, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:ExposureTime");
            yield return new ElementItemLoader(MetadataNames.ExposureTime, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:ExposureTime");
            yield return new AttributeItemLoader(MetadataNames.Aperture, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:FNumber");
            yield return new ElementItemLoader(MetadataNames.Aperture, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:FNumber");
            yield return new AttributeItemLoader(MetadataNames.DateTaken, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:DateTimeOriginal");
            yield return new ElementItemLoader(MetadataNames.DateTaken, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:DateTimeOriginal");
            yield return new AttributeItemLoader(MetadataNames.Rating, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@xmp:Rating");
            yield return new ElementItemLoader(MetadataNames.Rating, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@xmp:Rating");
            yield return new AttributeItemLoader(MetadataNames.FocalLength, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:FocalLength");
            yield return new ElementItemLoader(MetadataNames.FocalLength, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:FocalLength");

            //yield return new AttributeItemLoader(MetadataNames.Lens, "/x:xmpmeta/rdf:RDF/rdf:Description/@aux:Lens");
            //yield return new ElementItemLoader(MetadataNames.Lens, "/x:xmpmeta/rdf:RDF/rdf:Description/aux:Lens");
            yield return new AttributeItemLoader(MetadataNames.Latitude, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:GPSLatitude");
            yield return new ElementItemLoader(MetadataNames.Latitude, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:GPSLatitude");

            yield return new AttributeItemLoader(MetadataNames.Longitude, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/@exif:GPSLongitude");
            yield return new ElementItemLoader(MetadataNames.Longitude, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:GPSLongitude");

            yield return new ElementItemLoader(MetadataNames.IsoSpeed, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/exif:ISOSpeedRatings/rdf:Seq/rdf:li");
            yield return new ElementItemListLoader(MetadataNames.Keywords, pathToItem: "/x:xmpmeta/rdf:RDF/rdf:Description/dc:subject/rdf:Bag/rdf:li");
        }

        /// <summary>
        ///     Creates the name space manager.
        /// </summary>
        /// <param name="document">
        ///     The XML document.
        /// </param>
        /// <returns>
        ///     The XML name space manager.
        /// </returns>
        private static XmlNamespaceManager CreateNamespaceManager(XmlDocument document)
        {
            Contract.Requires(document != null);

            Contract.Ensures(Contract.Result<XmlNamespaceManager>() != null);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
            nsmgr.AddNamespace(prefix: "x", uri: "adobe:ns:meta/");
            nsmgr.AddNamespace(prefix: "rdf", uri: "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
            nsmgr.AddNamespace(prefix: "tiff", uri: "http://ns.adobe.com/tiff/1.0/");
            nsmgr.AddNamespace(prefix: "exif", uri: "http://ns.adobe.com/exif/1.0/");
            nsmgr.AddNamespace(prefix: "aux", uri: "http://ns.adobe.com/exif/1.0/aux/");
            nsmgr.AddNamespace(prefix: "xmp", uri: "http://ns.adobe.com/xap/1.0/");
            nsmgr.AddNamespace(prefix: "photoshop", uri: "http://ns.adobe.com/photoshop/1.0/");
            nsmgr.AddNamespace(prefix: "xmpMM", uri: "http://ns.adobe.com/xap/1.0/mm/");
            nsmgr.AddNamespace(prefix: "dc", uri: "http://purl.org/dc/elements/1.1/");
            nsmgr.AddNamespace(prefix: "Iptc4xmpCore", uri: "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/");
            nsmgr.AddNamespace(prefix: "xmpRights", uri: "http://ns.adobe.com/xap/1.0/rights/");
            nsmgr.AddNamespace(prefix: "lr", uri: "http://ns.adobe.com/lightroom/1.0/");

            return nsmgr;
        }

        private static string NormalizeValue(string name, string value)
        {
            if (StringComparer.InvariantCultureIgnoreCase.Equals(name, MetadataNames.FocalLength))
            {
                if (SplitParts(value, out uint v1, out uint v2))
                {
                    double d = MetadataNormalizationFunctions.ToReal(v1, v2);

                    return MetadataFormatting.FormatFocalLength(d);
                }
            }

            if (StringComparer.InvariantCultureIgnoreCase.Equals(name, MetadataNames.Aperture))
            {
                if (SplitParts(value, out uint v1, out uint v2))
                {
                    double d = MetadataNormalizationFunctions.ToReal(v1, v2);

                    return MetadataFormatting.FormatFNumber(d);
                }
            }

            if (StringComparer.InvariantCultureIgnoreCase.Equals(name, MetadataNames.ExposureTime))
            {
                if (SplitParts(value, out uint v1, out uint v2))
                {
                    double d = MetadataNormalizationFunctions.ToReal(v1, v2);

                    return MetadataFormatting.FormatExposure(d);
                }
            }

            if (StringComparer.InvariantCultureIgnoreCase.Equals(name, MetadataNames.Orientation))
            {
                if (int.TryParse(value, out int orientation))
                {
                    // http://sylvana.net/jpegcrop/exif_orientation.html
                    //  1        2       3      4         5            6           7          8

                    //888888  888888      88  88      8888888888  88                  88  8888888888
                    //88          88      88  88      88  88      88  88          88  88      88  88
                    //8888      8888    8888  8888    88          8888888888  8888888888          88
                    //88          88      88  88
                    //88          88  888888  888888
                    switch (orientation)
                    {
                        case 1: return "TopLeft";
                        case 2: return "TopRight";
                        case 3: return "BottomRight";
                        case 4: return "BottomLeft";
                        case 5: return "LeftTop";
                        case 6: return "RightTop";
                        case 7: return "RightBottom";
                        case 8: return "LeftBottom";
                    }
                }
            }

            return value;
        }

        /// <summary>
        ///     Selects or creates a single node.
        /// </summary>
        /// <param name="document">
        ///     The document.
        /// </param>
        /// <param name="namespaceManager">
        ///     The namespace manager.
        /// </param>
        /// <param name="baseNode">
        ///     The base node.
        /// </param>
        /// <param name="path">
        ///     The path to the node.
        /// </param>
        /// <returns>
        ///     The node to that path.
        /// </returns>
        private static XmlElement SelectOrCreateSingleNode(XmlDocument document, XmlNamespaceManager namespaceManager, XmlNode baseNode, string path)
        {
            Contract.Requires(document != null);
            Contract.Requires(namespaceManager != null);
            Contract.Requires(baseNode != null);
            Contract.Requires(!string.IsNullOrEmpty(path));

            XmlNode fullNode = baseNode.SelectSingleNode(path, namespaceManager);

            if (fullNode != null)
            {
                return (XmlElement) fullNode;
            }

            string[] fragments = path.Split(separator: '/');

            // find the most deep element that exists
            int depth = 0;

            while (depth < fragments.Length)
            {
                XmlNode found = baseNode.SelectSingleNode(fragments[depth], namespaceManager);

                if (found == null)
                {
                    break;
                }

                ++depth;
                baseNode = found;
            }

            // Create any nodes that need creating.
            while (depth < fragments.Length)
            {
                string node = fragments[depth];
                XmlElement newElement = CreateElement(document, namespaceManager, node);
                baseNode.AppendChild(newElement);
                baseNode = newElement;
                ++depth;
            }

            return (XmlElement) baseNode;
        }

        private static bool SplitParts(string value, out uint v1, out uint v2)
        {
            string[] split = value.Split(separator: '/');

            if (split.Length != 2)
            {
                v1 = 0;
                v2 = 0;

                return false;
            }

            if (!uint.TryParse(split[0], out v1))
            {
                v2 = 0;

                return false;
            }

            if (!uint.TryParse(split[1], out v2))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Stores the value.
        /// </summary>
        /// <param name="loader">
        ///     The loader.
        /// </param>
        /// <param name="properties">
        ///     The properties.
        /// </param>
        /// <param name="value">
        ///     The value.
        /// </param>
        private static void StoreValue(IItemLoader loader, Dictionary<string, string> properties, string value)
        {
            Contract.Requires(loader != null);
            Contract.Requires(properties != null);

            if (properties.TryGetValue(loader.Name, out string lastValue))
            {
                if (string.IsNullOrWhiteSpace(lastValue) && !string.IsNullOrWhiteSpace(value))
                {
                    properties[loader.Name] = NormalizeValue(loader.Name, value);
                }
            }
            else
            {
                if (value != null)
                {
                    properties.Add(loader.Name, NormalizeValue(loader.Name, value));
                }
            }
        }
    }
}