/*
 * Cascade Labs - Hyperliquid Symbol Mapper
 *
 * Maps between LEAN symbols and Hyperliquid coin symbols
 * 
 * Perpetuals: Use simple coin names like "BTC", "ETH", "SOL"
 * Spot: Use @{index} format for API (e.g., @107 for HYPE/USDC)
 * 
 * All perpetuals are settled in USDC
 */

using QuantConnect;
using QuantConnect.Brokerages;

namespace QuantConnect.Lean.DataSource.CascadeHyperliquid
{
    /// <summary>
    /// Symbol mapper for Hyperliquid perpetual futures and spot
    /// </summary>
    /// <remarks>
    /// Hyperliquid perpetuals:
    /// - Use simple coin names: "BTC", "ETH", "SOL"
    /// - All settled in USDC
    /// - LEAN representation: BTCUSD, ETHUSD, etc. with SecurityType.CryptoFuture
    /// 
    /// Hyperliquid spot:
    /// - Use @{index} format: @107 for HYPE/USDC, @142 for UBTC/USDC
    /// - PURR/USDC is canonical and uses "PURR/USDC" format
    /// - LEAN representation: HYPEUSD, PURRUSD, etc. with SecurityType.Crypto
    /// </remarks>
    public class HyperliquidSymbolMapper : ISymbolMapper
    {
        /// <summary>
        /// Supported security types for Hyperliquid
        /// </summary>
        public readonly HashSet<SecurityType> SupportedSecurityTypes = new()
        {
            SecurityType.CryptoFuture,
            SecurityType.Crypto
        };

        /// <summary>
        /// Mapping from LEAN spot ticker (base currency) to Hyperliquid API spot name
        /// These are the primary USDC-quoted spot pairs
        /// </summary>
        /// <remarks>
        /// Spot index mapping (from spotMeta API):
        /// - PURR/USDC = "PURR/USDC" (canonical, index 0)
        /// - HYPE/USDC = @107
        /// - UBTC/USDC = @142 (Unit Bitcoin, same price as BTC)
        /// - UETH/USDC = @151 (Unit Ethereum, same price as ETH)  
        /// - USOL/USDC = @156 (Unit Solana, same price as SOL)
        /// 
        /// Note: Use HyperliquidUniverseProvider.GetSpotAssetsAsync() for complete dynamic list
        /// </remarks>
        private static readonly Dictionary<string, string> SpotTickerToApiName = new(StringComparer.OrdinalIgnoreCase)
        {
            // Common/major spot pairs - see HyperliquidUniverseProvider for full dynamic list
            { "PURR", "PURR/USDC" },  // Canonical pair (index 0)
            { "HYPE", "@107" },       // HYPE/USDC
            { "UBTC", "@142" },       // Unit Bitcoin (same price as BTC)
            { "UETH", "@151" },       // Unit Ethereum (same price as ETH)
            { "USOL", "@156" },       // Unit Solana (same price as SOL)
            // Additional popular spot pairs
            { "TRUMP", "@9" },        // TRUMP/USDC
            { "PEPE", "@11" },        // PEPE/USDC (different from kPEPE perp)
            { "MOG", "@43" },         // MOG/USDC
        };

        /// <summary>
        /// Reverse mapping from Hyperliquid API name to LEAN base ticker
        /// </summary>
        private static readonly Dictionary<string, string> ApiNameToSpotTicker = 
            SpotTickerToApiName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Converts a LEAN symbol to Hyperliquid coin format
        /// </summary>
        /// <param name="symbol">LEAN symbol</param>
        /// <returns>Hyperliquid coin symbol (e.g., "BTC" for perps, "@107" for spot)</returns>
        /// <exception cref="ArgumentException">If symbol is not supported</exception>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrEmpty(symbol.Value))
            {
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty");
            }

            if (!SupportedSecurityTypes.Contains(symbol.SecurityType))
            {
                throw new ArgumentException(
                    $"Hyperliquid only supports CryptoFuture and Crypto, but received {symbol.SecurityType}",
                    nameof(symbol));
            }

            if (!string.Equals(symbol.ID.Market, CascadeMarkets.Hyperliquid, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Hyperliquid only supports market '{CascadeMarkets.Hyperliquid}', but received '{symbol.ID.Market}'",
                    nameof(symbol));
            }

            var ticker = symbol.Value;

            // Handle common quote currency suffixes to extract base currency
            var quoteSuffixes = new[] { "USDC", "USD", "PERP" };
            string baseCurrency = ticker;
            foreach (var suffix in quoteSuffixes)
            {
                if (ticker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    baseCurrency = ticker.Substring(0, ticker.Length - suffix.Length);
                    break;
                }
            }

