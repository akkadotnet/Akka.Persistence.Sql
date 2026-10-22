# Akka.Persistence.Sql DDL Scripts

This directory contains pre-generated DDL (Data Definition Language) scripts for creating the database tables required by Akka.Persistence.Sql. These scripts allow you to manage your database schema through migrations (e.g., Entity Framework Core, FluentMigrator) rather than relying on automatic table creation at runtime.

## Directory Structure

```
docs/ddl/
├── README.md
├── default/           # For new deployments (table-mapping = default)
│   ├── sqlserver/
│   ├── postgresql/
│   ├── mysql/
│   └── sqlite/
└── compat/            # For legacy plugin migrations
    ├── sqlserver/     # table-mapping = sql-server
    ├── postgresql/    # table-mapping = postgresql
    ├── mysql/         # table-mapping = mysql
    └── sqlite/        # table-mapping = sqlite
```

Each provider directory contains four DDL files:
- `journal.sql` - Event journal table
- `journal-tags.sql` - Tag table for `TagMode.TagTable`
- `snapshot.sql` - Snapshot store table
- `metadata.sql` - Metadata table for `delete-compatibility-mode`

## Which DDL Should I Use?

### New Deployments → Use `default/`

If you're starting fresh with Akka.Persistence.Sql and not migrating from a legacy plugin, use the DDL files from the `default/` directory. These scripts create tables with the default naming convention:

| Table | Name |
|-------|------|
| Journal | `journal` |
| Tags | `tags` |
| Snapshot | `snapshot` |
| Metadata | `journal_metadata` |

**HOCON Configuration:**
```hocon
akka.persistence.journal.sql {
    table-mapping = default  # This is the default, can be omitted
}
```

### Migration from Legacy Plugins → Use `compat/`

If you're migrating from legacy Akka.Persistence plugins (Akka.Persistence.SqlServer, Akka.Persistence.PostgreSql, etc.), use the DDL files from the `compat/` directory. These scripts create tables with the legacy naming conventions:

| Plugin | Table Mapping | Journal Table | Columns |
|--------|--------------|---------------|---------|
| Akka.Persistence.SqlServer | `sql-server` | `EventJournal` | PascalCase |
| Akka.Persistence.PostgreSql | `postgresql` | `event_journal` | snake_case |
| Akka.Persistence.MySql | `mysql` | `event_journal` | snake_case |
| Akka.Persistence.Sqlite | `sqlite` | `event_journal` | snake_case |

**HOCON Configuration (SQL Server example):**
```hocon
akka.persistence.journal.sql {
    table-mapping = sql-server
}
```

## Using DDL Scripts with Migrations

### Entity Framework Core

You can execute these DDL scripts as raw SQL migrations:

```csharp
public partial class AddAkkaPersistenceTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Read and execute DDL files
        var journalDdl = File.ReadAllText("path/to/journal.sql");
        migrationBuilder.Sql(journalDdl);

        var tagsDdl = File.ReadAllText("path/to/journal-tags.sql");
        migrationBuilder.Sql(tagsDdl);

        var snapshotDdl = File.ReadAllText("path/to/snapshot.sql");
        migrationBuilder.Sql(snapshotDdl);
    }
}
```

### FluentMigrator

```csharp
[Migration(1)]
public class AddAkkaPersistenceTables : Migration
{
    public override void Up()
    {
        Execute.EmbeddedScript("journal.sql");
        Execute.EmbeddedScript("journal-tags.sql");
        Execute.EmbeddedScript("snapshot.sql");
    }
}
```

## Idempotency

All DDL scripts are idempotent - they can be executed multiple times without error. Each script includes appropriate checks:

- **SQL Server**: `IF NOT EXISTS` checks with `sys.tables`
- **PostgreSQL**: `CREATE TABLE IF NOT EXISTS`
- **MySQL**: `CREATE TABLE IF NOT EXISTS`
- **SQLite**: `CREATE TABLE IF NOT EXISTS`

This means you can safely include these scripts in your migration pipeline without worrying about failures on re-runs.

## Customizing Table Names

If you need custom table names or schema, you have two options:

1. **Modify the DDL scripts**: Copy the appropriate DDL files and modify the table names to match your requirements.

2. **Use HOCON configuration**: Configure custom table names in your HOCON:

```hocon
akka.persistence.journal.sql {
    table-mapping = default
    default {
        schema-name = "akka"  # Custom schema
        journal {
            table-name = "my_journal"
        }
        tag {
            table-name = "my_tags"
        }
    }
}
```

Then regenerate DDL or modify scripts accordingly.

## Regenerating DDL Scripts

If you need to regenerate the DDL scripts (e.g., after configuration changes), you can use the DDL Generator tool:

```bash
cd src/Akka.Persistence.Sql.DdlGenerator
dotnet run -- --all --table-mapping all
```

Options:
- `--output, -o`: Output directory (default: `docs/ddl`)
- `--all, -a`: Generate for all providers
- `--provider, -p`: Generate for specific provider (SqlServer, PostgreSQL, MySQL, SQLite)
- `--table-mapping, -m`: Table mapping mode: `default`, `compat`, or `all`

## Disabling Auto-Initialization

When using DDL scripts for schema management, you should disable automatic table creation:

```hocon
akka.persistence.journal.sql {
    auto-initialize = false
}

akka.persistence.snapshot-store.sql {
    auto-initialize = false
}
```

This prevents Akka.Persistence.Sql from attempting to create tables at startup, which could conflict with your migration-managed schema.

## See Also

- [Tag-table orphan cleanup and FK migration](../../scripts/TagTableForeignKey/README.md) - Explicit PostgreSQL and SQL Server maintenance scripts for existing databases
- [Migration Guide](../articles/migration.md) - Migrating from legacy plugins
- [Configuration](../articles/configuration.md) - Full configuration reference
