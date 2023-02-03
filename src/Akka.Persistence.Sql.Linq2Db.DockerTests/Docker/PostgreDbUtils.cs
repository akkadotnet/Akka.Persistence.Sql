//-----------------------------------------------------------------------
// <copyright file="DbUtils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Npgsql;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker
{
    public static class PostgreDbUtils
    {
        public static string ConnectionString { get; private set; }

        public static void Initialize(PostgreSqlFixture fixture)
        {
            ConnectionString = fixture.ConnectionString;
            var connectionBuilder = new NpgsqlConnectionStringBuilder(ConnectionString);

            //connect to postgres database to create a new database
            var databaseName = connectionBuilder.Database;

            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            bool dbExists;
            using (var cmd = new NpgsqlCommand())
            {
                cmd.CommandText = $@"SELECT TRUE FROM pg_database WHERE datname='{databaseName}'";
                cmd.Connection = conn;

                var result = cmd.ExecuteScalar();
                dbExists = result != null && Convert.ToBoolean(result);
            }

            if (dbExists)
            {
                DoClean(conn);
            }
            else
            {
                DoCreate(conn, databaseName);
            }
        }

        public static void Clean()
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            DoClean(conn);
        }

        private static void DoCreate(NpgsqlConnection conn, string databaseName)
        {
            using var cmd = new NpgsqlCommand();
            cmd.CommandText = $@"CREATE DATABASE {databaseName}";
            cmd.Connection = conn;
            cmd.ExecuteNonQuery();
        }

        private static void DoClean(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand();
            cmd.CommandText = @"
                    DROP TABLE IF EXISTS public.event_journal;
                    DROP TABLE IF EXISTS public.snapshot_store;
                    DROP TABLE IF EXISTS public.metadata;";
            cmd.Connection = conn;
            cmd.ExecuteNonQuery();
        }
    }
}