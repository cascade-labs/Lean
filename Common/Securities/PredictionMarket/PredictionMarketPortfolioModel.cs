/*
 * CASCADELABS.IO
 * Cascade Labs LLC
 */

using QuantConnect.Logging;
using QuantConnect.Orders;

namespace QuantConnect.Securities.PredictionMarket
{
    /// <summary>
    /// Portfolio model for prediction market securities that handles binary settlement
    /// </summary>
    /// <remarks>
    /// Core settlement logic:
    /// - Detects settlement fills (order tag = "Liquidate from delisting")
    /// - Overrides fill.FillPrice with the binary settlement price ($1 for Yes, $0 for No)
    /// - Delegates to base.ProcessFill() for standard P&L and cash accounting
    ///
    /// P&L examples:
    /// - Bought at $0.60, result=Yes → profit = ($1.00 - $0.60) × quantity
    /// - Bought at $0.60, result=No → profit = ($0.00 - $0.60) × quantity (total loss)
    /// - Short at $0.60, result=Yes → loss = ($1.00 - $0.60) × quantity
    /// - Short at $0.60, result=No → profit = ($0.60 - $0.00) × quantity
    /// </remarks>
    public class PredictionMarketPortfolioModel : SecurityPortfolioModel
    {
        /// <summary>
        /// The order tag used to identify settlement/delisting liquidation orders
        /// </summary>
        public const string DelistingOrderTag = "Liquidate from delisting";

        /// <summary>
        /// Processes a fill for a prediction market security, handling binary settlement
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="fill">The order event fill object to be applied</param>
        public override void ProcessFill(SecurityPortfolioManager portfolio, Security security, OrderEvent fill)
        {
            // Check if this is a settlement fill (delisting liquidation)
            // Mutate FillPrice in-place so the same fill object flows to both
            // portfolio accounting AND TradeBuilder.ProcessFill() with the settlement price
            if (IsSettlementFill(fill))
            {
                var predictionMarket = security as PredictionMarket;
                if (predictionMarket != null)
                {
                    fill.FillPrice = GetSettlementPrice(predictionMarket);
                }
            }

            base.ProcessFill(portfolio, security, fill);
        }

        /// <summary>
        /// Determines if an order event represents a settlement fill
        /// </summary>
        /// <param name="fill">The order event to check</param>
        /// <returns>True if this is a settlement/delisting liquidation fill</returns>
        private static bool IsSettlementFill(OrderEvent fill)
        {
            // Check if the order ticket has the delisting tag
            return fill.Ticket?.Tag == DelistingOrderTag;
        }

        /// <summary>
        /// Gets the binary settlement price based on the market's settlement result and token type.
        /// YES tokens pay $1 when result is Yes, NO tokens pay $1 when result is No.
        /// </summary>
        /// <param name="predictionMarket">The prediction market security</param>
        /// <returns>Settlement price ($0 or $1), or last market price if Pending</returns>
        private static decimal GetSettlementPrice(PredictionMarket predictionMarket)
        {
            var result = predictionMarket.SettlementResult;
            var isNoToken = predictionMarket.Symbol.ID.TokenType == PredictionMarketTokenType.No;

            switch (result)
            {
                case PredictionMarketSettlementResult.Yes:
                    return isNoToken ? 0.0m : 1.0m;

                case PredictionMarketSettlementResult.No:
                    return isNoToken ? 1.0m : 0.0m;

                case PredictionMarketSettlementResult.Pending:
                default:
                    // Fallback to last market price with error log
                    Log.Error($"PredictionMarketPortfolioModel.GetSettlementPrice(): " +
                        $"Settlement result is Pending for {predictionMarket.Symbol}. " +
                        $"Falling back to last market price: {predictionMarket.Price}");
                    return predictionMarket.Price;
            }
        }
    }
}
