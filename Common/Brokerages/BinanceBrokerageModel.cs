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
using QuantConnect.Util;
using QuantConnect.Orders;
using QuantConnect.Benchmarks;
using QuantConnect.Securities;
using QuantConnect.Orders.Fees;
using System.Collections.Generic;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides Binance specific properties
    /// </summary>
    public class BinanceBrokerageModel : DefaultBrokerageModel
    {
        private const decimal _defaultLeverage = 3;
        private const decimal _defaultFutureLeverage = 25;

        /// <summary>
        /// Market name
        /// </summary>
        protected virtual string MarketName => Market.Binance;

        /// <summary>
        /// Gets a map of the default markets to be used for each security type
        /// </summary>
        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets { get; } = GetDefaultMarkets(Market.Binance);

        /// <summary>
        /// Initializes a new instance of the <see cref="BinanceBrokerageModel"/> class
        /// </summary>
        /// <param name="accountType">The type of account to be modeled, defaults to <see cref="AccountType.Cash"/></param>
        public BinanceBrokerageModel(AccountType accountType = AccountType.Cash) : base(accountType)
        {
        }

        /// <summary>
        /// Binance global leverage rule
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override decimal GetLeverage(Security security)
        {
            if (AccountType == AccountType.Cash || security.IsInternalFeed() || security.Type == SecurityType.Base)
            {
                return 1m;
            }

            return security.Symbol.SecurityType == SecurityType.CryptoFuture ? _defaultFutureLeverage : _defaultLeverage;
        }

        /// <summary>
        /// Get the benchmark for this model
        /// </summary>
        /// <param name="securities">SecurityService to create the security with if needed</param>
        /// <returns>The benchmark for this brokerage</returns>
        public override IBenchmark GetBenchmark(SecurityManager securities)
        {
            var symbol = Symbol.Create("BTCUSDC", SecurityType.Crypto, MarketName);
            return SecurityBenchmark.CreateInstance(securities, symbol);
        }

        /// <summary>
        /// Provides Binance fee model
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            return new BinanceFeeModel();
        }

        /// <summary>
        /// Binance does not support update of orders
        /// </summary>
        /// <param name="security">The security of the order</param>
        /// <param name="order">The order to be updated</param>
        /// <param name="request">The requested update to be made to the order</param>
        /// <param name="message">If this function returns false, a brokerage message detailing why the order may not be updated</param>
        /// <returns>Binance does not support update of orders, so it will always return false</returns>
        public override bool CanUpdateOrder(Security security, Order order, UpdateOrderRequest request, out BrokerageMessageEvent message)
        {
            message = new BrokerageMessageEvent(BrokerageMessageType.Warning, 0, "Brokerage does not support update. You must cancel and re-create instead."); ;
            return false;
        }

        /// <summary>
        /// Returns true if the brokerage could accept this order. This takes into account
        /// order type, security type, and order size limits.
        /// </summary>
        /// <remarks>
        /// For example, a brokerage may have no connectivity at certain times, or an order rate/size limit
        /// </remarks>
        /// <param name="security">The security of the order</param>
        /// <param name="order">The order to be processed</param>
        /// <param name="message">If this function returns false, a brokerage message detailing why the order may not be submitted</param>
        /// <returns>True if the brokerage could process the order, false otherwise</returns>
        public override bool CanSubmitOrder(Security security, Order order, out BrokerageMessageEvent message)
        {
            message = null;

            // Binance API provides minimum order size in quote currency
            // and hence we have to check current order size using available price and order quantity
            var quantityIsValid = true;
            switch (order)
            {
                case LimitOrder limitOrder:
                    quantityIsValid &= IsOrderSizeLargeEnough(limitOrder.LimitPrice);
                    break;
                case MarketOrder:
                    if (!security.HasData)
                    {
                        message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported",
                            "There is no data for this symbol yet, please check the security.HasData flag to ensure there is at least one data point."
                        );

                        return false;
                    }

                    var price = order.Direction == OrderDirection.Buy ? security.AskPrice : security.BidPrice;
                    quantityIsValid &= IsOrderSizeLargeEnough(price);
                    break;
                case StopLimitOrder stopLimitOrder:
                    if (security.Symbol.SecurityType == SecurityType.CryptoFuture)
                    {
                        message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported", Invariant($"{order.Type} orders are not supported for this symbol ${security.Symbol}"));
                        return false;
                    }
                    quantityIsValid &= IsOrderSizeLargeEnough(stopLimitOrder.LimitPrice);
                    // Binance Trading UI requires this check too...
                    quantityIsValid &= IsOrderSizeLargeEnough(stopLimitOrder.StopPrice);
                    break;
                case StopMarketOrder:
                    // despite Binance API allows you to post STOP_LOSS and TAKE_PROFIT order types
                    // they always fails with the content
                    // {"code":-1013,"msg":"Take profit orders are not supported for this symbol."}
                    // currently no symbols supporting TAKE_PROFIT or STOP_LOSS orders

                    message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported",
                        Invariant($"{order.Type} orders are not supported for this symbol. Please check 'https://api.binance.com/api/v3/exchangeInfo?symbol={security.SymbolProperties.MarketTicker}' to see supported order types.")
                    );
                    return false;
                default:
                    message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported",
                        Invariant($"{order.Type} orders are not supported by Binance.")
                    );
                    return false;
            }


            if (!quantityIsValid)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported",
                    Invariant($"The minimum order size (in quote currency) for {security.Symbol.Value} is {security.SymbolProperties.MinimumOrderSize}. Order quantity was {order.Quantity}.")
                );

                return false;
            }

            if (security.Type != SecurityType.Crypto && security.Type != SecurityType.CryptoFuture)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "NotSupported",
                    Invariant($"The {nameof(BinanceBrokerageModel)} does not support {security.Type} security type.")
                );

                return false;
            }
            return base.CanSubmitOrder(security, order, out message);

            bool IsOrderSizeLargeEnough(decimal price) =>
                // if we have a minimum order size we enforce it
                !security.SymbolProperties.MinimumOrderSize.HasValue || order.AbsoluteQuantity * price > security.SymbolProperties.MinimumOrderSize;
        }

        protected static IReadOnlyDictionary<SecurityType, string> GetDefaultMarkets(string marketName)
        {
            var map = DefaultMarketMap.ToDictionary();
            map[SecurityType.Crypto] = marketName;
            return map.ToReadOnlyDictionary();
        }
    }
}
