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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// DataManager will manage the subscriptions for both the DataFeeds and the SubscriptionManager
    /// </summary>
    public class DataManager : IDataManager
    {
        /// There is no ConcurrentHashSet collection in .NET,
        /// so we use ConcurrentDictionary with byte value to minimize memory usage
        private readonly ConcurrentDictionary<SubscriptionDataConfig, byte> _subscriptionManagerSubscriptions
            = new ConcurrentDictionary<SubscriptionDataConfig, byte>();

        /// <summary>
        /// Gets the data feed subscription collection
        /// </summary>
        public readonly SubscriptionCollection DataFeedSubscriptions = new SubscriptionCollection();

        /// <summary>
        /// Gets all the current data config subscriptions that are being processed
        /// </summary>
        public IEnumerable<SubscriptionDataConfig> SubscriptionManagerSubscriptions => _subscriptionManagerSubscriptions.Select(x => x.Key);

        /// <summary>
        /// Returns true if the given subscription data config is already present
        /// </summary>
        public bool SubscriptionManagerTryAdd(SubscriptionDataConfig config)
        {
            return _subscriptionManagerSubscriptions.TryAdd(config, 0);
        }

        /// <summary>
        /// Returns true if the given subscription data config is already present
        /// </summary>
        public bool SubscriptionManagerContainsKey(SubscriptionDataConfig config)
        {
            return _subscriptionManagerSubscriptions.ContainsKey(config);
        }

        /// <summary>
        /// Returns the amount of data config subscriptions processed
        /// </summary>
        public int SubscriptionManagerCount()
        {
            return _subscriptionManagerSubscriptions.Skip(0).Count();
        }
    }
}
