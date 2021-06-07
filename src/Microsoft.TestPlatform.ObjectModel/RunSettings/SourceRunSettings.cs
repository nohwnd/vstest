// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// The source run settings.
    /// </summary>
    public class SourceRunSettings : TestRunSettings
    {
        private string SourceRunSettingsName = string.Empty;
        private string sourcesSettingName = string.Empty;
        private string sourceSettingName = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceRunSettings"/> class.
        /// </summary>
        public SourceRunSettings() : base(Constants.SourceRunSettingsName)
        {
            this.SourceSettingsList = new Collection<SourceSettings>();
            this.SourceRunSettingsName = Constants.SourceRunSettingsName;
            this.sourcesSettingName = Constants.SourcesSettingName;
            this.sourceSettingName = Constants.SourceSettingName;
        }

        /// <summary>
        /// Gets the source settings list.
        /// </summary>
        public Collection<SourceSettings> SourceSettingsList
        {
            get;
            private set;
        }

#if !NETSTANDARD1_0
        public override XmlElement ToXml()
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement(this.SourceRunSettingsName);
            var subRoot = doc.CreateElement(this.sourcesSettingName);
            root.AppendChild(subRoot);

            foreach (var sourceSettings in this.SourceSettingsList)
            {
                XmlNode child = doc.ImportNode(sourceSettings.ToXml(this.sourceSettingName), true);
                subRoot.AppendChild(child);
            }

            return root;
        }
#endif

        /// <summary>
        /// The from xml.
        /// </summary>
        /// <param name="reader">
        /// The reader.
        /// </param>
        /// <returns>
        /// The <see cref="SourceRunSettings"/>.
        /// </returns>
        /// <exception cref="SettingsException">
        /// Settings exception
        /// </exception>
        internal static SourceRunSettings FromXml(XmlReader reader)
        {
            ValidateArg.NotNull(reader, nameof(reader));

            return FromXml(reader,
                Constants.SourcesSettingName,
                Constants.SourceSettingName);
        }

        /// <summary>
        /// The from xml.
        /// </summary>
        /// <param name="reader">
        /// The reader.
        /// </param>
        /// <param name="sourcesSettingName">
        /// Sources setting name.
        /// </param>
        /// <param name="sourceSettingName">
        /// Source setting name.
        /// </param>
        /// <returns>
        /// The <see cref="SourceRunSettings"/>
        /// </returns>
        private static SourceRunSettings FromXml(XmlReader reader, string sourcesSettingName, string sourceSettingName)
        {
            // Validation.
            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);

            var elementName = reader.Name;
            var empty = reader.IsEmptyElement;
            var settings = new SourceRunSettings();

            // Move to next node.
            reader.Read();

            // Return empty settings if previous element empty.
            if (empty)
            {
                return settings;
            }

            // Read inner nodes.
            while (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Name.Equals(sourcesSettingName, StringComparison.OrdinalIgnoreCase))
                {
                    var items = ReadListElementFromXml(reader, sourceSettingName);
                    foreach (var item in items)
                    {
                        settings.SourceSettingsList.Add(item);
                    }
                }
                else
                {
                    throw new SettingsException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.Resources.InvalidSettingsXmlElement,
                            elementName,
                            reader.Name));
                }
            }
            reader.ReadEndElement();

            return settings;
        }

        /// <summary>
        /// Reads source settings list from runSettings
        /// </summary>
        /// <param name="reader">
        /// The reader.
        /// </param>
        /// <param name="sourceSettingName">
        /// Source setting name.
        /// </param>
        /// <returns>
        /// SourceSettings List
        /// </returns>
        private static List<SourceSettings> ReadListElementFromXml(XmlReader reader, string sourceSettingName)
        {
            // Validation.
            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);

            var elementName = reader.Name;
            var empty = reader.IsEmptyElement;
            var settings = new List<SourceSettings>();

            // Move to next node
            reader.Read();

            // Return empty settings if previous element is empty.
            if (empty)
            {
                return settings;
            }

            // Read inner nodes.
            while (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Name.Equals(sourceSettingName, StringComparison.OrdinalIgnoreCase))
                {
                    settings.Add(SourceSettings.FromXml(reader));
                }
                else
                {
                    throw new SettingsException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.Resources.InvalidSettingsXmlElement,
                            elementName,
                            reader.Name));
                }
            }

            return settings;
        }

        /// <summary>
        /// Gets existing source index.
        /// </summary>
        /// <param name="sourceSettings">Source settings.</param>
        /// <returns>Index of given source settings.</returns>
        public int GetExistingSourceIndex(string path)
        {
            var existingSourceIndex = -1;

            for (int i = 0; i < SourceSettingsList.Count; i++)
            {
                var source = SourceSettingsList[i];

                if (source.Path != null &&
                    path != null &&
                    source.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    existingSourceIndex = i;
                    break;
                }
            }

            return existingSourceIndex;
        }
    }

    /// <summary>
    /// The source settings.
    /// </summary>
    public class SourceSettings
    {
        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        public string Path
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets Platform for this source.
        /// </summary>
        public Architecture Platform
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets Framework for this source.
        /// </summary>
        public Framework Framework
        {
            get;
            set;
        }

#if !NETSTANDARD1_0
        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        public XmlElement Configuration
        {
            get;
            set;
        }

        /// <summary>
        /// The to xml.
        /// </summary>
        /// <returns>
        /// The <see cref="XmlElement"/>.
        /// </returns>
        public XmlElement ToXml()
        {
            return ToXml(Constants.SourceSettingName);
        }

        /// <summary>
        /// The to xml.
        /// </summary>
        /// <param name="sourceName">
        /// The source name.
        /// </param>
        /// <returns>
        /// The <see cref="XmlElement"/>.
        /// </returns>
        public XmlElement ToXml(string sourceName)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement(sourceName);

            AppendAttribute(doc, root, Constants.SourcePath, this.Path);
            AppendAttribute(doc, root, Constants.SourcePlatform, this.Platform.ToString());
            AppendAttribute(doc, root, Constants.SourceFramework, this.Framework.ToString());

            if (Configuration != null)
            {
                root.AppendChild(doc.ImportNode(Configuration, true));
            }

            return root;
        }

        private static void AppendAttribute(XmlDocument doc, XmlElement owner, string attributeName, string attributeValue)
        {
            if (string.IsNullOrWhiteSpace(attributeValue))
            {
                return;
            }

            XmlAttribute attribute = doc.CreateAttribute(attributeName);
            attribute.Value = attributeValue;
            owner.Attributes.Append(attribute);
        }
