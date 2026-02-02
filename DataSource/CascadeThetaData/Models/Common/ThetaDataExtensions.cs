/*
 * Cascade Labs - ThetaData Extension Methods
 */

using System.Globalization;
using QuantConnect.Securities;

namespace QuantConnect.Lean.DataSource.CascadeThetaData.Models.Common
{
    /// <summary>
    /// Extension methods for ThetaData operations
    /// </summary>
    public static class ThetaDataExtensions
    {
        private static readonly Dictionary<int, string> ExchangeMap = new()
        {
            { 0, "UNKNOWN" },
            { 1, "NYSE" },
            { 2, "AMEX" },
            { 3, "NASDAQ" },
            { 4, "CBOE" },
            { 5, "ISE" },
            { 6, "PHLX" },
            { 7, "BOX" },
            { 8, "ARCA" },
            { 9, "BATS" },
            { 10, "C2" },
            { 11, "EDGX" },
            { 12, "MIAX" },
            { 13, "GEMINI" },
            { 14, "PEARL" },
            { 15, "EMLD" },
            { 16, "MPRL" },
            { 17, "SPHR" },
            { 18, "MEMX" }
        };

        /// <summary>
        /// Converts a DateTime to ThetaData date format (YYYYMMDD)
        /// </summary>
        public static string ConvertToThetaDataDateFormat(this DateTime dateTime)
        {
            return dateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts ThetaData date format (YYYYMMDD) to DateTime
        /// </summary>
        public static DateTime ConvertFromThetaDataDateFormat(this string dateString)
        {
            return DateTime.ParseExact(dateString, "yyyyMMdd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generates date ranges with specified interval for parallel requests
        /// </summary>
        public static IEnumerable<(DateTime startDate, DateTime endDate)> GenerateDateRangesWithInterval(
            DateTime startDate,
            DateTime endDate,
            int intervalInDays = 30)
        {
            var currentStart = startDate;
            while (currentStart < endDate)
            {
                var currentEnd = currentStart.AddDays(intervalInDays);
                if (currentEnd > endDate)
                {
                    currentEnd = endDate;
                }

                yield return (currentStart, currentEnd);
                currentStart = currentEnd;
            }
        }

        /// <summary>
        /// Tries to get exchange name from code, returns "UNKNOWN" if not found
        /// </summary>
        public static string TryGetExchangeOrDefault(this int exchangeCode)
        {
            return ExchangeMap.TryGetValue(exchangeCode, out var exchange) ? exchange : "UNKNOWN";
        }

        /// <summary>
        /// Gets the symbol's exchange time zone
        /// </summary>
        public static NodaTime.DateTimeZone GetSymbolExchangeTimeZone(this Symbol symbol)
        {
            var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            return marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType).TimeZone;
        }
    }
}
