/*
 * Cascade Labs - ThetaData End of Day Response Model
 */

using Newtonsoft.Json;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Rest
{
    /// <summary>
    /// End of day report data from ThetaData API
    /// </summary>
    public class EndOfDayReportResponse
    {
        /// <summary>
        /// Date in YYYYMMDD format
        /// </summary>
        [JsonProperty("date")]
        public int Date { get; set; }

        /// <summary>
        /// Milliseconds since midnight for last trade
        /// </summary>
        [JsonProperty("ms_of_day")]
        public long MillisecondsOfDay { get; set; }

        /// <summary>
        /// Opening price
        /// </summary>
        [JsonProperty("open")]
        public decimal Open { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        [JsonProperty("high")]
        public decimal High { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        [JsonProperty("low")]
        public decimal Low { get; set; }

        /// <summary>
        /// Closing price
        /// </summary>
        [JsonProperty("close")]
        public decimal Close { get; set; }

        /// <summary>
        /// Volume
        /// </summary>
        [JsonProperty("volume")]
        public decimal Volume { get; set; }

        /// <summary>
        /// Number of trades
        /// </summary>
        [JsonProperty("count")]
        public int Count { get; set; }

        /// <summary>
        /// Bid price at close
        /// </summary>
        [JsonProperty("bid")]
        public decimal BidPrice { get; set; }

        /// <summary>
        /// Bid size at close
        /// </summary>
        [JsonProperty("bid_size")]
        public decimal BidSize { get; set; }

        /// <summary>
        /// Ask price at close
        /// </summary>
        [JsonProperty("ask")]
        public decimal AskPrice { get; set; }

        /// <summary>
        /// Ask size at close
        /// </summary>
        [JsonProperty("ask_size")]
        public decimal AskSize { get; set; }

        /// <summary>
        /// Gets the DateTime from the date and milliseconds
        /// </summary>
        public DateTime LastTradeDateTimeMilliseconds
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
