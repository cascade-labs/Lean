/*
 * Lean Algorithmic Trading Engine - Fidelity Brokerage Integration
 *
 * Connects to the Fidelity Python sidecar (fidelity_sidecar.py) which drives
 * a headless Playwright browser against Fidelity's web UI.
 *
 * Supports BrokerageLink (401k) and standard brokerage accounts.
 * Equity/ETF trading only, Market and Limit orders, $0 commissions.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fidelity
{
    /// <summary>
    /// Fidelity brokerage implementation that communicates with a Python sidecar
    /// service running Playwright browser automation against Fidelity's web UI.
    /// </summary>
    [BrokerageFactory(typeof(FidelityBrokerageFactory))]
    public class FidelityBrokerage : Brokerage
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _account;
        private readonly IAlgorithm _algorithm;
        private bool _connected;

        /// <summary>
        /// Creates a new FidelityBrokerage instance
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="sidecarUrl">Base URL of the Python sidecar (default: http://127.0.0.1:5198)</param>
        /// <param name="account">Fidelity account number to trade in</param>
        public FidelityBrokerage(IAlgorithm algorithm, string sidecarUrl, string account)
            : base("Fidelity")
        {
            _algorithm = algorithm;
            _baseUrl = sidecarUrl.TrimEnd('/');
            _account = account;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        /// <summary>
        /// Returns true if the sidecar is connected and logged in
        /// </summary>
        public override bool IsConnected => _connected;

        /// <summary>
        /// USD-based account
        /// </summary>
        public override string AccountBaseCurrency => Currencies.USD;

        /// <summary>
        /// Connects to Fidelity via the sidecar (triggers Playwright login)
        /// </summary>
        public override void Connect()
        {
            Log.Trace("FidelityBrokerage.Connect(): Connecting to sidecar...");
            var response = Post("/connect", new { });
            var json = JObject.Parse(response);
            if (json["success"]?.Value<bool>() != true)
            {
                throw new InvalidOperationException(
                    $"FidelityBrokerage: Failed to connect to sidecar: {response}");
            }
            _connected = true;
            Log.Trace("FidelityBrokerage.Connect(): Connected successfully");
        }

        /// <summary>
        /// Disconnects from the sidecar (closes Playwright browser)
        /// </summary>
        public override void Disconnect()
        {
            try
            {
                Post("/disconnect", new { });
            }
            catch (Exception e)
            {
                Log.Error(e, "FidelityBrokerage.Disconnect()");
            }
            _connected = false;
        }

        /// <summary>
        /// Places an order via the Fidelity sidecar
        /// </summary>
        public override bool PlaceOrder(Order order)
        {
            Log.Trace($"FidelityBrokerage.PlaceOrder(): {order}");

            var action = order.Quantity > 0 ? "buy" : "sell";
            var quantity = Math.Abs(order.Quantity);

            decimal? limitPrice = null;
            if (order is LimitOrder limitOrder)
            {
                limitPrice = limitOrder.LimitPrice;
            }

            try
            {
                var payload = new
                {
                    symbol = order.Symbol.Value,
                    quantity = (double)quantity,
                    action,
                    account = _account,
                    dry_run = false,
                    limit_price = limitPrice.HasValue ? (double?)((double)limitPrice.Value) : null
                };

                var response = Post("/order", payload);
                var json = JObject.Parse(response);
                var success = json["success"]?.Value<bool>() ?? false;
                var message = json["message"]?.Value<string>() ?? "";
                var orderId = json["order_id"]?.Value<string>() ?? $"FID-{DateTime.UtcNow.Ticks}";

                if (success)
                {
                    order.BrokerId.Add(orderId);

                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                    {
                        Status = OrderStatus.Submitted,
                        Message = message
                    });

                    // Fidelity web automation doesn't give real-time fill callbacks,
                    // so we assume market orders fill immediately
                    if (order.Type == OrderType.Market)
                    {
                        var fillPrice = order.Symbol.HasUnderlying
                            ? 0m
                            : GetLastPrice(order.Symbol);

                        OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                        {
                            Status = OrderStatus.Filled,
                            FillQuantity = order.Quantity,
                            FillPrice = fillPrice,
                            Message = "Filled via Fidelity web automation"
                        });
                    }
                    return true;
                }
                else
                {
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                    {
                        Status = OrderStatus.Invalid,
                        Message = $"Fidelity order rejected: {message}"
                    });
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "FidelityBrokerage.PlaceOrder()");
                OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                {
                    Status = OrderStatus.Invalid,
                    Message = $"Fidelity order error: {e.Message}"
                });
                return false;
            }
        }

        /// <summary>
        /// Order updates are not supported by Fidelity web automation
        /// </summary>
        public override bool UpdateOrder(Order order)
        {
            OnMessage(new BrokerageMessageEvent(
                BrokerageMessageType.Warning, "NotSupported",
                "Fidelity does not support order updates. Cancel and re-place instead."));
            return false;
        }

        /// <summary>
        /// Order cancellation is not reliably supported via web automation
        /// </summary>
        public override bool CancelOrder(Order order)
        {
            OnMessage(new BrokerageMessageEvent(
                BrokerageMessageType.Warning, "NotSupported",
                "Fidelity web automation does not support order cancellation."));
            return false;
        }

        /// <summary>
        /// Gets open orders. Fidelity web automation doesn't expose pending orders well,
        /// so we return empty (Lean handles order state internally).
        /// </summary>
        public override List<Order> GetOpenOrders()
        {
            return new List<Order>();
        }

        /// <summary>
        /// Gets account holdings from the Fidelity sidecar
        /// </summary>
        public override List<Holding> GetAccountHoldings()
        {
            try
            {
                var response = Get("/holdings");
                var items = JArray.Parse(response);
                var holdings = new List<Holding>();

                foreach (var item in items)
                {
                    var symbol = item["symbol"]?.Value<string>();
                    if (string.IsNullOrEmpty(symbol))
                        continue;

                    // Skip money market / core positions (they are cash)
                    var upper = symbol.ToUpperInvariant();
                    if (upper == "SPAXX" || upper == "FDRXX" || upper == "FCASH"
                        || upper == "FZFXX" || upper == "SPRXX")
                        continue;

                    var qty = item["quantity"]?.Value<decimal>() ?? 0;
                    var price = item["last_price"]?.Value<decimal>() ?? 0;

                    holdings.Add(new Holding
                    {
                        Symbol = Symbol.Create(symbol, SecurityType.Equity, Market.USA),
                        Quantity = qty,
                        AveragePrice = price,
                        MarketPrice = price,
                        CurrencySymbol = "$"
                    });
                }
                return holdings;
            }
            catch (Exception e)
            {
                Log.Error(e, "FidelityBrokerage.GetAccountHoldings()");
                return new List<Holding>();
            }
        }

        /// <summary>
        /// Gets cash balance from the sidecar
        /// </summary>
        public override List<CashAmount> GetCashBalance()
        {
            try
            {
                var response = Get("/cash");
                var items = JArray.Parse(response);
                return items.Select(item => new CashAmount(
                    item["amount"]?.Value<decimal>() ?? 0,
                    item["currency"]?.Value<string>() ?? Currencies.USD
                )).ToList();
            }
            catch (Exception e)
            {
                Log.Error(e, "FidelityBrokerage.GetCashBalance()");
                return new List<CashAmount>
                {
                    new CashAmount(0, Currencies.USD)
                };
            }
        }

        /// <summary>
        /// No history available via web automation
        /// </summary>
        public override IEnumerable<BaseData> GetHistory(HistoryRequest request)
        {
            return Enumerable.Empty<BaseData>();
        }

        /// <summary>
        /// Clean up HTTP client
        /// </summary>
        public override void Dispose()
        {
            Disconnect();
            _httpClient?.Dispose();
            base.Dispose();
        }

        // ---------------------------------------------------------------
        // HTTP helpers
        // ---------------------------------------------------------------

        private string Get(string path)
        {
            var url = _baseUrl + path;
            var result = _httpClient.GetAsync(url).GetAwaiter().GetResult();
            var body = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Fidelity sidecar GET {path} returned {result.StatusCode}: {body}");
            }
            return body;
        }

        private string Post(string path, object payload)
        {
            var url = _baseUrl + path;
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var result = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();
            var body = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Fidelity sidecar POST {path} returned {result.StatusCode}: {body}");
            }
            return body;
        }

        private decimal GetLastPrice(Symbol symbol)
        {
            try
            {
                if (_algorithm?.Securities != null &&
                    _algorithm.Securities.TryGetValue(symbol, out var security))
                {
                    return security.Price;
                }
            }
            catch
            {
                // Swallow - best effort
            }
            return 0m;
        }
    }
}
