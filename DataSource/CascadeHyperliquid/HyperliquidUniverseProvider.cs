/*
 * Cascade Labs - Hyperliquid Universe Provider
 *
 * Provides lists of available trading pairs on Hyperliquid DEX
 * for use in LEAN universe selection and coarse filters.
 */

using Newtonsoft.Json.Linq;
using QuantConnect.Logging;

namespace QuantConnect.Lean.DataSource.CascadeHyperliquid
{
    /// <summary>
    /// Represents metadata for a Hyperliquid perpetual futures asset
    /// </summary>
    public class HyperliquidPerpAsset
    {
        /// <summary>
        /// Asset name (e.g., "BTC", "ETH", "SOL")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of decimal places for size
        /// </summary>
        public int SzDecimals { get; set; }

        /// <summary>
        /// Maximum leverage allowed
        /// </summary>
        public int MaxLeverage { get; set; }

        /// <summary>
        /// Whether the asset only allows isolated margin
        /// </summary>
        public bool OnlyIsolated { get; set; }

        /// <summary>
        /// Whether the asset is delisted
        /// </summary>
        public bool IsDelisted { get; set; }

        /// <summary>
        /// LEAN-compatible symbol (e.g., "BTCUSD" for perpetuals)
        /// </summary>
        public string LeanSymbol => $"{Name}USD";
    }

    /// <summary>
    /// Represents metadata for a Hyperliquid spot trading pair
    /// </summary>
    public class HyperliquidSpotAsset
    {
        /// <summary>
        /// Spot pair index (used for API calls as @{index})
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Base token name (e.g., "HYPE", "UBTC")
        /// </summary>
        public string BaseToken { get; set; } = string.Empty;

        /// <summary>
        /// Quote token name (e.g., "USDC")
        /// </summary>
        public string QuoteToken { get; set; } = string.Empty;

        /// <summary>
        /// Base token index
        /// </summary>
        public int BaseTokenIndex { get; set; }

        /// <summary>
        /// Quote token index
        /// </summary>
        public int QuoteTokenIndex { get; set; }

        /// <summary>
        /// Whether this is a canonical pair (uses name like "PURR/USDC" for API)
        /// </summary>
        public bool IsCanonical { get; set; }

        /// <summary>
        /// Display name (e.g., "HYPE/USDC")
        /// </summary>
        public string DisplayName => $"{BaseToken}/{QuoteToken}";

        /// <summary>
        /// API identifier - use name for canonical, @index for others
        /// </summary>
        public string ApiIdentifier => IsCanonical ? DisplayName : $"@{Index}";

        /// <summary>
        /// LEAN-compatible symbol (e.g., "HYPEUSDC" for spot)
        /// </summary>
        public string LeanSymbol => $"{BaseToken}{QuoteToken}";
    }

    /// <summary>
    /// Provides access to the Hyperliquid trading universe
    /// </summary>
    public class HyperliquidUniverseProvider : IDisposable
    {
        private readonly HyperliquidRestClient _client;
        private List<HyperliquidPerpAsset>? _perpAssets;
        private List<HyperliquidSpotAsset>? _spotAssets;
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// Creates a new universe provider
        /// </summary>
        /// <param name="testnet">If true, uses testnet API</param>
        public HyperliquidUniverseProvider(bool testnet = false)
        {
            _client = new HyperliquidRestClient(testnet);
        }

