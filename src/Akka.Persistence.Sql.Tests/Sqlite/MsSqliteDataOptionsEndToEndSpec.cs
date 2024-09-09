// -----------------------------------------------------------------------
//  <copyright file="MsSqliteDataOptionsEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteDataOptionsEndToEndSpec: SqlDataOptionsEndToEndSpecBase<MsSqliteContainer>
    {
        public MsSqliteDataOptionsEndToEndSpec(ITestOutputHelper output, MsSqliteContainer fixture) 
            : base(nameof(MsSqliteDataOptionsEndToEndSpec), output, fixture) { }
    }
}
