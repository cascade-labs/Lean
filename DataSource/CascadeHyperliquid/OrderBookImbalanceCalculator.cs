/*
 * Cascade Labs - Order Book Imbalance Calculator
 *
 * Calculates order book imbalance (OBI) signal from Hyperliquid L2 data.
 * Designed to integrate with multi-factor alpha models.
 *
 * OBI ranges from -1 (all sell pressure) to +1 (all buy pressure)
 */

using Newtonsoft.Json.Linq;
using QuantConnect;
using QuantConnect.Logging;

namespace QuantConnect.Lean.DataSource.CascadeHyperliquid
{
    /// <summary>
    /// Calculates order book imbalance from Hyperliquid L2 data
    /// </summary>
    public class OrderBookImbalanceCalculator : IDisposable
    {
        private readonly HyperliquidRestClient _client;
        private readonly int _levels;
        private readonly bool _useWeighting;
        private readonly bool _ownsClient;
        
        // Cache to avoid excessive API calls
        private readonly Dictionary<string, (DateTime time, double obi, JObject rawBook)> _cache = new();
        private readonly TimeSpan _cacheExpiry;
        
        /// <summary>
        /// Creates a new OrderBookImbalanceCalculator
        /// </summary>
        /// <param name="client">Hyperliquid REST client (creates new if null)</param>
        /// <param name="levels">Number of book levels to consider (1-20)</param>
        /// <param name="useWeighting">Use depth-weighted calculation</param>
        /// <param name="cacheExpirySeconds">How long to cache results</param>
        public OrderBookImbalanceCalculator(
            HyperliquidRestClient? client = null,
            int levels = 5,
            bool useWeighting = true,
            double cacheExpirySeconds = 2.0)
        {
            _ownsClient = client == null;
            _client = client ?? new HyperliquidRestClient();
            _levels = Math.Clamp(levels, 1, 20);
            _useWeighting = useWeighting;
            _cacheExpiry = TimeSpan.FromSeconds(cacheExpirySeconds);
        }
        