#endif

        internal static SourceSettings FromXml(XmlReader reader)
        {
            var elementName = reader.Name;
            var empty = reader.IsEmptyElement;
            var settings = new SourceSettings();

            // Read attributes.
            if (reader.HasAttributes)
            {
                while (reader.MoveToNextAttribute())
                {
                    switch (reader.Name.ToLowerInvariant())
                    {
                        case Constants.SourcePath:
                            settings.Path = reader.Value;
                            break;

                        case Constants.SourcePlatform:
                            settings.Platform = (Architecture)Enum.Parse(typeof(Architecture), reader.Value, true);
                            break;

                        case Constants.SourceFramework:
                            settings.Framework = (Framework)Enum.Parse(typeof(Framework), reader.Value, true);
                            break;

                        default:
                            throw new SettingsException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Resources.Resources.InvalidSettingsXmlAttribute,
                                    elementName,
                                    reader.Name));
                    }
                }
            }

            // Check for required attributes.
            if (string.IsNullOrWhiteSpace(settings.Path))
            {
                throw new SettingsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Resources.MissingSourceAttributes,
                        elementName,
                        Constants.SourcePath));
            }

            // Move to next node.
            reader.Read();

            // Return empty settings if previous element is empty.
            if (empty)
            {
                return settings;
            }

            // Read inner elements.
            while (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name.ToLowerInvariant())
                {
#if !NETSTANDARD1_0
                    case Constants.SourceConfigurationNameLower:
                        var document = new XmlDocument();
                        var element = document.CreateElement(reader.Name);
                        element.InnerXml = reader.ReadInnerXml();
                        settings.Configuration = element;
                        break;
#endif
                    default:
                        throw new SettingsException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Resources.Resources.InvalidSettingsXmlElement,
                                elementName,
                                reader.Name));
                }
            }
            reader.ReadEndElement();

            return settings;
        }
    }
}