            // For spot (Crypto), use the spot mapping
            if (symbol.SecurityType == SecurityType.Crypto)
            {
                if (SpotTickerToApiName.TryGetValue(baseCurrency, out var apiName))
                {
                    return apiName;
                }
                throw new ArgumentException(
                    $"Unknown spot symbol '{baseCurrency}'. Supported: {string.Join(", ", SpotTickerToApiName.Keys)}",
                    nameof(symbol));
            }

            // For perpetuals (CryptoFuture), return just the base currency
            // LEAN format: BTCUSD -> Hyperliquid format: BTC
            return baseCurrency;
        }

        /// <summary>
        /// Converts a Hyperliquid coin symbol to a LEAN symbol
        /// </summary>
        /// <param name="brokerageSymbol">Hyperliquid coin symbol (e.g., "BTC" for perps, "@107" for spot)</param>
        /// <param name="securityType">LEAN security type</param>
        /// <param name="market">Market identifier</param>
        /// <param name="expirationDate">Expiration date (not used for perpetuals)</param>
        /// <param name="strike">Strike price (not used)</param>
        /// <param name="optionRight">Option right (not used)</param>
        /// <returns>LEAN Symbol object</returns>
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
                throw new ArgumentNullException(nameof(brokerageSymbol), "Brokerage symbol cannot be null or empty");
            }

            if (!SupportedSecurityTypes.Contains(securityType))
            {
                throw new ArgumentException(
                    $"Hyperliquid only supports CryptoFuture and Crypto, but received {securityType}",
                    nameof(securityType));
            }

            if (!string.Equals(market, CascadeMarkets.Hyperliquid, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Hyperliquid only supports market '{CascadeMarkets.Hyperliquid}', but received '{market}'",
                    nameof(market));
            }

            string baseCurrency;

            // For spot (Crypto), map from API name back to LEAN ticker
            if (securityType == SecurityType.Crypto)
            {
                if (ApiNameToSpotTicker.TryGetValue(brokerageSymbol, out var ticker))
                {
                    baseCurrency = ticker;
                }
                else
                {
                    throw new ArgumentException(
                        $"Unknown spot API name '{brokerageSymbol}'. Supported: {string.Join(", ", ApiNameToSpotTicker.Keys)}",
                        nameof(brokerageSymbol));
                }
            }
            else
            {
                // For perpetuals, the brokerage symbol is the base currency
                baseCurrency = brokerageSymbol;
            }

            // Convert to LEAN format: BTC -> BTCUSD
            var leanTicker = $"{baseCurrency.ToUpperInvariant()}USD";

            return Symbol.Create(leanTicker, securityType, market);
        }

        /// <summary>
        /// Validates if a symbol is supported by Hyperliquid
        /// </summary>
        /// <param name="symbol">Symbol to validate</param>
        /// <returns>True if the symbol is supported</returns>
        public bool IsSymbolSupported(Symbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            if (!SupportedSecurityTypes.Contains(symbol.SecurityType))
            {
                return false;
            }

            if (!string.Equals(symbol.ID.Market, CascadeMarkets.Hyperliquid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // For spot, verify the base currency is in our supported list
            if (symbol.SecurityType == SecurityType.Crypto)
            {
                var ticker = symbol.Value;
                var quoteSuffixes = new[] { "USDC", "USD" };
                foreach (var suffix in quoteSuffixes)
                {
                    if (ticker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        var baseCurrency = ticker.Substring(0, ticker.Length - suffix.Length);
                        return SpotTickerToApiName.ContainsKey(baseCurrency);
                    }
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a symbol is a spot (Crypto) symbol
        /// </summary>
        /// <param name="symbol">Symbol to check</param>
        /// <returns>True if this is a spot symbol</returns>
        public bool IsSpot(Symbol symbol)
        {
            return symbol?.SecurityType == SecurityType.Crypto;
        }

        /// <summary>
        /// Gets the quote currency for Hyperliquid (both perpetuals and spot)
        /// </summary>
        /// <returns>Quote currency (USDC)</returns>
        public static string GetQuoteCurrency()
        {
            return "USDC";
        }

        /// <summary>
        /// Gets all supported spot tickers
        /// </summary>
        /// <returns>Collection of supported spot base currencies</returns>
        public static IEnumerable<string> GetSupportedSpotTickers()
        {
            return SpotTickerToApiName.Keys;
        }

        /// <summary>
        /// Gets the Hyperliquid API name for a spot ticker
        /// </summary>
        /// <param name="baseTicker">Base currency ticker (e.g., "HYPE")</param>
        /// <returns>Hyperliquid API name (e.g., "@107") or null if not found</returns>
        public static string? GetSpotApiName(string baseTicker)
        {
            return SpotTickerToApiName.TryGetValue(baseTicker, out var apiName) ? apiName : null;
        }
    }
}
