/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IOptionChainProvider"/> that will cache by date option contracts returned by another option chain provider.
    /// </summary>
    public class CachingOptionChainProvider : IOptionChainProvider
    {
        private readonly ConcurrentDictionary<(Symbol, DateTime), Lazy<List<Symbol>>> _cache = new();
        private readonly IOptionChainProvider _optionChainProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingOptionChainProvider"/> class
        /// </summary>
        /// <param name="optionChainProvider"></param>
        public CachingOptionChainProvider(IOptionChainProvider optionChainProvider)
        {
            _optionChainProvider = optionChainProvider;
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The option or the underlying symbol to get the option chain for.
        /// Providing the option allows targetting an option ticker different than the default e.g. SPXW</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            var key = (symbol, date.Date);
            var lazy = _cache.GetOrAdd(key, k =>
                new Lazy<List<Symbol>>(() => _optionChainProvider.GetOptionContractList(k.Item1, k.Item2).ToList()));
            return lazy.Value;
        }
    }
}