        /// <summary>
        /// Calculate OBI for a coin symbol
        /// </summary>
        /// <param name="coin">Hyperliquid coin symbol (e.g., "BTC", "ETH")</param>
        /// <returns>OBI value from -1 to +1, or null if unavailable</returns>
        public double? Calculate(string coin)
        {
            // Check cache
            if (_cache.TryGetValue(coin, out var cached) && 
                DateTime.UtcNow - cached.time < _cacheExpiry)
            {
                return cached.obi;
            }
            
            try
            {
                var book = _client.GetL2BookAsync(coin).GetAwaiter().GetResult();
                if (book == null)
                {
                    Log.Trace($"OrderBookImbalanceCalculator: No book data for {coin}");
                    return null;
                }
                
                var levels = book["levels"] as JArray;
                if (levels == null || levels.Count < 2)
                {
                    Log.Trace($"OrderBookImbalanceCalculator: Invalid book structure for {coin}");
                    return null;
                }
                
                var bids = ParseLevels(levels[0]);
                var asks = ParseLevels(levels[1]);
                
                var obi = _useWeighting 
                    ? CalculateWeightedOBI(bids, asks)
                    : CalculateSimpleOBI(bids, asks);
                
                _cache[coin] = (DateTime.UtcNow, obi, book);
                
                Log.Debug($"OrderBookImbalanceCalculator: {coin} OBI={obi:F4} " +
                         $"(bids={bids.Count}, asks={asks.Count})");
                
                return obi;
            }
            catch (Exception ex)
            {
                Log.Error($"OrderBookImbalanceCalculator: Error for {coin}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Calculate OBI from a LEAN Symbol
        /// </summary>
        public double? Calculate(Symbol symbol)
        {
            var coin = ExtractCoin(symbol);
            return Calculate(coin);
        }
        
        /// <summary>
        /// Get the raw order book (from cache or fresh)
        /// </summary>
        public JObject? GetOrderBook(string coin)
        {
            // Try cache first
            if (_cache.TryGetValue(coin, out var cached) && 
                DateTime.UtcNow - cached.time < _cacheExpiry)
            {
                return cached.rawBook;
            }
            
            // Fetch fresh
            Calculate(coin);
            
            return _cache.TryGetValue(coin, out var result) ? result.rawBook : null;
        }
        
        /// <summary>
        /// Get detailed OBI breakdown
        /// </summary>
        public ObiBreakdown? GetBreakdown(string coin)
        {
            var book = GetOrderBook(coin);
            if (book == null) return null;
            
            var levels = book["levels"] as JArray;
            if (levels == null || levels.Count < 2) return null;
            
            var bids = ParseLevels(levels[0]);
            var asks = ParseLevels(levels[1]);
            
            return new ObiBreakdown
            {
                Coin = coin,
                Time = DateTime.UtcNow,
                SimpleOBI = CalculateSimpleOBI(bids, asks),
                WeightedOBI = CalculateWeightedOBI(bids, asks),
                TotalBidQty = bids.Take(_levels).Sum(l => l.Size),
                TotalAskQty = asks.Take(_levels).Sum(l => l.Size),
                BidLevels = bids.Count,
                AskLevels = asks.Count,
                Spread = asks.Count > 0 && bids.Count > 0 
                    ? asks[0].Price - bids[0].Price 
                    : 0,
                MidPrice = asks.Count > 0 && bids.Count > 0 
                    ? (asks[0].Price + bids[0].Price) / 2 
                    : 0
            };
        }
        
        /// <summary>
        /// Clear the cache
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
        
        #region Private Methods
        
        private List<BookLevel> ParseLevels(JToken? levels)
        {
            var result = new List<BookLevel>();
            if (levels == null) return result;
            
            foreach (var level in levels)
            {
                var px = level["px"]?.ToString();
                var sz = level["sz"]?.ToString();
                var n = level["n"]?.Value<int>() ?? 1;
                
                if (px != null && sz != null)
                {
                    result.Add(new BookLevel
                    {
                        Price = decimal.Parse(px),
                        Size = decimal.Parse(sz),
                        OrderCount = n
                    });
                }
            }
            
            return result;
        }
        
        private double CalculateSimpleOBI(List<BookLevel> bids, List<BookLevel> asks)
        {
            var bidQty = bids.Take(_levels).Sum(l => l.Size);
            var askQty = asks.Take(_levels).Sum(l => l.Size);
            
            var total = bidQty + askQty;
            if (total == 0) return 0;
            
            return (double)((bidQty - askQty) / total);
        }
        
        private double CalculateWeightedOBI(List<BookLevel> bids, List<BookLevel> asks)
        {
            double weightedBid = 0, weightedAsk = 0;
            
            for (int i = 0; i < _levels; i++)
            {
                // Inverse distance weighting: level 0 = weight 1, level 1 = weight 0.5, etc.
                double weight = 1.0 / (i + 1);
                
                if (i < bids.Count)
                    weightedBid += (double)bids[i].Size * weight;
                if (i < asks.Count)
                    weightedAsk += (double)asks[i].Size * weight;
            }
            
            var total = weightedBid + weightedAsk;
            if (total == 0) return 0;
            
            return (weightedBid - weightedAsk) / total;
        }
        
        private string ExtractCoin(Symbol symbol)
        {
            // Extract base currency from symbol (e.g., "BTCUSDC" -> "BTC")
            var ticker = symbol.Value;
            
            if (ticker.EndsWith("USDC")) return ticker[..^4];
            if (ticker.EndsWith("USD")) return ticker[..^3];
            if (ticker.EndsWith("USDT")) return ticker[..^4];
            
            // For spot pairs like "PURR/USDC"
            var slashIndex = ticker.IndexOf('/');
            if (slashIndex > 0) return ticker[..slashIndex];
            
            return ticker;
        }
        
        #endregion
        
        public void Dispose()
        {
            if (_ownsClient)
            {
                _client?.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Single level in the order book
    /// </summary>
    public class BookLevel
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public int OrderCount { get; set; }
    }
    
    /// <summary>
    /// Detailed OBI breakdown for analysis
    /// </summary>
    public class ObiBreakdown
    {
        public string Coin { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public double SimpleOBI { get; set; }
        public double WeightedOBI { get; set; }
        public decimal TotalBidQty { get; set; }
        public decimal TotalAskQty { get; set; }
        public int BidLevels { get; set; }
        public int AskLevels { get; set; }
        public decimal Spread { get; set; }
        public decimal MidPrice { get; set; }
        
        /// <summary>
        /// Spread as basis points
        /// </summary>
        public double SpreadBps => MidPrice > 0 
            ? (double)(Spread / MidPrice) * 10000 
            : 0;
        
        public override string ToString() =>
            $"{Coin}: OBI={WeightedOBI:F3} (simple={SimpleOBI:F3}), " +
            $"Bid={TotalBidQty:F2}, Ask={TotalAskQty:F2}, Spread={SpreadBps:F1}bps";
    }
}
