// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the data attachment.
    /// Dev10 equivalent is UriDataAttachment.
    /// </summary>
    [DataContract]
    public class UriDataAttachment
    {
        /// <summary>
        /// Description of the attachment.
        /// </summary>
        [DataMember]
        public string Description { get; private set; }

        /// <summary>
        /// Uri of the attchment.
        /// </summary>
        [DataMember]
        public Uri Uri { get; private set; }

        public UriDataAttachment(Uri uri, string description)
        {
            this.Uri = uri;
            this.Description = description;
        }

        public override string ToString()
        {
            return $"{nameof(this.Uri)}: {this.Uri.AbsoluteUri}, {nameof(this.Description)}: {this.Description}";
        }
    }
}
