// -----------------------------------------------------------------------
//  <copyright file="MultiDataOptionsSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Akka.Actor.Setup;
using LinqToDB;

namespace Akka.Persistence.Sql.Config
{
    public sealed class MultiDataOptionsSetup: Setup
    {
        private readonly Dictionary<string, DataOptions> _options = new ();

        public void AddDataOptions(string pluginId, DataOptions dataOptions)
            => _options[pluginId] = dataOptions;

        public bool TryGetDataOptionsFor(string pluginId, out DataOptions dataOptions)
            => _options.TryGetValue(pluginId, out dataOptions);

        public void RemoveDataOptionsFor(string pluginId)
            => _options.Remove(pluginId);
    }
}
