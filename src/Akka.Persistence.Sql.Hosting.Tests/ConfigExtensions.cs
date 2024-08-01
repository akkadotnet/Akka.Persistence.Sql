// -----------------------------------------------------------------------
//  <copyright file="ConfigExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Extensions;
using FluentAssertions;

namespace Akka.Persistence.Sql.Hosting.Tests;

public static class ConfigExtensions
{
    public static void AssertType(this Configuration.Config actual, Configuration.Config expected, string key, Type? value = null)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        if (value is not null)
            Type.GetType(actual.GetString(key)).Should().Be(value);
        else
            actual.GetString(key).Should().Be(expected.GetString(key));
    }
    
    public static void AssertString(this Configuration.Config actual, Configuration.Config expected, string key, string? value = null)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        actual.GetString(key).Should().Be(value ?? expected.GetString(key));
    }

    public static void AssertInt(this Configuration.Config actual, Configuration.Config expected, string key, int? value = null)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        actual.GetInt(key).Should().Be(value ?? expected.GetInt(key));
    }

    public static void AssertBool(this Configuration.Config actual, Configuration.Config expected, string key, bool? value = null)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        actual.GetBoolean(key).Should().Be(value ?? expected.GetBoolean(key));
    }

    public static void AssertTimeSpan(this Configuration.Config actual, Configuration.Config expected, string key, TimeSpan? value = null)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        actual.GetTimeSpan(key).Should().Be(value ?? expected.GetTimeSpan(key));
    }
    
    public static void AssertIsolationLevel(this Configuration.Config actual, Configuration.Config expected, string key)
    {
        expected.HasPath(key).Should().BeTrue();
        actual.HasPath(key).Should().BeTrue();
        actual.GetIsolationLevel(key).Should().Be(expected.GetIsolationLevel(key));
    }
    
    public static void AssertMappingEquals(this Configuration.Config actual, Configuration.Config expected, string key)
    {
        var actualMapConfig = actual.GetConfig(key);
        var expectedMapConfig = expected.GetConfig(key);
        
        actualMapConfig.AssertString(expectedMapConfig, "schema-name");

        var actualJournalConfig = actualMapConfig.GetConfig("journal");
        actualJournalConfig.Should().NotBeNull();
        var expectedJournalConfig = expectedMapConfig.GetConfig("journal");
        expectedJournalConfig.Should().NotBeNull();
        
        actualJournalConfig.AssertBool(expectedJournalConfig, "use-writer-uuid-column");
        actualJournalConfig.AssertString(expectedJournalConfig, "table-name");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.ordering");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.deleted");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.persistence-id");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.sequence-number");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.created");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.tags");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.message");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.identifier");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.manifest");
        actualJournalConfig.AssertString(expectedJournalConfig, "columns.writer-uuid");
     
        var actualMetaConfig = actualMapConfig.GetConfig("metadata");
        actualMetaConfig.Should().NotBeNull();
        var expectedMetaConfig = expectedMapConfig.GetConfig("metadata");
        expectedMetaConfig.Should().NotBeNull();

        actualMetaConfig.AssertString(expectedMetaConfig, "table-name");
        actualMetaConfig.AssertString(expectedMetaConfig, "columns.persistence-id");
        actualMetaConfig.AssertString(expectedMetaConfig, "columns.sequence-number");
        
        var actualTagConfig = actualMapConfig.GetConfig("tag");
        actualTagConfig.Should().NotBeNull();
        var expectedTagConfig = expectedMapConfig.GetConfig("tag");
        expectedTagConfig.Should().NotBeNull();

        actualTagConfig.AssertString(expectedTagConfig, "table-name");
        actualTagConfig.AssertString(expectedTagConfig, "columns.ordering-id");
        actualTagConfig.AssertString(expectedTagConfig, "columns.tag-value");
        actualTagConfig.AssertString(expectedTagConfig, "columns.persistence-id");
        actualTagConfig.AssertString(expectedTagConfig, "columns.sequence-nr");
    }
    
}
