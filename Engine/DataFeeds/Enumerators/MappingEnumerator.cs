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

using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Enumerator who will emit <see cref="SymbolChangedEvent"/> events
    /// </summary>
    public class MappingEnumerator : CorporateEventBaseEnumerator
    {
        private readonly SubscriptionDataConfig _config;
        private readonly MapFile _mapFile;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="tradableDayNotifier">Tradable dates provider</param>
        /// <param name="mapFile">The <see cref="MapFile"/> to use</param>
        /// <param name="includeAuxiliaryData">True to emit auxiliary data</param>
        public MappingEnumerator(
            SubscriptionDataConfig config,
            MapFile mapFile,
            ITradableDatesNotifier tradableDayNotifier,
            bool includeAuxiliaryData)
            : base(config, tradableDayNotifier, includeAuxiliaryData)
        {
            _config = config;
            _mapFile = mapFile;
        }

        /// <summary>
        /// Check for new mappings
        /// </summary>
        /// <param name="eventArgs">The new tradable day event arguments</param>
        /// <returns>New mapping event, else Null</returns>
        protected override IEnumerable<BaseData> GetCorporateEvents(NewTradableDateEventArgs eventArgs)
        {
            if (_mapFile.HasData(eventArgs.Date))
            {
                // check to see if the symbol was remapped
                var newSymbol = _mapFile.GetMappedSymbol(eventArgs.Date, _config.MappedSymbol);
                if (newSymbol != _config.MappedSymbol)
                {
                    var changed = new SymbolChangedEvent(_config.Symbol, eventArgs.Date, _config.MappedSymbol, newSymbol);
                    _config.MappedSymbol = newSymbol;
                    yield return changed;
                }
            }
        }
    }
}
