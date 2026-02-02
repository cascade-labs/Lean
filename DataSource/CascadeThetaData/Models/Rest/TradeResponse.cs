/*
 * Cascade Labs - ThetaData Trade Response Model
 */

using Newtonsoft.Json;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Rest
{
    /// <summary>
    /// Trade tick data from ThetaData API
    /// </summary>
    public class TradeResponse
    {
        /// <summary>
        /// Milliseconds since epoch
        /// </summary>
        [JsonProperty("ms_of_day")]
        public long MillisecondsOfDay { get; set; }

        /// <summary>
        /// Date in YYYYMMDD format
        /// </summary>
        [JsonProperty("date")]
        public int Date { get; set; }

        /// <summary>
        /// Trade price
        /// </summary>
        [JsonProperty("price")]
        public decimal Price { get; set; }

        /// <summary>
        /// Trade size/volume
        /// </summary>
        [JsonProperty("size")]
        public decimal Size { get; set; }

        /// <summary>
        /// Trade condition code
        /// </summary>
        [JsonProperty("condition")]
        public int Condition { get; set; }

        /// <summary>
        /// Exchange code
        /// </summary>
        [JsonProperty("exchange")]
        public int Exchange { get; set; }

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
