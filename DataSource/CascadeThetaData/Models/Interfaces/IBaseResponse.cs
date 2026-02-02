/*
 * Cascade Labs - ThetaData Response Interface
 */

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Interfaces
{
    /// <summary>
    /// Base interface for all ThetaData API responses
    /// </summary>
    public interface IBaseResponse
    {
        /// <summary>
        /// Response header containing metadata and pagination info
        /// </summary>
        ResponseHeader Header { get; }
    }

    /// <summary>
    /// Response header with pagination support
    /// </summary>
    public class ResponseHeader
    {
        /// <summary>
        /// URL for the next page of results, if available
        /// </summary>
        public string? NextPage { get; set; }

        /// <summary>
        /// Format information for the response data
        /// </summary>
        public string[]? Format { get; set; }
    }
}
