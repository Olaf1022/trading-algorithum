﻿/*
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

using System;
using System.Linq;
using QuantConnect.Data;

namespace QuantConnect.Algorithm.CSharp.Benchmarks
{
    /// <summary>
    /// Demonstration of the Scheduled Events features available in QuantConnect.
    /// </summary>
    public class ScheduledEventsBenchmark : QCAlgorithm
    {
        public override void Initialize()
        {
            SetStartDate(2008, 1, 1);
            SetEndDate(2018, 1, 1);
            SetCash(100000);
            AddSecurity(SecurityType.Equity, "SPY", Resolution.Daily);

            foreach (int period in Enumerable.Range(0, 100))
            {

                Schedule.On(DateRules.EveryDay("SPY"), TimeRules.AfterMarketOpen("SPY", period), () =>
                {
                });

                Schedule.On(DateRules.EveryDay("SPY"), TimeRules.BeforeMarketClose("SPY", period), () =>
                {
                });

            }
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromSeconds(5)), () =>
            {
            });
        }

        public override void OnData(Slice data)
        {
        }
    }
}