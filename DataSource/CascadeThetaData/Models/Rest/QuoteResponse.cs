/*
 * Cascade Labs - ThetaData Quote Response Model
 */

using Newtonsoft.Json;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Rest
{
    /// <summary>
    /// Quote tick data from ThetaData API
    /// </summary>
    public class QuoteResponse
    {
        /// <summary>
        /// Milliseconds since midnight
        /// </summary>
        [JsonProperty("ms_of_day")]
        public long MillisecondsOfDay { get; set; }

        /// <summary>
        /// Date in YYYYMMDD format
        /// </summary>
        [JsonProperty("date")]
        public int Date { get; set; }

        /// <summary>
        /// Bid price
        /// </summary>
        [JsonProperty("bid")]
        public decimal BidPrice { get; set; }

        /// <summary>
        /// Bid size
        /// </summary>
        [JsonProperty("bid_size")]
        public decimal BidSize { get; set; }

        /// <summary>
        /// Bid exchange code
        /// </summary>
        [JsonProperty("bid_exchange")]
        public int BidExchange { get; set; }

        /// <summary>
        /// Bid condition
        /// </summary>
        [JsonProperty("bid_condition")]
        public string BidCondition { get; set; } = string.Empty;

        /// <summary>
        /// Ask price
        /// </summary>
        [JsonProperty("ask")]
        public decimal AskPrice { get; set; }

        /// <summary>
        /// Ask size
        /// </summary>
        [JsonProperty("ask_size")]
        public decimal AskSize { get; set; }

        /// <summary>
        /// Ask exchange code
        /// </summary>
        [JsonProperty("ask_exchange")]
        public int AskExchange { get; set; }

        /// <summary>
        /// Ask condition
        /// </summary>
        [JsonProperty("ask_condition")]
        public string AskCondition { get; set; } = string.Empty;

        /// <summary>
        /// Gets the DateTime from the date and milliseconds
        /// </summary>
        public DateTime DateTimeMilliseconds
        {
            get
            {
                var year = Date / 10000;
                var month = (Date / 100) % 100;
                var day = Date % 100;
                return new DateTime(year, month, day).AddMilliseconds(MillisecondsOfDay);
            }
        }
    }
}
