﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.IO;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds.Transport
{
    /// <summary>
    /// Represents a stream reader capabable of downloading a remote file and then
    /// reading it from disk
    /// </summary>
    public class RemoteFileSubscriptionStreamReader : IStreamReader
    {
        private readonly IStreamReader _streamReader;
        private static IDownloadProvider _downloader;

        /// <summary>
        /// Gets whether or not this stream reader should be rate limited
        /// </summary>
        public bool ShouldBeRateLimited => false;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteFileSubscriptionStreamReader"/> class.
        /// </summary>
        /// <param name="dataCacheProvider">The <see cref="IDataCacheProvider"/> used to retrieve a stream of data</param>
        /// <param name="source">The remote url to be downloaded via web client</param>
        /// <param name="downloadDirectory">The local directory and destination of the download</param>
        /// <param name="headers">Defines header values to add to the request</param>
        /// <param name="liveMode">True if running in live mode</param>
        public RemoteFileSubscriptionStreamReader(IDataCacheProvider dataCacheProvider, string source, string downloadDirectory, IEnumerable<KeyValuePair<string, string>> headers, bool liveMode)
        {
            // for live mode we don't want to cache
            var doNotUseCache = dataCacheProvider.IsDataEphemeral // will be true for live history requests
                || liveMode; // will be false for live history requests but true for live subscriptions

            // create a hash for a new filename
            var filename = doNotUseCache ? Guid.NewGuid().ToString() : source.ToMD5() + source.GetExtension();
            var destination = Path.Combine(downloadDirectory, filename);

            if (doNotUseCache || !File.Exists(destination))
            {
                var contents = _downloader.Download(source, headers, null, null);
                File.WriteAllText(destination, contents);

                // Send the file to the dataCacheProvider so it is available when the streamReader asks for it
                dataCacheProvider.Store(destination, System.Text.Encoding.UTF8.GetBytes(contents));
            }

            // now we can just use the local file reader
            _streamReader = new LocalFileSubscriptionStreamReader(dataCacheProvider, destination);
        }

        /// <summary>
        /// Gets <see cref="SubscriptionTransportMedium.RemoteFile"/>
        /// </summary>
        public SubscriptionTransportMedium TransportMedium
        {
            get { return SubscriptionTransportMedium.RemoteFile; }
        }

        /// <summary>
        /// Gets whether or not there's more data to be read in the stream
        /// </summary>
        public bool EndOfStream
        {
            get { return _streamReader.EndOfStream; }
        }

        /// <summary>
        /// Gets the next line/batch of content from the stream
        /// </summary>
        public string ReadLine()
        {
            return _streamReader.ReadLine();
        }

        /// <summary>
        /// Disposes of the stream
        /// </summary>
        public void Dispose()
        {
            _streamReader.Dispose();
        }

        /// <summary>
        /// Save reference to the download system.
        /// </summary>
        /// <param name="downloader">Downloader provider for the remote file fetching.</param>
        public static void SetDownloadProvider(IDownloadProvider downloader)
        {
            _downloader = downloader;
        }
    }
}
