/*
 * CASCADELABS.IO
 * Cascade Labs LLC
 */

using System;
using QuantConnect.Orders;

namespace QuantConnect.Securities.PredictionMarket
{
    /// <summary>
    /// Buying power model for prediction market securities with cash-only (no margin), long-only trading.
    /// </summary>
    /// <remarks>
    /// Key characteristics:
    /// - Leverage is always 1 (no margin)
    /// - All tokens are long-only: buy YES or buy NO, no short positions
    /// - Buying at price P: requires P × quantity in cash (max loss = purchase price)
    /// - Selling is only allowed to close existing long positions
    /// </remarks>
    public class PredictionMarketBuyingPowerModel : BuyingPowerModel
    {
        /// <summary>
        /// Initializes a new instance with leverage = 1 (no margin)
        /// </summary>
        public PredictionMarketBuyingPowerModel()
            : base(1m, 0m)
        {
        }

        /// <summary>
        /// Gets the leverage for the security (always 1)
        /// </summary>
        /// <param name="security">The security</param>
        /// <returns>1 (no leverage)</returns>
        public override decimal GetLeverage(Security security)
        {
            return 1m;
        }

        /// <summary>
        /// Sets the leverage for the security. Throws if leverage is not 1.
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="leverage">The desired leverage (must be 1)</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            if (leverage != 1m)
            {
                throw new InvalidOperationException(
                    "Prediction market securities do not support leverage. Leverage must be 1.");
            }
        }

        /// <summary>
        /// Gets the initial margin requirement for an order.
        /// Long-only: margin = price × quantity.
        /// </summary>
        /// <param name="parameters">The parameters containing security and quantity</param>
        /// <returns>The initial margin requirement</returns>
        public override InitialMargin GetInitialMarginRequirement(InitialMarginParameters parameters)
        {
            var security = parameters.Security;

            // Long-only: margin = price × quantity
            return security.QuoteCurrency.ConversionRate
                * security.SymbolProperties.ContractMultiplier
                * security.Price
                * Math.Abs(parameters.Quantity);
        }

        /// <summary>
        /// Check if there is sufficient buying power to execute this order.
        /// Blocks any order that would result in a short position (long-only).
        /// </summary>
        /// <param name="parameters">The parameters for the check</param>
        /// <returns>Returns buying power information for the order</returns>
        public override HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(
            HasSufficientBuyingPowerForOrderParameters parameters)
        {
            var order = parameters.Order;
            var security = parameters.Security;
            var holdings = security.Holdings;

            // Short circuit: zero quantity orders always have sufficient buying power
            if (order.Quantity == 0)
            {
                return parameters.Sufficient();
            }

            // Block any order that would create a short position
            var newPosition = holdings.Quantity + order.Quantity;
            if (newPosition < 0)
            {
                return parameters.Insufficient(
                    $"Prediction market tokens are long-only. Order would result in short position of {newPosition}. " +
                    "To take a bearish position, buy the NO token instead of shorting the YES token.");
            }

            // Selling to close a long position: no additional buying power needed (already prepaid)
            if (order.Direction == OrderDirection.Sell)
            {
                return parameters.Sufficient();
            }

            // For buys, use the standard buying power check
            return base.HasSufficientBuyingPowerForOrder(parameters);
        }

        /// <summary>
        /// Gets the margin currently allocated to the specified holding.
        /// Prediction markets are fully prepaid — no ongoing margin is required.
        /// </summary>
        /// <param name="parameters">The parameters containing security and holdings info</param>
        /// <returns>Always 0: prepaid contracts have no maintenance margin</returns>
        public override MaintenanceMargin GetMaintenanceMargin(MaintenanceMarginParameters parameters)
        {
            return 0m;
        }

        /// <summary>
        /// Gets the reserved buying power for the specified position.
        /// Prediction markets are fully prepaid — no buying power is reserved against open positions.
        /// </summary>
        /// <param name="parameters">The parameters containing the security</param>
        /// <returns>Always 0: prepaid contracts reserve no buying power</returns>
        public override ReservedBuyingPowerForPosition GetReservedBuyingPowerForPosition(
            ReservedBuyingPowerForPositionParameters parameters)
        {
            return parameters.ResultInAccountCurrency(0m);
        }
    }
}
