# Tag-table orphan cleanup and foreign-key migration

The journal-to-tag FK is an **optional, operator-applied migration**. The checked-in
[base initialization DDL](../../docs/ddl/README.md) and runtime auto-initialization
create tables without this constraint. Orphan cleanup can be used on its own;
installing the FK is a separate operator decision based on the workload's
[measured performance tradeoffs](../../README.md#measured-fk-overhead).

These operator-applied scripts support **PostgreSQL and SQL Server ordinary,
non-partitioned tables**. They add
`tags.ordering_id -> journal.ordering ON DELETE CASCADE` to an existing schema.
They run separately from application startup. Installing a package does not run
them, and application accounts do not need DDL permissions.

| Script | Behavior |
| --- | --- |
| `01_cleanup_orphan_tags.sql` | Reports orphan count and sample ordering IDs by default. Explicit execution deletes at most one configured batch per invocation. |
| `02_add_foreign_key.sql` | Refuses orphaned data or conflicting constraints; installs and validates the cascading relationship. |
| `03_drop_foreign_key.sql` | Removes only the matching relationship. Does not restore deleted data. |

An orphan is a tag row whose journal ordering ID is **physically absent**.
Cleanup retains tags whose parent is soft-deleted but still present. It does
not delete tags based on age, projection offsets, or persistence ID alone.
Removing tags whose journal events still exist can change `EventsByTag` results.

## New databases

Choose the [default](../../docs/ddl/default/) or
[compatibility](../../docs/ddl/compat/) mapping for the provider. Configure the
schema/table names in the deployment copies, run `journal.sql` before
`journal-tags.sql`, and create the snapshot and metadata tables where required.
The initialization SQL is already checked into this repository for every
supported provider; it does not need to be generated to deploy the base schema.

For a schema without the FK, use that base schema alone. To opt in on an empty
PostgreSQL or SQL Server schema, configure and run the provider's
`02_add_foreign_key.sql` after both tables exist and before starting writers.
The script validates existing rows, so an existing/populated deployment must
follow the audit/cleanup workflow below first. Disable journal and snapshot
`auto-initialize` when the deployment manages schema creation externally.

| Provider | Base journal | Base tags | Optional FK installation |
| --- | --- | --- | --- |
| PostgreSQL | [journal.sql](../../docs/ddl/default/postgresql/journal.sql) | [journal-tags.sql](../../docs/ddl/default/postgresql/journal-tags.sql) | [02_add_foreign_key.sql](PostgreSql/02_add_foreign_key.sql) |
| SQL Server | [journal.sql](../../docs/ddl/default/sqlserver/journal.sql) | [journal-tags.sql](../../docs/ddl/default/sqlserver/journal-tags.sql) | [02_add_foreign_key.sql](SqlServer/02_add_foreign_key.sql) |

For compatibility/custom mappings, use the corresponding base DDL and edit the
maintenance script headers to match; the links above use default mappings.

## Configure and apply

1. Review the scripts against the actual schema and take the normal recovery
   precautions for a database maintenance operation. Use a maintenance identity
   with the required read/delete/DDL permissions. Pause journal writers,
   deleters, imports, and external retention jobs through cleanup and FK
   installation. Do not use an uncommitted import as evidence of orphaned data.
2. Copy the three scripts for the provider into the deployment's migration
   assets and edit their configuration headers consistently. Run against the
   intended database. Both tables must use the configured shared schema.
3. Run `01` unchanged to audit. Confirm the reported rows are disposable orphan
   index entries. Counts and samples may scan large tables; plan the window and
   inspect the relevant indexes/query plans first.
4. Set `execute_cleanup` to `true` (PostgreSQL) or `@execute_cleanup` to `1`
   (SQL Server), choose a batch size, and run `01`. Repeat until the reported
   remaining orphan count is zero. Each invocation performs one bounded DELETE.
   Use autocommit, without a surrounding transaction, so each batch commits
   independently. Completed batches remain committed if a later invocation
   fails; the same script can be resumed.
5. Run `02` using the deployment/migration account. This step scans existing
   data and can hold table/schema locks. The scripts bound lock waits to five
   seconds; PostgreSQL also sets a 30-minute validation timeout. Review these
   values for the maintenance window. A lock timeout or validation failure
   must be investigated before retrying.
6. Resume normal application operation with `auto-initialize = false` when
   schema management is external. Keep the plugin's explicit tag deletion:
   databases that have not adopted this migration still depend on it.

### Mapping configuration

| Setting | Default PostgreSQL | Default SQL Server | Compatibility difference |
| --- | --- | --- | --- |
| `schema_name` | `public` | `dbo` | Use the deployment's configured schema. |
| `journal_table` | `journal` | `journal` | PostgreSQL: `event_journal`; SQL Server: `EventJournal`. |
| `journal_ordering_column` | `ordering` | `ordering` | SQL Server compatibility: `Ordering`. |
| `tag_table` | `tags` | `tags` | Use configured tag-table name. |
| `tag_ordering_id_column` | `ordering_id` | `ordering_id` | Use configured tag ordering column. |
| `constraint_name` (`02`/`03`) | `tags_ordering_id_journal_ordering_fk` | Same | Keep consistent between add/drop scripts. |

PostgreSQL headers use `psql` `\set` commands; edit those declarations in the
deployment copy. They overwrite command-line `-v` values with the same names.
Values are passed as data and identifiers are quoted by the scripts. SQL Server
headers use T-SQL declarations; escape apostrophes in string values by doubling
them. Do not replace identifier quoting with raw string concatenation.

For example, after editing the copies, use an existing configured PostgreSQL
service or SQL Server connection. Supply credentials through the client's
normal credential mechanism, outside the scripts and source control:

```bash
psql "service=maintenance" -X -v ON_ERROR_STOP=1 -f PostgreSql/01_cleanup_orphan_tags.sql
psql "service=maintenance" -X -v ON_ERROR_STOP=1 -f PostgreSql/02_add_foreign_key.sql

sqlcmd -S "$SQLSERVER_HOST" -d "$DATABASE" -U "$SQLSERVER_USER" -b -i SqlServer/01_cleanup_orphan_tags.sql
sqlcmd -S "$SQLSERVER_HOST" -d "$DATABASE" -U "$SQLSERVER_USER" -b -i SqlServer/02_add_foreign_key.sql
```

PostgreSQL scripts require `psql`; the backslash commands are not server SQL for
an ORM's raw-SQL executor. SQL Server scripts can run through `sqlcmd` or a SQL
client that executes the complete batch. Keep the script's transaction handling
intact rather than splitting its statements into separately committed commands.

## Validation and rollback

FK performance depends on the insert strategy. The existing journal setting
`prefer-parameters-on-multirow-insert = true` substantially improved SQL Server
FK insert throughput in the [benchmark rerun](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/610).
See the [summary in the main README](../../README.md#measured-fk-overhead) for
both v1.5 and dev. The dev parent-insert optimization from #580 does not remove
PostgreSQL's or the delete path's FK cost. The measured v1.5 baseline `c8413c6`
predates the batching backport; the recorded stable figures do not measure this
new implementation. See the [current batching configuration](../../README.md#tag-table-insert-batching).
Benchmark the deployment's absolute before/after throughput: the setting can
also slow some no-FK workloads, and PostgreSQL showed much smaller gains. It
does not disable FK enforcement or change transaction boundaries.

The add scripts verify the configured relationship rather than trusting a
constraint name alone. A matching validated/enforced FK can be rerun. SQL Server
revalidates and enables a matching disabled/untrusted FK. PostgreSQL refuses
disabled or replica-only enforcement triggers, requiring the operator to
investigate. Neither script replaces an incompatible constraint automatically.

`03` is an intentional DDL rollback. It removes future cascade/enforcement,
preserves surviving rows and custom columns, and cannot recreate rows removed
by cleanup or cascade. Parent-row deletion after installation changes behavior:
ordinary DELETE now also removes its dependent tag rows.

Partition switching, inheritance, archival operations, and TRUNCATE need separate
provider-specific review. These scripts do not establish a retention policy or
support every existing custom constraint/trigger layout. MySQL and SQLite
migrations are outside this script set. Existing generated DDL has index-layout
differences from runtime initialization; these scripts validate the relationship
against the live schema and do not reconcile unrelated indexes or column layouts.

## Repeatable integration checks

Run against disposable local PostgreSQL and SQL Server containers that include
`psql` and `/opt/mssql-tools18/bin/sqlcmd`. The runner creates and removes its
own uniquely named databases; it uses the containers' existing credential
environment and never writes credentials to results.

```bash
python3 scripts/TagTableForeignKey/verify.py \
  --postgres-container codex-fkbench-postgres \
  --sqlserver-container codex-fkbench-sqlserver
```

The checks exercise shipped default and compatibility DDL plus custom quoted
identifiers and a custom column. They check dry-run preservation, invalid batch
sizes, bounded cleanup/resumption, soft-deleted-parent retention, orphan rejection,
FK rejection/cascade, transaction rollback, repeated application, enforcement
state, conflicting definitions, and DDL rollback. Expected failures must include
the relevant diagnostic, so an unrelated SQL failure cannot satisfy a check.

For actual-plugin container insert/delete measurements, see the
[foreign-key benchmark harness and raw results in PR #610](https://github.com/akkadotnet/Akka.Persistence.Sql/pull/610).
