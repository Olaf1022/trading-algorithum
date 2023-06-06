# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
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

from AlgorithmImports import *

### <summary>
### This algorithm demonstrate how to use OptionStrategies helper class to batch send orders for common strategies.
### In this case, the algorithm tests the Covered and Protective Call strategies.
### </summary>
class CoveredAndProtectiveCallStrategiesAlgorithm(QCAlgorithm):

    def Initialize(self):
        self.SetStartDate(2015, 12, 24)
        self.SetEndDate(2015, 12, 24)
        self.SetCash(1000000)

        option = self.AddOption("GOOG")
        self._option_symbol = option.Symbol

        option.SetFilter(-2, +2, 0, 180)

        self.SetBenchmark("GOOG")

    def OnData(self,slice):
        if not self.Portfolio.Invested:
            for kvp in slice.OptionChains:
                chain = kvp.Value
                contracts = sorted(sorted(chain, key = lambda x: abs(chain.Underlying.Price - x.Strike)),
                                   key = lambda x: x.Expiry, reverse=True)

                if len(contracts) == 0: continue
                contract = contracts[0]
                if contract != None:
                    self._covered_call = OptionStrategies.CoveredCall(self._option_symbol, contract.Strike, contract.Expiry)
                    self._protective_call = OptionStrategies.ProtectiveCall(self._option_symbol, contract.Strike, contract.Expiry)
                    self.Buy(self._covered_call, 2)
        else:
            # Verify that the strategy was traded
            positionGroup = list(self.Portfolio.Positions.Groups)[0]

            buyingPowerModel = positionGroup.BuyingPowerModel
            if not isinstance(buyingPowerModel, OptionStrategyPositionGroupBuyingPowerModel):
                raise Exception("Expected position group buying power model type: OptionStrategyPositionGroupBuyingPowerModel. "
                                f"Actual: {type(positionGroup.BuyingPowerModel).__name__}")

            positions = list(positionGroup.Positions)
            if len(positions) != 2:
                raise Exception(f"Expected position group to have 2 positions. Actual: {len(positions)}")

            optionPosition = [position for position in positions if position.Symbol.SecurityType == SecurityType.Option][0]
            underlyingPosition = [position for position in positions if position.Symbol.SecurityType == SecurityType.Equity][0]
            expectedOptionPositionQuantity = -2
            expectedUnderlyingPositionQuantity = 2 * self.Securities[self._option_symbol].SymbolProperties.ContractMultiplier

            if optionPosition.Quantity != expectedOptionPositionQuantity:
                raise Exception(f"Expected option position quantity to be {expectedOptionPositionQuantity}. Actual: {optionPosition.Quantity}")

            if underlyingPosition.Quantity != expectedUnderlyingPositionQuantity:
                raise Exception(f"Expected underlying position quantity to be {expectedUnderlyingPositionQuantity}. Actual: {underlyingPosition.Quantity}")

            # Now we should be able to close the position using the inverse strategy (a protective call)
            self.Buy(self._protective_call, 2);

            # We can quit now, no more testing required
            self.Quit();

    def OnEndOfAlgorithm(self):
        if self.Portfolio.Invested:
            raise Exception("Expected no holdings at end of algorithm")

        orders_count = len(list(self.Transactions.GetOrders(lambda order: order.Status == OrderStatus.Filled)))
        if orders_count != 4:
            raise Exception("Expected 4 orders to have been submitted and filled, 2 for buying the covered call and 2 for the liquidation. "
                            f"Actual {orders_count}")

    def OnOrderEvent(self, orderEvent):
        self.Debug(str(orderEvent))
