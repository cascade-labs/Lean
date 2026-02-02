/*
 * Cascade Labs - ThetaData Subscription Plan Interface
 */

using QuantConnect.Util;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Interfaces
{
    /// <summary>
    /// Defines the capabilities and limits of a ThetaData subscription plan
    /// </summary>
    public interface ISubscriptionPlan
    {
        /// <summary>
        /// Rate limiter for API requests
        /// </summary>
        RateGate? RateGate { get; }

        /// <summary>
        /// Resolutions accessible under this plan
        /// </summary>
        HashSet<Resolution> AccessibleResolutions { get; }

        /// <summary>
        /// Earliest date data can be accessed from
        /// </summary>
        DateTime FirstAccessDate { get; }
    }
}
