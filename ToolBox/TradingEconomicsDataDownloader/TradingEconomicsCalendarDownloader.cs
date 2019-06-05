/*
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
*/

using Newtonsoft.Json;
using QuantConnect.Data.Custom.TradingEconomics;
using QuantConnect.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QuantConnect.ToolBox.TradingEconomicsDataDownloader
{
    /// <summary>
    /// Trading Economics Calendar Downloader class
    /// </summary>
    public class TradingEconomicsCalendarDownloader : TradingEconomicsDataDownloader
    {
        private readonly string _destinationFolder;
        private readonly DateTime _fromDate;
        private readonly DateTime _toDate;

        public TradingEconomicsCalendarDownloader(string destinationFolder)
        {
            _fromDate = new DateTime(2000, 10, 01);
            _toDate = DateTime.Now;
            _destinationFolder = Path.Combine(destinationFolder, "world", "daily");
            Directory.CreateDirectory(_destinationFolder);
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        /// <returns>True if process all downloads successfully</returns>
        public override bool Run()
        {
            var stopwatch = Stopwatch.StartNew();
            var data = new List<TradingEconomicsCalendar>();

            var startUtc = _fromDate;
            while (startUtc < _toDate)
            {
                try
                {
                    var endUtc = startUtc.AddMonths(1).AddDays(-1);
                    var collection = Get(startUtc, endUtc)
                        .SelectMany(JsonConvert.DeserializeObject<List<TradingEconomicsCalendar>>);

                    data.AddRange(collection);

                    startUtc = startUtc.AddMonths(1);
                }
                catch (Exception e)
                {
                    Log.Error(e, $"TradingEconomicsCalendarDownloader(): Error parsing data for date {startUtc:yyyyMMdd}");
                    return false;
                }
            }

            Log.Trace($"TradingEconomicsCalendarDownloader(): {data.Count} calendar entries read in {stopwatch.Elapsed}");

            foreach (var kvp in data.GroupBy(GetFileName))
            {
                var path = Path.Combine(_destinationFolder, kvp.Key);
                var zipPath = path.Replace(".json", ".zip");

                try
                {
                    var contents = JsonConvert.SerializeObject(kvp.ToList());
                    File.WriteAllText(path, contents);
                    // Write out this data string to a zip file
                    Compression.Zip(path, zipPath, kvp.Key, true);
                }
                catch (Exception e)
                {
                    Log.Error(e, $"TradingEconomicsCalendarDownloader(): Error creating {path}");
                    return false;
                }
            }

            Log.Trace($"TradingEconomicsCalendarDownloader(): Finished in {stopwatch.Elapsed}");
            return true;
        }

        /// <summary>
        /// Get Trading Economics Calendar data for a given this start and end times(in UTC).
        /// </summary>
        /// <param name="startUtc">Start time of the data in UTC</param>
        /// <param name="endUtc">End time of the data in UTC</param>
        /// <returns>Enumerable of string representing data for this date range</returns>
        public override IEnumerable<string> Get(DateTime startUtc, DateTime endUtc)
        {
            var url = $"/calendar/country/all/{startUtc:yyyy-MM-dd}/{endUtc:yyyy-MM-dd}";
            yield return HttpRequester(url).Result;
        }

        private string GetFileName(TradingEconomicsCalendar tradingEconomicsCalendar)
        {
            var ticker = tradingEconomicsCalendar.Ticker;
            if (string.IsNullOrWhiteSpace(ticker))
                ticker = tradingEconomicsCalendar.Category + tradingEconomicsCalendar.Country;

            return ticker.Replace(" ", "-").ToLower() + "_calendar.json";
        }
    }
}