        /// <summary>
        /// Creates a new universe provider with an existing REST client
        /// </summary>
        public HyperliquidUniverseProvider(HyperliquidRestClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Gets all available perpetual futures assets
        /// </summary>
        /// <param name="includeDelisted">If true, includes delisted assets</param>
        /// <returns>List of perpetual assets</returns>
        public async Task<List<HyperliquidPerpAsset>> GetPerpAssetsAsync(bool includeDelisted = false)
        {
            await RefreshPerpAssetsIfNeededAsync().ConfigureAwait(false);

            lock (_lock)
            {
                if (_perpAssets == null) return new List<HyperliquidPerpAsset>();

                return includeDelisted
                    ? _perpAssets.ToList()
                    : _perpAssets.Where(a => !a.IsDelisted).ToList();
            }
        }

        /// <summary>
        /// Gets all available spot trading pairs
        /// </summary>
        /// <param name="quoteFilter">Optional filter for quote token (e.g., "USDC")</param>
        /// <returns>List of spot pairs</returns>
        public async Task<List<HyperliquidSpotAsset>> GetSpotAssetsAsync(string? quoteFilter = null)
        {
            await RefreshSpotAssetsIfNeededAsync().ConfigureAwait(false);

            lock (_lock)
            {
                if (_spotAssets == null) return new List<HyperliquidSpotAsset>();

                var result = _spotAssets.AsEnumerable();

                if (!string.IsNullOrEmpty(quoteFilter))
                {
                    result = result.Where(a => a.QuoteToken.Equals(quoteFilter, StringComparison.OrdinalIgnoreCase));
                }

                return result.ToList();
            }
        }

        /// <summary>
        /// Gets LEAN-compatible symbols for all perpetual futures
        /// </summary>
        /// <param name="includeDelisted">If true, includes delisted assets</param>
        /// <returns>List of LEAN symbols like ["BTCUSD", "ETHUSD", ...]</returns>
        public async Task<List<string>> GetPerpSymbolsAsync(bool includeDelisted = false)
        {
            var assets = await GetPerpAssetsAsync(includeDelisted).ConfigureAwait(false);
            return assets.Select(a => a.LeanSymbol).ToList();
        }

        /// <summary>
        /// Gets LEAN-compatible symbols for all spot pairs
        /// </summary>
        /// <param name="quoteFilter">Optional filter for quote token (e.g., "USDC")</param>
        /// <returns>List of LEAN symbols like ["HYPEUSDC", "UBTCUSDC", ...]</returns>
        public async Task<List<string>> GetSpotSymbolsAsync(string? quoteFilter = null)
        {
            var assets = await GetSpotAssetsAsync(quoteFilter).ConfigureAwait(false);
            return assets.Select(a => a.LeanSymbol).ToList();
        }

        /// <summary>
        /// Gets the top perpetual assets by max leverage (major coins typically have highest leverage)
        /// </summary>
        /// <param name="count">Number of assets to return</param>
        /// <returns>Top assets sorted by leverage descending</returns>
        public async Task<List<HyperliquidPerpAsset>> GetTopPerpsByLeverageAsync(int count = 20)
        {
            var assets = await GetPerpAssetsAsync(false).ConfigureAwait(false);
            return assets
                .OrderByDescending(a => a.MaxLeverage)
                .ThenBy(a => a.Name)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Gets popular/major spot pairs (wrapped assets like UBTC, UETH, plus HYPE)
        /// </summary>
        /// <returns>List of major spot pairs</returns>
        public async Task<List<HyperliquidSpotAsset>> GetMajorSpotPairsAsync()
        {
            var assets = await GetSpotAssetsAsync("USDC").ConfigureAwait(false);

            // Major tokens typically start with U (wrapped) or are HYPE
            var majorTokenPrefixes = new[] { "UBTC", "UETH", "USOL", "HYPE", "PURR" };

            return assets
                .Where(a => majorTokenPrefixes.Any(p =>
                    a.BaseToken.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    a.BaseToken.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Refreshes perpetual assets from the API
        /// </summary>
        public async Task RefreshPerpAssetsAsync()
        {
            var meta = await _client.GetMetaAsync().ConfigureAwait(false);
            if (meta == null)
            {
                Log.Error("HyperliquidUniverseProvider: Failed to fetch perpetuals metadata");
                return;
            }

            var universe = meta["universe"] as JArray;
            if (universe == null)
            {
                Log.Error("HyperliquidUniverseProvider: No universe found in metadata");
                return;
            }

            var assets = new List<HyperliquidPerpAsset>();

            foreach (var item in universe)
            {
                var asset = new HyperliquidPerpAsset
                {
                    Name = item["name"]?.ToString() ?? string.Empty,
                    SzDecimals = item["szDecimals"]?.Value<int>() ?? 0,
                    MaxLeverage = item["maxLeverage"]?.Value<int>() ?? 1,
                    OnlyIsolated = item["onlyIsolated"]?.Value<bool>() ?? false,
                    IsDelisted = item["isDelisted"]?.Value<bool>() ?? false
                };

                if (!string.IsNullOrEmpty(asset.Name))
                {
                    assets.Add(asset);
                }
            }

            lock (_lock)
            {
                _perpAssets = assets;
            }

            Log.Trace($"HyperliquidUniverseProvider: Loaded {assets.Count} perpetual assets");
        }

        /// <summary>
        /// Refreshes spot assets from the API
        /// </summary>
        public async Task RefreshSpotAssetsAsync()
        {
            var spotMeta = await _client.GetSpotMetaAsync().ConfigureAwait(false);
            if (spotMeta == null)
            {
                Log.Error("HyperliquidUniverseProvider: Failed to fetch spot metadata");
                return;
            }

            var tokens = spotMeta["tokens"] as JArray;
            var universe = spotMeta["universe"] as JArray;

            if (tokens == null || universe == null)
            {
                Log.Error("HyperliquidUniverseProvider: No tokens or universe found in spot metadata");
                return;
            }

            // Build token index lookup
            var tokenLookup = new Dictionary<int, string>();
            foreach (var token in tokens)
            {
                var index = token["index"]?.Value<int>() ?? -1;
                var name = token["name"]?.ToString() ?? string.Empty;
                if (index >= 0 && !string.IsNullOrEmpty(name))
                {
                    tokenLookup[index] = name;
                }
            }

            var assets = new List<HyperliquidSpotAsset>();

            foreach (var item in universe)
            {
                var tokensArray = item["tokens"] as JArray;
                if (tokensArray == null || tokensArray.Count < 2) continue;

                var baseIndex = tokensArray[0].Value<int>();
                var quoteIndex = tokensArray[1].Value<int>();

                if (!tokenLookup.TryGetValue(baseIndex, out var baseName) ||
                    !tokenLookup.TryGetValue(quoteIndex, out var quoteName))
                {
                    continue;
                }

                var asset = new HyperliquidSpotAsset
                {
                    Index = item["index"]?.Value<int>() ?? 0,
                    BaseToken = baseName,
                    QuoteToken = quoteName,
                    BaseTokenIndex = baseIndex,
                    QuoteTokenIndex = quoteIndex,
                    IsCanonical = item["isCanonical"]?.Value<bool>() ?? false
                };

                assets.Add(asset);
            }

            lock (_lock)
            {
                _spotAssets = assets;
            }

            Log.Trace($"HyperliquidUniverseProvider: Loaded {assets.Count} spot pairs");
        }

        private async Task RefreshPerpAssetsIfNeededAsync()
        {
            lock (_lock)
            {
                if (_perpAssets != null) return;
            }
            await RefreshPerpAssetsAsync().ConfigureAwait(false);
        }

        private async Task RefreshSpotAssetsIfNeededAsync()
        {
            lock (_lock)
            {
                if (_spotAssets != null) return;
            }
            await RefreshSpotAssetsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Force refresh all cached data
        /// </summary>
        public async Task RefreshAllAsync()
        {
            await RefreshPerpAssetsAsync().ConfigureAwait(false);
            await RefreshSpotAssetsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Prints a summary of available assets to the log
        /// </summary>
        public async Task PrintSummaryAsync()
        {
            var perps = await GetPerpAssetsAsync(false).ConfigureAwait(false);
            var spots = await GetSpotAssetsAsync("USDC").ConfigureAwait(false);
            var majorSpots = await GetMajorSpotPairsAsync().ConfigureAwait(false);

            Log.Trace($"=== Hyperliquid Universe Summary ===");
            Log.Trace($"Perpetual Futures: {perps.Count} active assets");
            Log.Trace($"Spot Pairs (USDC): {spots.Count} pairs");
            Log.Trace($"Major Spot Pairs: {majorSpots.Count} pairs");

            Log.Trace($"\nTop 10 Perps by Leverage:");
            var topPerps = await GetTopPerpsByLeverageAsync(10).ConfigureAwait(false);
            foreach (var p in topPerps)
            {
                Log.Trace($"  {p.LeanSymbol} (leverage: {p.MaxLeverage}x)");
            }

            Log.Trace($"\nMajor Spot Pairs:");
            foreach (var s in majorSpots)
            {
                Log.Trace($"  {s.LeanSymbol} (API: {s.ApiIdentifier})");
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            // Don't dispose the client if it was passed in externally
            _disposed = true;
        }
    }
}
