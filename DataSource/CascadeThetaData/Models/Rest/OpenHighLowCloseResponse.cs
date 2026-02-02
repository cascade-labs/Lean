/*
 * Cascade Labs - ThetaData OHLC Response Model
 */

using Newtonsoft.Json;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Rest
{
    /// <summary>
    /// OHLC bar data from ThetaData API
    /// </summary>
    public class OpenHighLowCloseResponse
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
