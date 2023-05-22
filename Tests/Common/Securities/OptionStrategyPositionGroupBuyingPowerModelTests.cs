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
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Option.StrategyMatcher;
using QuantConnect.Securities.Positions;
using QuantConnect.Logging;
using QuantConnect.Tests.Engine.DataFeeds;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class OptionStrategyPositionGroupBuyingPowerModelTests
    {
        private QCAlgorithm _algorithm;
        private SecurityPortfolioManager _portfolio;
        private QuantConnect.Securities.Equity.Equity _equity;
        private Option _callOption;
        private Option _putOption;

        [SetUp]
        public void Setup()
        {
            _algorithm = new AlgorithmStub();
            _algorithm.SetCash(1000000);
            _algorithm.SetSecurityInitializer(security => security.FeeModel = new ConstantFeeModel(0));
            _portfolio = _algorithm.Portfolio;

            _equity = _algorithm.AddEquity("SPY");

            var strike = 200m;
            var expiry = new DateTime(2016, 1, 15);

            var callOptionSymbol = Symbols.CreateOptionSymbol("SPY", OptionRight.Call, strike, expiry);
            _callOption = _algorithm.AddOptionContract(callOptionSymbol);

            var putOptionSymbol = Symbols.CreateOptionSymbol("SPY", OptionRight.Put, strike, expiry);
            _putOption = _algorithm.AddOptionContract(putOptionSymbol);
        }

        /// <summary>
        /// All these test cases are based on test done on local IB TWS and assuming an initial cash of 1,000,000
        /// </summary>
        // option strategy definition, initial quantity, order quantity, expected result
        private static readonly TestCaseData[] HasSufficientBuyingPowerForOrderTestCases = new[]
        {
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 0, -50, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 0, -60, false),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 0, 40, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 0, 50, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 20, -50 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 20, -60 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 20, +40 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 20, +50 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -20, -50 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -20, -60 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -20, 40 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -20, 50 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 0, 50, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 0, 60, false),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 0, -40, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 0, -50, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 20, 50 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 20, 60 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 20, -40 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 20, -50 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -20, -50 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -20, -60 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -20, 50 - -20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -20, 60 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 0, -80, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 0, -1000000 / 10250 + 1, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 0, 90, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 0, 100, false),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 20, -80 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 20, -90 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 20, +90 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 20, +100 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -20, +90 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -20, +100 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -20, -1000000 / 10250 - -20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -20, -1000000 / 10250 + 1 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 0, 80, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 0, 1000000 / 10250 + 1, false),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 0, -90, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 0, -100, false),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 20, 80 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 20, 90 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 20, -90 - 20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 20, -100 - 20, false),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -20, -90 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -20, -100 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -20, 1000000 / 10250 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -20, 1000000 / 10250 + 1 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 0, 1000, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 0, 1010, false),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 0, -980, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 0, -990, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 20, 1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 20, 1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 20, -980 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 20, -990 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -20, -980 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -20, -990 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -20, 1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -20, 1010 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 0, 1000, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 0, 10000, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 0, -1000, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 0, -1010, false),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 20, 1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 20, 10000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 20, -1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 20, -1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -20, -1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -20, -1010 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -20, 1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -20, 10000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 0, 970, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 0, 990, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 0, -990, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 0, -1010, false),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 20, 970 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 20, 990 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 20, -990 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 20, -1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -20, -990 - -20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -20, -1010 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -20, 970 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -20, 990 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 0, 990, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 0, 1010, false),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 0, -1000, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 0, -10000, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 20, 990 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 20, 1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 20, -1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 20, -10000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -20, -1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -20, -10000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -20, 990 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -20, 1010 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 0, 85, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 0, 90, false),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 0, -50, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 0, -55, false),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 20, 85 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 20, 90 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 20, -50 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 20, -55 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -20, -50 - -20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -20, -55 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -20, 85 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -20, 90 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 0, 90, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 0, 110, false),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 0, -50, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 0, -60, false),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 20, 90 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 20, 110 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 20, -50 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 20, -60 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -20, -50 - -20, true).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -20, -60 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -20, 90 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -20, 110 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 0, 700, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 0, 720, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 0, -640, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 0, -660, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 20, 700 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 20, 720 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 20, -640 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 20, -660 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -20, -640 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -20, -660 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -20, 700 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -20, 720 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 0, 640, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 0, 660, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 0, -700, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 0, -720, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 20, 640 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 20, 660 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 20, -700 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 20, -720 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -20, -700 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -20, -720 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -20, 640 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -20, 660 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 0, 1000, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 0, 10000, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 0, -990, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 0, -1010, false),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 20, 1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 20, 10000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 20, -990 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 20, -1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -20, -990 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -20, -1010 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -20, 1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -20, 10000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 0, 990, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 0, 1010, false),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 0, -1000, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 0, -10000, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 20, 990 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 20, 1010 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 20, -1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 20, -10000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -20, -1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -20, -10000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -20, 990 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -20, 1010 - -20, false),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 0, 1300, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 0, 1310, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 0, -40, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 0, -60, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 20, 1300 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 20, 1310 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 20, -40 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 20, -60 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -20, -40 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -20, -60 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -20, 1300 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -20, 1310 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 0, 1000, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 0, 10000, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 0, -310, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 0, -330, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 20, 1000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 20, 10000 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 20, -310 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 20, -330 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -20, -310 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -20, -330 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -20, 1000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -20, 10000 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 0, 980, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 0, 1000, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 0, -980, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 0, -1000, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 20, 980 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 20, 1000 - 20, false),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 20, -20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 20, -980 - 20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 20, -1000 - 20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -20, -980 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -20, -1000 - -20, false).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -20, 20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -20, 980 - -20, true),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -20, 1000 - -20, false).Explicit(),
        };

        [TestCaseSource(nameof(HasSufficientBuyingPowerForOrderTestCases))]
        public void HasSufficientBuyingPowerForOrder(OptionStrategyDefinition optionStrategy, int initialPositionQuantity, int orderQuantity,
            bool expectedResult)
        {
            _algorithm.SetCash(1000000);

            var initialPositionGroup = SetUpOptionStrategy(optionStrategy, initialPositionQuantity);
            var orders = GetPositionGroupOrders(initialPositionGroup, initialPositionQuantity != 0 ? initialPositionQuantity : 1, orderQuantity);
            var ordersPositionGroup = _portfolio.Positions.CreatePositionGroup(orders);

            var result = ordersPositionGroup.BuyingPowerModel.HasSufficientBuyingPowerForOrder(
                new HasSufficientPositionGroupBuyingPowerForOrderParameters(_portfolio, ordersPositionGroup, orders));

            Assert.AreEqual(expectedResult, result.IsSufficient, result.Reason);
        }

        [Test]
        public void HasSufficientBuyingPowerForStrategyOrder([Values] bool withInitialHoldings)
        {
            const decimal price = 1.2345m;
            const decimal underlyingPrice = 200m;

            _algorithm.SetCash(100000);
            var initialMargin = _portfolio.MarginRemaining;

            _equity.SetMarketPrice(new Tick { Value = underlyingPrice });
            _callOption.SetMarketPrice(new Tick { Value = price });
            _putOption.SetMarketPrice(new Tick { Value = price });

            var initialHoldingsQuantity = withInitialHoldings ? -10 : 0;
            _callOption.Holdings.SetHoldings(1.5m, initialHoldingsQuantity);
            _putOption.Holdings.SetHoldings(1m, initialHoldingsQuantity);

            var optionStrategy = OptionStrategies.Straddle(_callOption.Symbol.Canonical, _callOption.StrikePrice, _callOption.Expiry);

            var sufficientCaseConsidered = false;
            var insufficientCaseConsidered = false;

            // make sure these cases are considered:
            // 1. liquidating part of the position
            var partialLiquidationCaseConsidered = false;
            // 2. liquidating the whole position
            var fullLiquidationCaseConsidered = false;
            // 3. shorting more, but with margin left
            var furtherShortingWithMarginRemainingCaseConsidered = false;
            // 4. shorting even more to the point margin is no longer enough
            var furtherShortingWithNoMarginRemainingCaseConsidered = false;

            for (var strategyQuantity = Math.Abs(initialHoldingsQuantity); strategyQuantity > -30; strategyQuantity--)
            {
                var buyingPowerModel = new OptionStrategyPositionGroupBuyingPowerModel(
                    _callOption.Holdings.Quantity + strategyQuantity == 0
                        // Liquidating
                        ? null
                        : optionStrategy);
                var orders = GetStrategyOrders(strategyQuantity);

                var positionGroup = _portfolio.Positions.CreatePositionGroup(orders);

                var maintenanceMargin = buyingPowerModel.GetMaintenanceMargin(
                    new PositionGroupMaintenanceMarginParameters(_portfolio, positionGroup));

                var hasSufficientBuyingPowerResult = buyingPowerModel.HasSufficientBuyingPowerForOrder(
                    new HasSufficientPositionGroupBuyingPowerForOrderParameters(_portfolio, positionGroup, orders));

                Assert.AreEqual(maintenanceMargin < initialMargin, hasSufficientBuyingPowerResult.IsSufficient);

                if (hasSufficientBuyingPowerResult.IsSufficient)
                {
                    sufficientCaseConsidered = true;
                }
                else
                {
                    Assert.IsTrue(sufficientCaseConsidered, "All 'sufficient buying power' case should have been before the 'insufficient' ones");

                    insufficientCaseConsidered = true;
                }

                var newPositionQuantity = positionGroup.Quantity;
                if (newPositionQuantity == 0)
                {
                    fullLiquidationCaseConsidered = true;
                }
                else if (newPositionQuantity < 0)
                {
                    if (newPositionQuantity > initialHoldingsQuantity)
                    {
                        partialLiquidationCaseConsidered = true;
                    }
                    else if (hasSufficientBuyingPowerResult.IsSufficient)
                    {
                        furtherShortingWithMarginRemainingCaseConsidered = true;
                    }
                    else
                    {
                        furtherShortingWithNoMarginRemainingCaseConsidered = true;
                    }
                }
            }

            Assert.IsTrue(sufficientCaseConsidered, "The 'sufficient buying power' case was not considered");
            Assert.IsTrue(insufficientCaseConsidered, "The 'insufficient buying power' case was not considered");

            if (withInitialHoldings)
            {
                Assert.IsTrue(partialLiquidationCaseConsidered, "The 'partial liquidation' case was not considered");
                Assert.IsTrue(fullLiquidationCaseConsidered, "The 'full liquidation' case was not considered");
            }

            Assert.IsTrue(furtherShortingWithMarginRemainingCaseConsidered, "The 'further shorting with margin remaining' case was not considered");
            Assert.IsTrue(furtherShortingWithNoMarginRemainingCaseConsidered, "The 'further shorting with no margin remaining' case was not considered");
        }

        [Test]
        public void HasSufficientBuyingPowerForReducingStrategyOrder()
        {
            const decimal price = 1m;
            const decimal underlyingPrice = 200m;

            _equity.SetMarketPrice(new Tick { Value = underlyingPrice });
            _callOption.SetMarketPrice(new Tick { Value = price });
            _putOption.SetMarketPrice(new Tick { Value = price });

            var initialHoldingsQuantity = -10;
            _callOption.Holdings.SetHoldings(1.5m, initialHoldingsQuantity);
            _putOption.Holdings.SetHoldings(1m, initialHoldingsQuantity);

            _algorithm.SetCash(_portfolio.TotalMarginUsed * 0.95m);

            var optionStrategy = OptionStrategies.Straddle(_callOption.Symbol.Canonical, _callOption.StrikePrice, _callOption.Expiry);
            var quantity = -initialHoldingsQuantity / 2;
            var buyingPowerModel = new OptionStrategyPositionGroupBuyingPowerModel(optionStrategy);
            var orders = GetStrategyOrders(quantity);

            var positionGroup = _portfolio.Positions.CreatePositionGroup(orders);

            var parameters = new HasSufficientPositionGroupBuyingPowerForOrderParameters(_portfolio, positionGroup, orders);
            var availableBuyingPower = buyingPowerModel.GetPositionGroupBuyingPower(parameters.Portfolio, parameters.PositionGroup, orders.First().GroupOrderManager.Direction);
            var deltaBuyingPowerArgs = new ReservedBuyingPowerImpactParameters(parameters.Portfolio, parameters.PositionGroup, parameters.Orders);
            var deltaBuyingPower = buyingPowerModel.GetReservedBuyingPowerImpact(deltaBuyingPowerArgs).Delta;

            // Buying power should be sufficient for reducing the position, even if the delta buying power is greater than the available buying power
            Assert.Less(deltaBuyingPower, 0);
            Assert.Greater(deltaBuyingPower, availableBuyingPower);

            var hasSufficientBuyingPowerResult = buyingPowerModel.HasSufficientBuyingPowerForOrder(
                new HasSufficientPositionGroupBuyingPowerForOrderParameters(_portfolio, positionGroup, orders));

            Assert.IsTrue(hasSufficientBuyingPowerResult.IsSufficient);
        }

        /// <summary>
        /// Test cases for the <see cref="OptionStrategyPositionGroupBuyingPowerModel.GetInitialMarginRequirement"/> method.
        ///
        /// TODO: We should come back and revisit these test cases to make sure they are correct.
        /// The approximate values from IB for the prices used in the test are in the comments.
        /// For instance, see the test case for the CoveredCall strategy. The margin values do not match IB's values.
        ///
        /// Test cases marked as explicit will fail if ran, they have an approximate expected value based on IB's margin requirements.
        /// </summary>
        private static readonly TestCaseData[] InitialMarginRequirementsTestCases = new[]
        {
            // OptionStrategyDefinition, initialHoldingsQuantity, expectedInitialMarginRequirement
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 1, 19000m),                     // IB:  19325
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -1, 12000m),                    // IB:  12338.58
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 1, 12000m),                      // IB:  12331.38
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -1, 10000m),                     // IB:  10276.15
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 1, 1000m),                   // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -1, 0m),                     // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 1, 0m),                       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -1, 1000m),                   // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 1, 0m),                      // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -1, 1000m),                  // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 1, 1000m),                    // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -1, 0m),                      // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Straddle, 1, 0m).Explicit(),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Straddle, -1, 3000m).Explicit(),             // IB:  3001.60
            new TestCaseData(OptionStrategyDefinitions.Strangle, 1, 0m).Explicit(),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Strangle, -1, 3000m).Explicit(),             // IB:  3001.60
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 1, 0m),                       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -1, 0m).Explicit(),           // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 1, 0m).Explicit(),       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -1, 0m),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 1, 0m),                        // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -1, 1000m),                    // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 1, 1000m),                // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -1, 0m),                  // IB:  0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 1, 0m),                  // IB:  0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -1, 0m),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 1, 0m),                   // IB:  0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -1, 3000m).Explicit(),    // IB:  3001.6
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 1, 1000m),                       // IB:  1017.62
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -1, 0m),                         // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 1, 12000m),                  // IB:  inverted covered call
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -1, 19000m),                 // IB:  covered call
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 1, 10000m),                   // IB:  inverted covered put
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -1, 12000m),                  // IB:  covered put
        };

        [TestCaseSource(nameof(InitialMarginRequirementsTestCases))]
        public void GetsInitialMarginRequirement(OptionStrategyDefinition optionStrategyDefinition, int quantity,
            decimal expectedInitialMarginRequirement)
        {
            var positionGroup = SetUpOptionStrategy(optionStrategyDefinition, quantity);

            var initialMarginRequirement = positionGroup.BuyingPowerModel.GetInitialMarginRequirement(
                new PositionGroupInitialMarginParameters(_portfolio, positionGroup));

            Assert.AreEqual((double)expectedInitialMarginRequirement, (double)initialMarginRequirement.Value, (double)(0.2m * expectedInitialMarginRequirement));
        }

        private static readonly TestCaseData[] CoveredCallInitialMarginRequirementsTestCases = new[]
        {
            // OptionStrategyDefinition, initialHoldingsQuantity, expectedInitialMarginRequirement, option strike
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 2, 53700m, 200),                     // IB: 53,714
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 2, 38000m, 300),                     // IB: 38,756
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 2, 23000m, 400),                     // IB: 23,752
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 2, 21000m, 500),                     // IB: 20,939
        };

        [TestCaseSource(nameof(CoveredCallInitialMarginRequirementsTestCases))]
        public void CoveredCallInitialMarginRequirement(OptionStrategyDefinition optionStrategyDefinition, int quantity,
            decimal expectedInitialMarginRequirement, int strike)
        {
            var positionGroup = SetUpOptionStrategy(optionStrategyDefinition, quantity, strike);

            var initialMarginRequirement = positionGroup.BuyingPowerModel.GetInitialMarginRequirement(
                new PositionGroupInitialMarginParameters(_portfolio, positionGroup));

            Assert.AreEqual((double)expectedInitialMarginRequirement, (double)initialMarginRequirement.Value, (double)(0.2m * expectedInitialMarginRequirement));
        }

        /// <summary>
        /// Test cases for the <see cref="OptionStrategyPositionGroupBuyingPowerModel.GetMaintenanceMargin"/> method.
        ///
        /// TODO: We should come back and revisit these test cases to make sure they are correct.
        /// The approximate values from IB for the prices used in the test are in the comments.
        /// For instance, see the test case for the CoveredCall strategy. The margin values do not match IB's values.
        ///
        /// Test cases marked as explicit will fail if ran, they have an approximate expected value based on IB's margin requirements.
        /// </summary>
        private static readonly TestCaseData[] MaintenanceMarginTestCases = new[]
        {
            // OptionStrategyDefinition, initialHoldingsQuantity, expectedMaintenanceMargin
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 1, 19000m),                     // IB:  19325
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -1, 3000m),                     // IB:  3000
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 1, 10250m),                      // IB:  12000m
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -1, 10000m),                     // IB:  10276
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 1, 1000m),                   // IB:  10000
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -1, 0m),                     // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 1, 0m),                       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -1, 1000m),                   // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 1, 0m),                      // IB:  0
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -1, 1000m),                  // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 1, 1000m),                    // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -1, 0m),                      // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Straddle, 1, 0m).Explicit(),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Straddle, -1, 3000m).Explicit(),             // IB:  3001.60
            new TestCaseData(OptionStrategyDefinitions.Strangle, 1, 0m).Explicit(),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.Strangle, -1, 3000m).Explicit(),             // IB:  3001.60
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 1, 0m),                       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -1, 0m).Explicit(),           // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 1, 0m).Explicit(),       // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -1, 0m),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 1, 0m),                        // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -1, 1000m),                    // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 1, 1000m),                // IB:  1000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -1, 0m),                  // IB:  0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 1, 0m),                  // IB:  0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -1, 0m),                 // IB:  0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 1, 0m),                   // IB:  0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -1, 3000m).Explicit(),    // IB:  3001.6
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 1, 1000m),                       // IB:  1017.62
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -1, 0m),                         // IB:  0
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 1, 3000m),                   // IB:  inverted covered call
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -1, 19000m),                 // IB:  covered call
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 1, 10000m),                   // IB:  inverted covered Put
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -1, 10250m),                  // IB:  covered Put
        };

        [TestCaseSource(nameof(MaintenanceMarginTestCases))]
        public void GetsMaintenanceMargin(OptionStrategyDefinition optionStrategyDefinition, int quantity, decimal expectedMaintenanceMargin)
        {
            var positionGroup = SetUpOptionStrategy(optionStrategyDefinition, quantity);

            var maintenanceMargin = positionGroup.BuyingPowerModel.GetMaintenanceMargin(
                new PositionGroupMaintenanceMarginParameters(_portfolio, positionGroup));

            Assert.AreEqual((double)expectedMaintenanceMargin, (double)maintenanceMargin.Value, (double)(0.2m * expectedMaintenanceMargin));
        }

        // option strategy definition, initial position quantity, final position quantity
        private static readonly TestCaseData[] OrderQuantityForDeltaBuyingPowerTestCases = new[]
        {
            // Initial margin for ProtectiveCall with quantity 10 is 125000m
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 125000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -125000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -125000m, -10).Explicit(),
            // Initial margin for ProtectivePut with quantity 10 is 104000m
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 100000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -100000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -100000m, -10).Explicit(),

            // Initial margin for CoveredCall with quantity 10 is 192100m
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 192100m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -192100m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -192100m, -10).Explicit(),
            // Initial margin for CoveredCall with quantity -10 is 112000
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 112000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, -112000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, -112000m, +10).Explicit(),
            // Initial margin for CoveredPut with quantity 10 is 102500
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 102500 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -102500 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -102500, -10).Explicit(),
            // Initial margin for CoveredPut with quantity -10 is 102500
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 102500m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, -102500m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, -102500m, +10),
            // Initial margin for BullCallSpread with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 1000m, 1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -1000m, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -10000m, 10).Explicit(),
            // Initial margin for BullCallSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 0m, 0).Explicit(),
            // Initial margin for BearPutSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, 0m, 0).Explicit(),
            // Initial margin for BearPutSpread with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, -10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, -10000m, +10).Explicit(),
            // Initial margin for BullCallSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, 0m, 0).Explicit(),
            // Initial margin for BullCallSpread with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, -10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, -10000m, +10).Explicit(),
            // Initial margin for BullPutSpread with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -10000m, -10).Explicit(),
            // Initial margin for BullPutSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 0m, 0).Explicit(),
            // Initial margin for Straddle with quantity 10 is 112020
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 112020m / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -112020m / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -112020m, -10).Explicit(),
            // Initial margin for Straddle with quantity -10 is 235019
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 235019m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, -235019m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, -235019m, +10).Explicit(),
            // Initial margin for Strangle with quantity 10 is 102020
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 102020m / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -102020m / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -102020m, -10).Explicit(),
            // Initial margin for Strangle with quantity -10 is 225020
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 225020m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, -225020m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, -225020m, +10).Explicit(),
            // Initial margin for ButterflyCall with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, 0m, 0).Explicit(),
            // Initial margin for ButterflyCall with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, -10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, -10000m, +10).Explicit(),
            // Initial margin for ShortButterflyCall with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -10000m, -10).Explicit(),
            // Initial margin for ShortButterflyCall with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 0m, 0).Explicit(),
            // Initial margin for ButterflyPut with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, 0m, 0).Explicit(),
            // Initial margin for ButterflyPut with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, -10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, -10000m, +10).Explicit(),
            // Initial margin for ShortButterflyPut with quantity 10 is 1000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -10000m, -10).Explicit(),
            // Initial margin for ShortButterflyPut with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 0m, 0).Explicit(),
            // Initial margin for CallCalendarSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, 0m, 0).Explicit(),
            // Initial margin for CallCalendarSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 0m, 0).Explicit(),
            // Initial margin for PutCalendarSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, 0m, 0).Explicit(),
            // Initial margin for PutCalendarSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 0m, 0).Explicit(),
            // Initial margin for IronCondor with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 10000m / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -10000m / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -10000m, -10).Explicit(),
            // Initial margin for IronCondor with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 0m, 0).Explicit(),
        };

        [TestCaseSource(nameof(OrderQuantityForDeltaBuyingPowerTestCases))]
        public void PositionGroupOrderQuantityCalculationForDeltaBuyingPower(OptionStrategyDefinition optionStrategyDefinition,
            int initialPositionQuantity, decimal deltaBuyingPower, int expectedQuantity)
        {
            var positionGroup = SetUpOptionStrategy(optionStrategyDefinition, initialPositionQuantity);

            var quantity = positionGroup.BuyingPowerModel.GetMaximumLotsForDeltaBuyingPower(new GetMaximumLotsForDeltaBuyingPowerParameters(
                _portfolio, positionGroup, deltaBuyingPower, minimumOrderMarginPortfolioPercentage: 0)).NumberOfLots;

            Assert.AreEqual(expectedQuantity, quantity);
        }

        // option strategy definition, initial position quantity, target buying power percent, expected quantity
        private static readonly TestCaseData[] OrderQuantityForTargetBuyingPowerTestCases = new[]
        {
            // Initial margin requirement for ProtectiveCall with quantity 10 is 125000m
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 125000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 125000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 0m, -10).Explicit(),
            // Initial margin requirement for CoveredCall with quantity -10 is 192100m
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 192100m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 192100m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 0m, +10).Explicit(),
            // Initial margin requirement for ProtectivePut with quantity 10 is 100000m
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 100000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 100000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 0m, -10).Explicit(),
            // Initial margin requirement for CoveredPut with quantity -10 is 205000
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 205000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 205000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 0m, +10).Explicit(),
            // Initial margin requirement for CoveredCall with quantity 10 is 192100m
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 192100m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 192100m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 0m, -10),
            // Initial margin requirement for CoveredCall with quantity -10 is 125000m
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 125000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 125000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 0m, +10),
            // Initial margin requirement for CoveredPut with quantity 10 is 205000
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 205000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 205000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 0m, -10),
            // Initial margin requirement for CoveredPut with quantity -10 is 100000m
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 100000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 100000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 0m, +10),
            // Initial margin requirement for BearCallSpread with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 10000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 10000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 0m, -10),
            // Initial margin requirement for BearCallSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 0m, 0).Explicit(),
            // Initial margin requirement for BearPutSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, 0m, 0).Explicit(),
            // Initial margin requirement for BearPutSpread with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 10000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 10000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 0m, +10).Explicit(),
            // Initial margin requirement for BullCallSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, 0m, 0).Explicit(),
            // Initial margin requirement for BullCallSpread with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 10000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 10000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 0m, +10).Explicit(),
            // Initial margin requirement for BullPutSpread with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 10000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 10000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 0m, -10),
            // Initial margin requirement for BullPutSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 0m, 0).Explicit(),
            // Initial margin requirement for Straddle with quantity 10 is 112020
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 112020m * 11 / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 112020m * 9 / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 0m, -10).Explicit(),
            // Initial margin requirement for Straddle with quantity -10 is 235019
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 235019m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 235019m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 0m, +10).Explicit(),
            // Initial margin requirement for Strangle with quantity 10 is 102020
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 102020m * 11 / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 102020m * 9 / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 0m, -10).Explicit(),
            // Initial margin requirement for Strangle with quantity -10 is 225020
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 225020m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 225020m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 0m, +10).Explicit(),
            // Initial margin requirement for ButterflyCall with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, 0m, 0).Explicit(),
            // Initial margin requirement for ButterflyCall with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 10000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 10000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 0m, +10).Explicit(),
            // Initial margin requirement for ShortButterflyCall with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 10000m * 11 / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 10000m * 9 / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 0m, -10),
            // Initial margin requirement for ShortButterflyCall with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 0m, 0).Explicit(),
            // Initial margin requirement for ButterflyPut with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, 0m, 0).Explicit(),
            // Initial margin requirement for ButterflyPut with quantity -10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 10000m * 11 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 10000m * 9 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 0m, +10).Explicit(),
            // Initial margin requirement for ShortButterflyPut with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 10000m * 11 / 10, +1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 10000m * 9 / 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 0m, -10),
            // Initial margin requirement for ShortButterflyPut with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 0m, 0).Explicit(),
            // Initial margin requirement for CallCalendarSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, 0m, 0).Explicit(),
            // Initial margin requirement for CallCalendarSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 0m, 0).Explicit(),
            // Initial margin requirement for PutCalendarSpread with quantity 10 is 0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, 0m, 0).Explicit(),
            // Initial margin requirement for PutCalendarSpread with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 0m, 0).Explicit(),
            // Initial margin requirement for IronCondor with quantity 10 is 10000
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 10000m * 11 / 10, +1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 10000m * 9 / 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 0m, -10).Explicit(),
            // Initial margin requirement for IronCondor with quantity -10 is 0
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 0m, 0).Explicit(),
        };

        [TestCaseSource(nameof(OrderQuantityForTargetBuyingPowerTestCases))]
        public void PositionGroupOrderQuantityCalculationForTargetBuyingPower(OptionStrategyDefinition optionStrategyDefinition,
            int initialPositionQuantity, decimal targetBuyingPower, int expectedQuantity)
        {
            var positionGroup = SetUpOptionStrategy(optionStrategyDefinition, initialPositionQuantity);

            var targetBuyingPowerPercent = targetBuyingPower / _portfolio.TotalPortfolioValue;

            var quantity = positionGroup.BuyingPowerModel.GetMaximumLotsForTargetBuyingPower(new GetMaximumLotsForTargetBuyingPowerParameters(
                _portfolio, positionGroup, targetBuyingPowerPercent, minimumOrderMarginPortfolioPercentage: 0)).NumberOfLots;

            Assert.AreEqual(expectedQuantity, quantity);
        }

        private static readonly TestCaseData[] PositionGroupBuyingPowerTestCases = new[]
        {
            // option strategy definition, initial position quantity, new position quantity
            // Starting from the "initial position quantity", we want to get the buying power available for an order that would get us to
            // the "new position quantity" (if we don't take into account the initial position).
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 1), // Going from 10 to 11
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -1), // Going from 10 to 9
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -10).Explicit(), // Going from 10 to 0
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -20).Explicit(), // Going from 10 to -10
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 1).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -20).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 10).Explicit(),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 20),
        };

        [TestCaseSource(nameof(PositionGroupBuyingPowerTestCases))]
        public void BuyingPowerForPositionGroupCalculation(OptionStrategyDefinition optionStrategyDefinition, int initialPositionQuantity,
            int newGroupQuantity)
        {
            var initialMargin = _portfolio.MarginRemaining;
            var initialPositionGroup = SetUpOptionStrategy(optionStrategyDefinition, initialPositionQuantity);

            var positionGroup = _portfolio.Positions.ResolvePositionGroups(new PositionCollection(
                initialPositionGroup.Positions.Select(position => new Position(position.Symbol,
                    position.Quantity / initialPositionQuantity * newGroupQuantity, position.UnitQuantity)))).Single();

            var finalQuantity = initialPositionQuantity + newGroupQuantity;
            OrderDirection direction;
            if (Math.Abs(finalQuantity) < Math.Abs(initialPositionQuantity))
            {
                direction = initialPositionGroup.GetPositionSide() == PositionSide.Long ? OrderDirection.Sell : OrderDirection.Buy;
            }
            else
            {
                direction = initialPositionGroup.GetPositionSide() == PositionSide.Long ? OrderDirection.Buy : OrderDirection.Sell;
            }
            var buyingPower = positionGroup.BuyingPowerModel.GetPositionGroupBuyingPower(new PositionGroupBuyingPowerParameters(_portfolio,
                positionGroup, direction));

            var initialUsedMargin = _portfolio.TotalMarginUsed;
            var initialPositionInitialMargin = positionGroup.BuyingPowerModel.GetInitialMarginRequirement(
                new PositionGroupInitialMarginParameters(_portfolio, initialPositionGroup));
            Log.Debug($"Initial used margin: {initialUsedMargin}");
            Log.Debug($"Initial position initial margin requirement: {initialPositionInitialMargin}");
            Log.Debug($"Final quantity: {finalQuantity}");

            // Initial and final positions are in the same side
            if (Math.Sign(finalQuantity) == Math.Sign(initialPositionQuantity))
            {
                // Increasing a position
                if (Math.Abs(finalQuantity) > Math.Abs(initialPositionQuantity))
                {
                    Assert.AreEqual(initialMargin - initialUsedMargin, buyingPower.Value);
                }
                // Reducing or closing a position
                else
                {
                    var positionGroupBuyingPower = positionGroup.BuyingPowerModel.GetReservedBuyingPowerForPositionGroup(
                        new ReservedBuyingPowerForPositionGroupParameters(_portfolio, positionGroup));
                    Assert.AreEqual(initialMargin - initialUsedMargin + initialPositionInitialMargin + positionGroupBuyingPower, buyingPower.Value);
                }
            }
            // Switching position side
            else
            {
                Assert.AreEqual(initialMargin, buyingPower.Value);
            }
        }

        private static readonly TestCaseData[] ReservedBuyingPowerImpactTestCases = new[]
        {
            // option strategy definition, initial position quantity, new position quantity
            // Starting from the "initial position quantity", we want to get the buying power available for an order that would get us to
            // the "new position quantity" (if we don't take into account the initial position).
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, 1), // Going from 10 to 11
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -1), // Going from 10 to 9
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -10), // Going from 10 to 0
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, 10, -20), // Going from 10 to -10
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.CoveredCall, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.CoveredPut, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BearCallSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BearPutSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BullCallSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.BullPutSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.Straddle, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.Straddle, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.Strangle, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.Strangle, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ButterflyCall, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyCall, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ButterflyPut, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ShortButterflyPut, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.CallCalendarSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.PutCalendarSpread, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.IronCondor, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ProtectiveCall, -10, 20),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -10),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, 10, -20),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, -1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 1),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 10),
            new TestCaseData(OptionStrategyDefinitions.ProtectivePut, -10, 20),
        };

        [TestCaseSource(nameof(ReservedBuyingPowerImpactTestCases))]
        public void ReservedBuyingPowerImpactCalculation(OptionStrategyDefinition optionStrategyDefinition, int initialPositionQuantity,
            int newGroupQuantity)
        {
            var initialMargin = _portfolio.MarginRemaining;
            var initialPositionGroup = SetUpOptionStrategy(optionStrategyDefinition, initialPositionQuantity);

            var positionGroup = _portfolio.Positions.ResolvePositionGroups(new PositionCollection(
                initialPositionGroup.Positions.Select(position => new Position(position.Symbol,
                    position.Quantity / initialPositionQuantity * newGroupQuantity, position.UnitQuantity)))).Single();

            var finalQuantity = initialPositionQuantity + newGroupQuantity;

            var buyingPowerImpact = positionGroup.BuyingPowerModel.GetReservedBuyingPowerImpact(new ReservedBuyingPowerImpactParameters(_portfolio,
                positionGroup, GetPositionGroupOrders(initialPositionGroup, initialPositionQuantity, newGroupQuantity)));

            var initialUsedMargin = initialPositionGroup.BuyingPowerModel.GetReservedBuyingPowerForPositionGroup(
                new ReservedBuyingPowerForPositionGroupParameters(_portfolio, initialPositionGroup)).AbsoluteUsedBuyingPower;
            Log.Debug($"Initial used margin: {initialUsedMargin}");
            Log.Debug($"Final quantity: {finalQuantity}");

            foreach (var contemplatedChangePosition in buyingPowerImpact.ContemplatedChanges)
            {
                var position = positionGroup.SingleOrDefault(p => contemplatedChangePosition.Symbol == p.Symbol);
                Assert.IsNotNull(position);
                Assert.AreEqual(position.Quantity, contemplatedChangePosition.Quantity);
            }

            Assert.That(buyingPowerImpact.Current, Is.EqualTo(initialUsedMargin).Within(1e-18));

            // Either initial and final positions are in the same side or we are liquidating
            if (Math.Sign(finalQuantity) == Math.Sign(initialPositionQuantity) || finalQuantity == 0)
            {
                var expectedDelta = Math.Abs(newGroupQuantity * initialUsedMargin / initialPositionQuantity)
                    * (Math.Abs(finalQuantity) < Math.Abs(initialPositionQuantity) ? -1 : +1);
                Assert.That(buyingPowerImpact.Delta, Is.EqualTo(expectedDelta).Within(1e-18));
                Assert.That(buyingPowerImpact.Contemplated, Is.EqualTo(initialUsedMargin + expectedDelta).Within(1e-18));
            }
            // Switching position side
            else
            {
                var finalPositionGroup = _portfolio.Positions.ResolvePositionGroups(new PositionCollection(
                    initialPositionGroup.Positions.Select(position =>
                        position.Combine(positionGroup.Positions.Single(x => x.Symbol == position.Symbol))))).Single();
                var finalPositionGroupMargin = finalPositionGroup.BuyingPowerModel.GetReservedBuyingPowerForPositionGroup(
                    new ReservedBuyingPowerForPositionGroupParameters(_portfolio, finalPositionGroup)).AbsoluteUsedBuyingPower;
                var expectedDelta = finalPositionGroupMargin - initialUsedMargin;
                Assert.That(buyingPowerImpact.Delta, Is.EqualTo(expectedDelta).Within(1e-18));
                Assert.That(buyingPowerImpact.Contemplated, Is.EqualTo(finalPositionGroupMargin).Within(1e-18));
            }
        }

        private List<Order> GetStrategyOrders(decimal quantity)
        {
            var groupOrderManager = new GroupOrderManager(1, 2, quantity);
            return new List<Order>()
            {
                Order.CreateOrder(new SubmitOrderRequest(
                    OrderType.ComboMarket,
                    _callOption.Type,
                    _callOption.Symbol,
                    1m.GetOrderLegGroupQuantity(groupOrderManager),
                    0,
                    0,
                    _algorithm.Time,
                    "",
                    groupOrderManager: groupOrderManager)),
                Order.CreateOrder(new SubmitOrderRequest(
                    OrderType.ComboMarket,
                    _putOption.Type,
                    _putOption.Symbol,
                    1m.GetOrderLegGroupQuantity(groupOrderManager),
                    0,
                    0,
                    _algorithm.Time,
                    "",
                    groupOrderManager: groupOrderManager))
            };
        }

        private List<Order> GetPositionGroupOrders(IPositionGroup positionGroup, decimal initialPositionGroupQuantity, decimal quantity)
        {
            var groupOrderManager = new GroupOrderManager(1, positionGroup.Count, quantity);
            return positionGroup.Positions.Select(position => Order.CreateOrder(new SubmitOrderRequest(
                OrderType.ComboMarket,
                position.Symbol.SecurityType,
                position.Symbol,
                (position.Quantity / initialPositionGroupQuantity).GetOrderLegGroupQuantity(groupOrderManager),
                0,
                0,
                _algorithm.Time,
                "",
                groupOrderManager: groupOrderManager))).ToList();
        }

        private IPositionGroup SetUpOptionStrategy(OptionStrategyDefinition optionStrategyDefinition, int initialHoldingsQuantity, int? strike = null)
        {
            if (initialHoldingsQuantity == 0)
            {
                var group = SetUpOptionStrategy(optionStrategyDefinition, 1);
                foreach (var position in group.Positions)
                {
                    var security = _algorithm.Securities[position.Symbol];
                    security.Holdings.SetHoldings(0, 0);
                }
                Assert.AreEqual(0, _portfolio.PositionGroups.Count);

                return group;
            }

            var may172023 = new DateTime(2023, 05, 17);
            var may192023 = new DateTime(2023, 05, 19);

            var spyMay19_300Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 300, may192023));
            spyMay19_300Call.SetMarketPrice(new Tick { Value = 112m });
            var spyMay19_310Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 310, may192023));
            spyMay19_310Call.SetMarketPrice(new Tick { Value = 102m });
            var spyMay19_320Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 320, may192023));
            spyMay19_320Call.SetMarketPrice(new Tick { Value = 92m });
            var spyMay19_330Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 330, may192023));
            spyMay19_330Call.SetMarketPrice(new Tick { Value = 82m });

            var spyMay17_200Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 200, may172023));
            spyMay17_200Call.SetMarketPrice(new Tick { Value = 220m });
            var spyMay17_400Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 400, may172023));
            spyMay17_400Call.SetMarketPrice(new Tick { Value = 28m });
            var spyMay17_300Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 300, may172023));
            spyMay17_300Call.SetMarketPrice(new Tick { Value = 112m });
            var spyMay17_500Call = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Call, 500, may172023));
            spyMay17_500Call.SetMarketPrice(new Tick { Value = 0.04m });

            var spyMay19_300Put = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Put, 300, may192023));
            spyMay19_300Put.SetMarketPrice(new Tick { Value = 0.02m });
            var spyMay19_310Put = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Put, 310, may192023));
            spyMay19_310Put.SetMarketPrice(new Tick { Value = 0.02m });
            var spyMay19_320Put = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Put, 320, may192023));
            spyMay19_320Put.SetMarketPrice(new Tick { Value = 0.03m });
            var spyMay17_300Put = _algorithm.AddOptionContract(Symbols.CreateOptionSymbol("SPY", OptionRight.Put, 300, may172023));
            spyMay17_300Put.SetMarketPrice(new Tick { Value = 0.01m });

            _equity.SetMarketPrice(new Tick { Value = 410m });
            _equity.SetLeverage(4);

            var expectedPositionGroupBPMStrategy = optionStrategyDefinition.Name;

            if (optionStrategyDefinition.Name == OptionStrategyDefinitions.CoveredCall.Name)
            {
                _equity.Holdings.SetHoldings(_equity.Price, initialHoldingsQuantity * _callOption.ContractMultiplier);

                var optionContract = spyMay19_300Call;
                if(strike.HasValue)
                {
                    switch (strike.Value)
                    {
                        case 200:
                            optionContract = spyMay17_200Call;
                            break;
                        case 300:
                            optionContract = spyMay17_300Call;
                            break;
                        case 400:
                            optionContract = spyMay17_400Call;
                            break;
                        case 500:
                            optionContract = spyMay17_500Call;
                            break;
                    }
                }

                optionContract.Holdings.SetHoldings(optionContract.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ProtectiveCall.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ProtectiveCall.Name)
            {
                _equity.Holdings.SetHoldings(_equity.Price, -initialHoldingsQuantity * _callOption.ContractMultiplier);
                spyMay19_300Call.Holdings.SetHoldings(spyMay19_300Call.Price, initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.CoveredCall.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.CoveredPut.Name)
            {
                _equity.Holdings.SetHoldings(_equity.Price, -initialHoldingsQuantity * _putOption.ContractMultiplier);
                spyMay19_300Put.Holdings.SetHoldings(spyMay19_300Put.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ProtectivePut.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ProtectivePut.Name)
            {
                _equity.Holdings.SetHoldings(_equity.Price, initialHoldingsQuantity * _putOption.ContractMultiplier);
                spyMay19_300Put.Holdings.SetHoldings(spyMay19_300Put.Price, initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.CoveredPut.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.BearCallSpread.Name)
            {
                var shortCallOption = spyMay19_300Call;
                var longCallOption = spyMay19_310Call;

                shortCallOption.Holdings.SetHoldings(shortCallOption.Price, -initialHoldingsQuantity);
                longCallOption.Holdings.SetHoldings(longCallOption.Price, initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.BullCallSpread.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.BearPutSpread.Name)
            {
                var longPutOption = spyMay19_310Put;
                var shortPutOption = spyMay19_300Put;

                longPutOption.Holdings.SetHoldings(longPutOption.Price, initialHoldingsQuantity);
                shortPutOption.Holdings.SetHoldings(shortPutOption.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.BullPutSpread.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.BullCallSpread.Name)
            {
                var shortCallOption = spyMay19_310Call;
                var longCallOption = spyMay19_300Call;

                longCallOption.Holdings.SetHoldings(longCallOption.Price, initialHoldingsQuantity);
                shortCallOption.Holdings.SetHoldings(shortCallOption.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.BearCallSpread.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.BullPutSpread.Name)
            {
                var longPutOption = spyMay19_300Put;
                var shortPutOption = spyMay19_310Put;

                longPutOption.Holdings.SetHoldings(longPutOption.Price, initialHoldingsQuantity);
                shortPutOption.Holdings.SetHoldings(shortPutOption.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.BearPutSpread.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.Straddle.Name)
            {
                spyMay19_300Call.Holdings.SetHoldings(spyMay19_300Call.Price, initialHoldingsQuantity);
                spyMay19_300Put.Holdings.SetHoldings(spyMay19_300Put.Price, initialHoldingsQuantity);
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.Strangle.Name)
            {
                spyMay19_310Call.Holdings.SetHoldings(spyMay19_310Call.Price, initialHoldingsQuantity);
                spyMay19_300Put.Holdings.SetHoldings(spyMay19_300Put.Price, initialHoldingsQuantity);
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ButterflyCall.Name)
            {
                var lowerStrikeCallOption = spyMay19_300Call;
                var middleStrikeCallOption = spyMay19_310Call;
                var upperStrikeCallOption = spyMay19_320Call;

                lowerStrikeCallOption.Holdings.SetHoldings(lowerStrikeCallOption.Price, initialHoldingsQuantity);
                middleStrikeCallOption.Holdings.SetHoldings(middleStrikeCallOption.Price, -2 * initialHoldingsQuantity);
                upperStrikeCallOption.Holdings.SetHoldings(upperStrikeCallOption.Price, initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ShortButterflyCall.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ShortButterflyCall.Name)
            {
                var lowerStrikeCallOption = spyMay19_300Call;
                var middleStrikeCallOption = spyMay19_310Call;
                var upperStrikeCallOption = spyMay19_320Call;

                lowerStrikeCallOption.Holdings.SetHoldings(lowerStrikeCallOption.Price, -initialHoldingsQuantity);
                middleStrikeCallOption.Holdings.SetHoldings(middleStrikeCallOption.Price, 2 * initialHoldingsQuantity);
                upperStrikeCallOption.Holdings.SetHoldings(middleStrikeCallOption.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ButterflyCall.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ButterflyPut.Name)
            {
                var lowerStrikePutOption = spyMay19_300Put;
                var middleStrikePutOption = spyMay19_310Put;
                var upperStrikePutOption = spyMay19_320Put;

                lowerStrikePutOption.Holdings.SetHoldings(lowerStrikePutOption.Price, initialHoldingsQuantity);
                middleStrikePutOption.Holdings.SetHoldings(middleStrikePutOption.Price, -2 * initialHoldingsQuantity);
                upperStrikePutOption.Holdings.SetHoldings(upperStrikePutOption.Price, initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ShortButterflyPut.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.ShortButterflyPut.Name)
            {
                var lowerStrikePutOption = spyMay19_300Put;
                var middleStrikePutOption = spyMay19_310Put;
                var upperStrikePutOption = spyMay19_320Put;

                lowerStrikePutOption.Holdings.SetHoldings(lowerStrikePutOption.Price, -initialHoldingsQuantity);
                middleStrikePutOption.Holdings.SetHoldings(middleStrikePutOption.Price, 2 * initialHoldingsQuantity);
                upperStrikePutOption.Holdings.SetHoldings(upperStrikePutOption.Price, -initialHoldingsQuantity);

                if (initialHoldingsQuantity < 0)
                {
                    expectedPositionGroupBPMStrategy = OptionStrategyDefinitions.ButterflyPut.Name;
                }
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.CallCalendarSpread.Name)
            {
                var longCallOption = spyMay19_300Call;
                var shortCallOption = spyMay17_300Call;

                longCallOption.Holdings.SetHoldings(longCallOption.Price, initialHoldingsQuantity);
                shortCallOption.Holdings.SetHoldings(shortCallOption.Price, -initialHoldingsQuantity);
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.PutCalendarSpread.Name)
            {
                var longPutOption = spyMay19_300Put;
                var shortPutOption = spyMay17_300Put;

                longPutOption.Holdings.SetHoldings(longPutOption.Price, initialHoldingsQuantity);
                shortPutOption.Holdings.SetHoldings(shortPutOption.Price, -initialHoldingsQuantity);
            }
            else if (optionStrategyDefinition.Name == OptionStrategyDefinitions.IronCondor.Name)
            {
                var longPutOption = spyMay19_300Put;
                var shortPutOption = spyMay19_310Put;
                var shortCallOption = spyMay19_320Call;
                var longCallOption = spyMay19_330Call;

                longPutOption.Holdings.SetHoldings(longPutOption.Price, initialHoldingsQuantity);
                shortPutOption.Holdings.SetHoldings(shortPutOption.Price, -initialHoldingsQuantity);
                shortCallOption.Holdings.SetHoldings(shortCallOption.Price, -initialHoldingsQuantity);
                longCallOption.Holdings.SetHoldings(longCallOption.Price, initialHoldingsQuantity);
            }

            var positionGroup = _portfolio.PositionGroups.Single();
            Assert.AreEqual(expectedPositionGroupBPMStrategy, positionGroup.BuyingPowerModel.ToString());

            return positionGroup;
        }
    }
}
