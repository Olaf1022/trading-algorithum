﻿# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from clr import AddReference
from clr import GetClrType as typeof
AddReference("System")
AddReference("QuantConnect.Common")
AddReference("QuantConnect.Algorithm.Framework")

from QuantConnect import *
from QuantConnect.Securities import *
from QuantConnect.Data.Auxiliary import ZipEntryName
from QuantConnect.Data.UniverseSelection import FuturesChainUniverse
from Selection.UniverseSelectionModel import UniverseSelectionModel
from datetime import datetime

class FutureUniverseSelectionModel(UniverseSelectionModel):

    def __init__(self,
                 refreshInterval,
                 futureChainSymbolSelector,
                 universeSettings = None, 
                 securityInitializer = None):

        self.nextRefreshTimeUtc = datetime.min

        self.refreshInterval = refreshInterval
        self.futureChainSymbolSelector = futureChainSymbolSelector
        self.universeSettings = universeSettings
        self.securityInitializer = securityInitializer

    def GetNextRefreshTimeUtc(self):
        return self.nextRefreshTimeUtc

    def CreateUniverses(self, algorithm):
        self.nextRefreshTimeUtc = algorithm.UtcTime + self.refreshInterval

        algorithm.Log(f"FutureUniverseSelectionModel.CreateUniverse({algorithm.UtcTime}): Refreshing Universes")

        uniqueSymbols = set()
        for futureSymbol in self.futureChainSymbolSelector(algorithm.UtcTime):
            if futureSymbol.SecurityType != SecurityType.Future:
                raise ValueError("futureChainSymbolSelector must return future symbols.")

            # prevent creating duplicate future chains -- one per symbol
            if futureSymbol not in uniqueSymbols:
                uniqueSymbols.add(futureSymbol)
                yield self.CreateFutureChain(algorithm, futureSymbol)

    def CreateFutureChain(self, algorithm, symbol):
        if symbol.SecurityType != SecurityType.Future:
            raise ValueError("CreateFutureChain requires an future symbol.")

        algorithm.Log(f"FutureUniverseSelectionModel.CreateFutureChain({algorithm.UtcTime}, {symbol}): Creating Future Chain")

        # rewrite non-canonical symbols to be canonical
        market = symbol.ID.Market
        if not symbol.IsCanonical():
            symbol = Symbol.Create(symbol.Value, SecurityType.Future, market, f"/{symbol.Value}")

        # resolve defaults if not specified
        settings = self.universeSettings if self.universeSettings is not None else algorithm.UniverseSettings
        initializer = self.securityInitializer if self.securityInitializer is not None else algorithm.SecurityInitializer
        # create canonical security object, but don't duplicate if it already exists
        securities = [s for s in algorithm.Securities if s.Key == symbol]
        if len(securities) == 0:
            futureChain = self.CreateFutureChainSecurity(algorithm, symbol, settings, initializer)
        else:
            futureChain = securities[0]

            algorithm.Log(f"FutureUniverseSelectionModel.CreateFutureChain({algorithm.UtcTime}, {symbol}): Resolved existing Future Chain Security")

        # set the future chain contract filter function
        futureChain.SetFilter(self.Filter)

        # force future chain security to not be directly tradable AFTER it's configured to ensure it's not overwritten
        futureChain.IsTradable = False

        return FuturesChainUniverse(futureChain, settings, algorithm.SubscriptionManager, initializer)

    def CreateFutureChainSecurity(self, algorithm, symbol, settings, initializer):

        algorithm.Log(f"FutureUniverseSelectionModel.CreateFutureChainSecurity({algorithm.UtcTime}, {symbol}): Creating Future Chain Security")

        market = symbol.ID.Market

        marketHoursEntry = MarketHoursDatabase.FromDataFolder().GetEntry(market, symbol, SecurityType.Future)
        symbolProperties = SymbolPropertiesDatabase.FromDataFolder().GetSymbolProperties(market, symbol, SecurityType.Future, CashBook.AccountCurrency)

        return SecurityManager.CreateSecurity(typeof(ZipEntryName), algorithm.Portfolio,
                algorithm.SubscriptionManager, marketHoursEntry.ExchangeHours, marketHoursEntry.DataTimeZone, symbolProperties,
                initializer, symbol, settings.Resolution, settings.FillForward, settings.Leverage, settings.ExtendedMarketHours,
                False, False, algorithm.LiveMode, False, False)

    def Filter(self, filter):
        '''Defines the future chain universe filter'''
        # NOP
        return filter