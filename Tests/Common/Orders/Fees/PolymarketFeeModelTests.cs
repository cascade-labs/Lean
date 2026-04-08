using System;
using NUnit.Framework;
using QuantConnect.Brokerages.Polymarket;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Securities.PredictionMarket;

namespace QuantConnect.Tests.Common.Orders.Fees
{
    [TestFixture]
    public class PolymarketFeeModelTests
    {
        private PredictionMarket _security = null!;
        private readonly Symbol _symbol = Symbol.CreatePredictionMarketToken("test-market", Market.Polymarket, PredictionMarketTokenType.Yes);

        [SetUp]
        public void SetUp()
        {
            _security = new PredictionMarket(
                _symbol,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                new Cash("USDC", 0, 1m),
                new SymbolProperties("TEST-MARKET", "USDC", 1, 0.0001m, 1m, string.Empty),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache());

            _security.SetMarketPrice(new QuoteBar(
                DateTime.UtcNow,
                _symbol,
                new Bar(0.59m, 0.59m, 0.59m, 0.59m),
                1,
                new Bar(0.61m, 0.61m, 0.61m, 0.61m),
                1,
                TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void ReturnsZeroWhenFeesDisabled()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "Crypto", false, "tok", 10m)));

            var fee = feeModel.GetOrderFee(new OrderFeeParameters(_security, new MarketOrder(_symbol, 100, DateTime.UtcNow)));

            Assert.AreEqual(0m, fee.Value.Amount);
        }

        [Test]
        public void AppliesCryptoExponentTwo()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "Crypto", true, "tok", 10m)));

            var fee = feeModel.GetOrderFee(new OrderFeeParameters(_security, new MarketOrder(_symbol, 100, DateTime.UtcNow)));

            Assert.AreEqual("USDC", fee.Value.Currency);
            Assert.AreEqual(0.0035m, fee.Value.Amount);
        }

        [Test]
        public void AppliesSportsExponentOne()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "NCAAB", true, "tok", 10m)));

            var fee = feeModel.GetOrderFee(new OrderFeeParameters(_security, new MarketOrder(_symbol, 100, DateTime.UtcNow)));

            Assert.AreEqual(0.0145m, fee.Value.Amount);
        }

        [Test]
        public void ReturnsZeroForRestingMakerLimitOrders()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "Crypto", true, "tok", 10m)));
            var order = new LimitOrder(_symbol, 100, 0.50m, DateTime.UtcNow)
            {
                OrderSubmissionData = new OrderSubmissionData(_security.BidPrice, _security.AskPrice, _security.Price)
            };

            var fee = feeModel.GetOrderFee(new OrderFeeParameters(_security, order));

            Assert.AreEqual(0m, fee.Value.Amount);
        }

        [Test]
        public void EnforcesMinimumNonZeroFee()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "NCAAB", true, "tok", 10m)));

            var fee = feeModel.GetOrderFee(new OrderFeeParameters(_security, new MarketOrder(_symbol, 1, DateTime.UtcNow)));

            Assert.AreEqual(PolymarketFeeModel.MinimumNonZeroFee, fee.Value.Amount);
        }

        [Test]
        public void ThrowsForUnsupportedFeeCategory()
        {
            var feeModel = new PolymarketFeeModel(new StubFeeScheduleProvider(
                new PolymarketFeeSchedule("test-market", "Politics", true, "tok", 10m)));

            Assert.Throws<NotSupportedException>(() =>
                feeModel.GetOrderFee(new OrderFeeParameters(_security, new MarketOrder(_symbol, 100, DateTime.UtcNow))));
        }

        private sealed class StubFeeScheduleProvider : IPolymarketFeeScheduleProvider
        {
            private readonly PolymarketFeeSchedule _schedule;

            public StubFeeScheduleProvider(PolymarketFeeSchedule schedule)
            {
                _schedule = schedule;
            }

            public PolymarketFeeSchedule GetFeeSchedule(Symbol symbol)
            {
                return _schedule;
            }
        }
    }
}
