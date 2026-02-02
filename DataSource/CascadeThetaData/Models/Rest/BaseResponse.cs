/*
 * Cascade Labs - ThetaData Base Response
 */

using Newtonsoft.Json;
using QuantConnect.Lean.DataSource.CascadeThetaData.Models.Interfaces;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Rest
{
    /// <summary>
    /// Generic response wrapper for ThetaData API responses
    /// </summary>
    /// <typeparam name="T">Type of data in the response</typeparam>
    public class BaseResponse<T> : IBaseResponse
    {
        /// <summary>
        /// Response header with metadata
        /// </summary>
        [JsonProperty("header")]
        public ResponseHeader Header { get; set; } = new();

        /// <summary>
        /// Response data array
        /// </summary>
        [JsonProperty("response")]
        public List<T>? Response { get; set; }
    }
}
