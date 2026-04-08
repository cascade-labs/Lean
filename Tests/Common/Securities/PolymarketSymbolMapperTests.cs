using NUnit.Framework;
using QuantConnect.Lean.DataSource.PolymarketData;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class PolymarketSymbolMapperTests
    {
        [Test]
        public void RegistersExplicitPolymarketYesAndNoSymbols()
        {
            var mapper = new PolymarketSymbolMapper();
            var metadata = new PolymarketMarketMetadata
            {
                Slug = "test-market",
                ConditionId = "condition-1",
                Category = "Crypto",
                FeesEnabled = true,
                YesTokenId = "yes-token",
                NoTokenId = "no-token"
            };

            var (yes, no) = mapper.GetLeanTokenSymbols("test-market", metadata);

            Assert.AreEqual(Market.Polymarket, yes.ID.Market);
            Assert.AreEqual(Market.Polymarket, no.ID.Market);
            Assert.IsTrue(yes.IsYesToken);
            Assert.IsTrue(no.IsNoToken);
            Assert.IsTrue(mapper.IsPolymarketSymbol(yes));
            Assert.IsTrue(mapper.IsPolymarketSymbol(no));
            Assert.IsFalse(mapper.IsPolymarketSymbol(Symbol.CreatePredictionMarketToken("test-market", Market.Kalshi, PredictionMarketTokenType.Yes)));

            Assert.IsTrue(PolymarketMarketRegistry.TryGet(yes, out var registered));
            Assert.AreEqual("yes-token", registered.GetTokenId(yes));
            Assert.AreEqual("no-token", registered.GetTokenId(no));
        }
    }
}
