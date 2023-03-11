// -----------------------------------------------------------------------
//  <copyright file="EventAdapter.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal
{
    public sealed class EventAdapter: IWriteEventAdapter
    {
        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            var value = evt switch
            {
                int i => i,
                string str => int.Parse(str),
                _ => throw new Exception($"Unknown type: {evt.GetType()}")
            };
        
            return evt.ToTagged(value);
        }
    }    
}

