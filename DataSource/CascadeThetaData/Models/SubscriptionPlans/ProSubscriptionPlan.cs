/*
 * Cascade Labs - ThetaData Pro Subscription Plan
 */

using QuantConnect.Util;
using QuantConnect.Lean.DataSource.CascadeThetaData.Models.Interfaces;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.SubscriptionPlans
{
    /// <summary>
    /// Pro subscription plan with full access to ThetaData
    /// </summary>
    public class ProSubscriptionPlan : ISubscriptionPlan
    {
        /// <summary>
        /// Rate limiter - Pro allows 100 requests per second
        /// </summary>
        public RateGate? RateGate { get; } = new RateGate(100, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Pro plan has access to all resolutions
        /// </summary>
        public HashSet<Resolution> AccessibleResolutions { get; } = new()
        {
            Resolution.Tick,
            Resolution.Second,
            Resolution.Minute,
            Resolution.Hour,
            Resolution.Daily
        };

        /// <summary>
        /// Pro plan can access data from 2007
        /// </summary>
        public DateTime FirstAccessDate { get; } = new DateTime(2007, 1, 1);
    }
}
