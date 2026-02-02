/*
 * Cascade Labs - ThetaData Symbol Mapper
 */

using QuantConnect.Brokerages;

namespace QuantConnect.Lean.DataSource.CascadeThetaData
{
    /// <summary>
    /// Maps between LEAN symbols and ThetaData symbols
    /// </summary>
    public class ThetaDataSymbolMapper : ISymbolMapper
    {
        /// <summary>
        /// Security types supported by ThetaData
        /// </summary>
        public HashSet<SecurityType> SupportedSecurityType { get; } = new()
        {
            SecurityType.Equity,
            SecurityType.Index,
            SecurityType.Option,
            SecurityType.IndexOption
        };

        /// <summary>
        /// Converts a LEAN symbol to ThetaData format
        /// </summary>
        /// <param name="symbol">LEAN symbol</param>
        /// <returns>ThetaData symbol string</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || symbol.Value == null)
            {
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null");
            }

            switch (symbol.SecurityType)
            {
                case SecurityType.Equity:
                case SecurityType.Index:
                    return symbol.Value.ToUpperInvariant();

                case SecurityType.Option:
                case SecurityType.IndexOption:
                    // Format: ROOT,EXPIRY,STRIKE,RIGHT
                    // EXPIRY as YYYYMMDD, STRIKE as integer (multiplied by 1000)
                    var underlying = symbol.Underlying?.Value ?? symbol.ID.Symbol;
                    var expiry = symbol.ID.Date.ToString("yyyyMMdd");
                    var strike = ((int)(symbol.ID.StrikePrice * 1000)).ToString();
                    var right = symbol.ID.OptionRight == OptionRight.Call ? "C" : "P";
                    return $"{underlying.ToUpperInvariant()},{expiry},{strike},{right}";

                default:
                    throw new NotSupportedException($"Security type {symbol.SecurityType} is not supported by ThetaData");
            }
        }

        /// <summary>
        /// Converts a ThetaData symbol to LEAN format
        /// </summary>
        /// <param name="brokerageSymbol">ThetaData symbol string</param>
        /// <param name="securityType">Security type</param>
        /// <param name="market">Market</param>
        /// <param name="expirationDate">Option expiration date (if applicable)</param>
        /// <param name="strike">Option strike price (if applicable)</param>
        /// <param name="optionRight">Option right (if applicable)</param>
        /// <returns>LEAN symbol</returns>
        public Symbol GetLeanSymbol(
            string brokerageSymbol,
            SecurityType securityType,
            string market,
            DateTime expirationDate = default,
            decimal strike = 0,
            OptionRight optionRight = OptionRight.Call)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                throw new ArgumentException("Brokerage symbol cannot be null or empty", nameof(brokerageSymbol));
            }

            switch (securityType)
            {
                case SecurityType.Equity:
                    return Symbol.Create(brokerageSymbol, SecurityType.Equity, market);

                case SecurityType.Index:
                    return Symbol.Create(brokerageSymbol, SecurityType.Index, market);

                case SecurityType.Option:
                    var underlying = Symbol.Create(brokerageSymbol, SecurityType.Equity, market);
                    return Symbol.CreateOption(underlying, market, OptionStyle.American, optionRight, strike, expirationDate);

                case SecurityType.IndexOption:
                    var indexUnderlying = Symbol.Create(brokerageSymbol, SecurityType.Index, market);
                    return Symbol.CreateOption(indexUnderlying, market, OptionStyle.European, optionRight, strike, expirationDate);

                default:
                    throw new NotSupportedException($"Security type {securityType} is not supported by ThetaData");
            }
        }
    }
}
