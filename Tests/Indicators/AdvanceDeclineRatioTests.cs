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
*/

using NUnit.Framework;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class AdvanceDeclineRatioTests : CommonIndicatorTests<TradeBar>
    {
        protected override IndicatorBase<TradeBar> CreateIndicator()
        {
            var adr = new AdvanceDeclineRatio("test_name");
            adr.AddStock(Symbols.AAPL);
            adr.AddStock(Symbols.IBM);
            adr.AddStock(Symbols.GOOG);
            return adr;
        }

        [Test]
        public virtual void ShouldIgnoreRemovedStocks()
        {
            var adr = (AdvanceDeclineRatio)CreateIndicator();
            var reference = System.DateTime.Today;

            adr.Update(new TradeBar() { Symbol = Symbols.AAPL, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });
            adr.Update(new TradeBar() { Symbol = Symbols.IBM, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });
            adr.Update(new TradeBar() { Symbol = Symbols.GOOG, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });

            // value is not ready yet
            Assert.AreEqual(0m, adr.Current.Value);

            adr.Update(new TradeBar() { Symbol = Symbols.AAPL, Close = 2, Volume = 100, Time = reference.AddMinutes(2) });
            adr.Update(new TradeBar() { Symbol = Symbols.IBM, Close = 0.5m, Volume = 100, Time = reference.AddMinutes(2) });
            adr.Update(new TradeBar() { Symbol = Symbols.GOOG, Close = 3, Volume = 100, Time = reference.AddMinutes(2) });

            Assert.AreEqual(2m, adr.Current.Value);
            adr.Reset();
            adr.RemoveStock(Symbols.GOOG);

            adr.Update(new TradeBar() { Symbol = Symbols.AAPL, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });
            adr.Update(new TradeBar() { Symbol = Symbols.IBM, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });
            adr.Update(new TradeBar() { Symbol = Symbols.GOOG, Close = 1, Volume = 100, Time = reference.AddMinutes(1) });

            // value is not ready yet
            Assert.AreEqual(0m, adr.Current.Value);

            adr.Update(new TradeBar() { Symbol = Symbols.AAPL, Close = 2, Volume = 100, Time = reference.AddMinutes(2) });
            adr.Update(new TradeBar() { Symbol = Symbols.IBM, Close = 0.5m, Volume = 100, Time = reference.AddMinutes(2) });
            adr.Update(new TradeBar() { Symbol = Symbols.GOOG, Close = 3, Volume = 100, Time = reference.AddMinutes(2) });

            Assert.AreEqual(1m, adr.Current.Value);
        }

        protected override string TestFileName => "arms_data.txt";

        protected override string TestColumnName => "A/D Ratio";
    }
}