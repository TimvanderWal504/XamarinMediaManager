﻿using System;
using System.ComponentModel;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager.Abstractions
{
    /// <summary>
    /// Information about the mediafile
    /// </summary>
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    public interface IMediaFile : INotifyPropertyChanged
    {
        /// <summary>
        /// A unique identifier for this media file
        /// </summary>
        Guid Id { get; set; }

        /// <summary>
        /// Indicator for player which type of file it should play
        /// </summary>
        MediaFileType Type { get; set; }

        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>
        /// The URL.
        /// </value>
        string Url { get; set; }

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        /// <value>
        /// The metadata.
        /// </value>
        IMediaFileMetadata Metadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [metadata extracted].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [metadata extracted]; otherwise, <c>false</c>.
        /// </value>
        bool MetadataExtracted { get; set; }
    }
}

