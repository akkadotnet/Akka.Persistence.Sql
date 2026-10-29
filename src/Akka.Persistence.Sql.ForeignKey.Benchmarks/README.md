# Tag ordering foreign-key benchmark

This standalone executable compares the journal schema initialized by the current Akka.Persistence.Sql source library with the same schema plus `tags(ordering_id) REFERENCES journal(ordering) ON DELETE CASCADE`. It writes through Akka.Persistence.Sql `UntypedPersistentActor` persistence and the configured SQL journal; it does not time raw SQL inserts. The delete case uses the plugin's `DeleteMessages` path, which explicitly deletes tag rows, so it measures the FK alongside the existing cleanup behavior. In each pair, both databases are initialized by the same library version/configuration and differ only by the added foreign key.

The original measurements use literal multi-row SQL (the default); see the parameterized tuning results below for a tested comparison.

The primary workload covers tagged inserts with two and four tags per event and batch sizes 1 and 50, plus ranged deletes over 50 persistence IDs with 100 events each. Insert cases use 2,000 measured events after 200 untimed warmup events per persistence ID. Delete seeding and a warmup delete are outside the timed interval. Five paired repetitions alternate baseline/FK order. Each run verifies journal and tag row counts; setup also verifies the FK exists and rejects an orphan tag. Persistence IDs and initial rows match across the paired databases. The result JSON includes raw per-run throughput and batch-operation/per-event-normalized p95 latency, medians, and throughput coefficient of variation.

The benchmark config sets `tag-write-mode = TagTable`; `db-round-trip-max-tag-batch-size` remains at the library default of 1,000. It uses the provider mappings `PostgreSQL.15` and `SqlServer.2022`. `CurrentEventsByTag`/`EventsByTag` concurrency and lock-wait instrumentation are not part of this primary insert/delete measurement. Per-event latency is each batch's elapsed time divided by the batch size; it is a normalized batch measure, not individual event timing.

PostgreSQL measurements used source base revision `9c133f339fe429df002e05f01e40f75e20dc09b3` and timed source hash `2d9f579060ec4eecf190a2a4b7419cd4c5966fbce80c3f23100a465b3e2b9b59`. SQL Server used timed source hash `30360a1c43ef65951762f2fdd7a16942676e480a14409e32f9b1cfc28a66132e` after metadata and failure-path cleanup updates; a later indentation-only edit did not affect the workload. Both runs used the same workload and timed intervals; the reported PostgreSQL image metadata was corrected after timing from the live disposable-container image inspection. See result files for exact provider names, engine versions, sanitized image labels, image digests, runtime, source hashes, and raw repetitions.

## 2026-10-29 rerun on current v1.5

This is a fresh run of the unchanged workload after rebasing onto stable commit
`c8413c6` (#608). The measured harness commit is `20d0c2c`; source and compiled
library hashes, image identities, timestamps, and shared-host observations are in
the [manifest](results/rerun-2026-10-29/manifest.json). The library uses LinqToDB
5.4.1.9. The benchmark ran on .NET 8.0.8 with PostgreSQL 16.15 and SQL Server
16.0.4185.3. The disposable SQL Server container used a 2,048 MB memory limit.

All four provider/configuration matrices completed: **200 individual measurements
in 100 matched pairs across 20 cases**. Journal/tag counts, retained deletion
boundaries, and FK violation probes passed. Every observation is retained. Rates,
medians, p95 summaries, and coefficients of variation were recomputed from the raw
records. Setup, warmup, verification, and database cleanup remain outside timing.

Median events/second; change is the ratio of the FK and no-FK medians, as in the
historical tables. `literal` and `parameterized` select the existing
`prefer-parameters-on-multirow-insert` option.

| Engine | Insert SQL | Operation | Tags/event | Batch/boundary | No FK/s | FK/s | Change |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL | literal | Insert | 2 | 1 | 195 | 194 | -0.6% |
| PostgreSQL | literal | Insert | 2 | 50 | 3,003 | 2,387 | -20.5% |
| PostgreSQL | literal | Insert | 4 | 1 | 264 | 231 | -12.5% |
| PostgreSQL | literal | Insert | 4 | 50 | 2,892 | 2,179 | -24.7% |
| PostgreSQL | literal | Delete | 4 | 100 | 16,450 | 10,175 | -38.1% |
| PostgreSQL | parameterized | Insert | 2 | 1 | 275 | 249 | -9.3% |
| PostgreSQL | parameterized | Insert | 2 | 50 | 3,123 | 2,504 | -19.8% |
| PostgreSQL | parameterized | Insert | 4 | 1 | 250 | 213 | -14.6% |
| PostgreSQL | parameterized | Insert | 4 | 50 | 2,659 | 1,836 | -30.9% |
| PostgreSQL | parameterized | Delete | 4 | 100 | 18,275 | 10,847 | -40.6% |
| SQL Server | literal | Insert | 2 | 1 | 122 | 95 | -21.8% |
| SQL Server | literal | Insert | 2 | 50 | 1,810 | 1,129 | -37.6% |
| SQL Server | literal | Insert | 4 | 1 | 116 | 93 | -20.1% |
| SQL Server | literal | Insert | 4 | 50 | 1,052 | 1,003 | -4.6% |
| SQL Server | literal | Delete | 4 | 100 | 5,257 | 3,970 | -24.5% |
| SQL Server | parameterized | Insert | 2 | 1 | 127 | 128 | +0.6% |
| SQL Server | parameterized | Insert | 2 | 50 | 2,078 | 2,039 | -1.9% |
| SQL Server | parameterized | Insert | 4 | 1 | 120 | 118 | -1.8% |
| SQL Server | parameterized | Insert | 4 | 50 | 1,068 | 954 | -10.7% |
| SQL Server | parameterized | Delete | 4 | 100 | 3,452 | 2,611 | -24.4% |

Raw records: [PostgreSQL literal](results/rerun-2026-10-29/postgresql-literal.json),
[PostgreSQL parameterized](results/rerun-2026-10-29/postgresql-parameterized.json),
[SQL Server literal](results/rerun-2026-10-29/sqlserver-literal.json), and
[SQL Server parameterized](results/rerun-2026-10-29/sqlserver-parameterized.json).
The [20-case summary CSV](results/rerun-2026-10-29/summary.csv) also includes
within-pair change ranges, variation, operation p95, and normalized batch latency.
Normalized per-event latency remains batch elapsed time divided by event count.

Absolute rates differ substantially from the historical home-PC measurements.
The historical runs used .NET 8.0.26; this run used 8.0.8, a shared development
host, and the container configuration above. These are fresh within-run FK/no-FK
comparisons, not a controlled before/after measurement of #608 or an explanation
of the absolute-rate difference. Configuration-mode runs were sequential, so
cross-mode comparisons also include possible host/time effects. Small gaps need
to be interpreted alongside the retained run-to-run variation.

This run is on **v1.5**. It does not include #580's LinqToDB 6 upgrade or enable
its opt-in parent-insert optimization on dev. It cannot establish whether that
optimization offsets FK costs. The earlier measurements below remain unchanged.

## Historical results

Median event throughput from the included five-run provider results. Change is
`100 * (FK median / baseline median - 1)`; a negative value means lower throughput.

| Provider | Workload | Tags/event | Batch | Baseline/s | FK/s | Change |
|---|---|---:|---:|---:|---:|---:|
| PostgreSQL 16.15 | Insert | 2 | 1 | 2,533 | 2,056 | -18.8% |
| PostgreSQL 16.15 | Insert | 2 | 50 | 14,038 | 11,293 | -19.6% |
| PostgreSQL 16.15 | Insert | 4 | 1 | 2,346 | 1,976 | -15.8% |
| PostgreSQL 16.15 | Insert | 4 | 50 | 12,047 | 9,025 | -25.1% |
| PostgreSQL 16.15 | Delete range | 4 | 100 | 120,616 | 66,851 | -44.6% |
| SQL Server 16.0.4185.3 | Insert | 2 | 1 | 1,300 | 558 | -57.1% |
| SQL Server 16.0.4185.3 | Insert | 2 | 50 | 10,444 | 4,512 | -56.8% |
| SQL Server 16.0.4185.3 | Insert | 4 | 1 | 1,258 | 525 | -58.3% |
| SQL Server 16.0.4185.3 | Insert | 4 | 50 | 8,347 | 2,559 | -69.3% |
| SQL Server 16.0.4185.3 | Delete range | 4 | 100 | 20,555 | 11,062 | -46.2% |

Each delete case seeds 50 actors with 100 events each, then deletes 99 journal events per actor (4,950 physical deletions total); the sequence-100 boundary event remains. Throughput coefficient of variation and p95 latency are in the [PostgreSQL](results/postgresql.json) and [SQL Server](results/sqlserver.json) raw JSON. The database engine and Akka runtime ran on a shared development host, so these figures are directional and should be confirmed on representative deployment hardware before release decisions.

## Historical parameterized insert tuning

The initial results above use the library default
`prefer-parameters-on-multirow-insert = false`. They measure that insert strategy
with and without the FK. A subsequent configuration experiment retains enforced
FKs and the same transactions while changing the existing option to `true`:

```hocon
akka.persistence.journal.sql {
    prefer-parameters-on-multirow-insert = true
}
```

Four fresh provider/mode runs repeat the complete five-pair matrix. Each mode
has its own no-FK/FK comparison with identical data/configuration in the pair;
mode runs occur sequentially on the same shared host. Original results remain
unchanged. The added `--prefer-parameters true|false` benchmark option defaults
to `false` and records `PreferParametersOnMultiRowInsert` in the raw JSON.

Median persisted events/second from those fresh runs:

| Provider | Tags/event | Batch | Default no FK | Default FK | Parameterized no FK | Parameterized FK | FK change within parameterized mode |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQL Server | 2 | 1 | 1,245 | 566 | 1,465 | 1,520 | +3.8% |
| SQL Server | 2 | 50 | 10,648 | 4,802 | 9,050 | 8,825 | -2.5% |
| SQL Server | 4 | 1 | 1,257 | 543 | 1,407 | 1,428 | +1.5% |
| SQL Server | 4 | 50 | 8,594 | 2,726 | 4,683 | 4,633 | -1.0% |
| PostgreSQL | 2 | 1 | 2,525 | 2,073 | 2,481 | 2,071 | -16.5% |
| PostgreSQL | 2 | 50 | 14,904 | 12,021 | 14,725 | 12,298 | -16.5% |
| PostgreSQL | 4 | 1 | 2,454 | 2,040 | 2,476 | 1,986 | -19.8% |
| PostgreSQL | 4 | 50 | 13,343 | 9,234 | 12,477 | 9,690 | -22.3% |

SQL Server benefits substantially: the existing setting raises FK throughput
from 566 to 1,520 events/s for two tags and individual writes, and from 4,802 to
8,825 for two tags and batches of 50. Within parameterized mode, the FK/no-FK
insert differences range from -2.5% to +3.8%, small relative to variation in
some scenarios. Positive deltas do not establish that an FK improves speed.

Absolute throughput still matters. With four tags and batches of 50, parameterized
FK throughput improves from 2,726 to 4,633 events/s, while the default no-FK control
achieves 8,594. Parameterization also slows the no-FK control in this case to
4,683. Thus a small FK gap within the tuned mode does not mean all original
performance has been recovered.

PostgreSQL gains much less from this setting: its parameterized-mode FK insert
penalties remain 16.5–22.3%. The four-tag batch-50 FK case rises from 9,234 to
9,690 events/s. Delete ordering is unchanged; parameterized-mode delete penalties
remain 44.6% on SQL Server and 45.6% on PostgreSQL.

Raw evidence: [SQL Server literal](results/tuning/sqlserver-literal.json),
[SQL Server parameterized](results/tuning/sqlserver-parameterized.json),
[PostgreSQL literal](results/tuning/postgresql-literal.json), and
[PostgreSQL parameterized](results/tuning/postgresql-parameterized.json).
Each includes the exact source hash, engine/image metadata, flag, per-run
counts, p95 operation and normalized batch latency, and variation. All 200 new
individual measurements passed the existing row/tag and deletion checks; each
FK database rejected an orphan insert before measurement.

The data supports trying this existing setting with the FK. Reduced SQL text
variation/compilation is a hypothesis for the improvement; compilation costs
were not profiled, so this experiment does not establish that mechanism.
The largest measured tag insert has 200 rows, below SQL Server's parameterized
batch-splitting threshold for four tag columns. Larger batches, mixed workloads,
concurrency, and deployment hardware need their own measurements.

Further candidates remain unmeasured: native provider bulk loading with
constraint checks retained, and deleting child tags before journal parents in
the same transaction to reduce cascade work. The existing primary keys already
index both FK endpoints.

To reproduce the tuned runs, use the commands below with
`--prefer-parameters true` and an output path under `results/tuning/` (create the
output directory first). Keep fresh literal controls using
`--prefer-parameters false` for an absolute comparison.

## Run

Build with the repository SDK and dependencies:

```sh
dotnet build src/Akka.Persistence.Sql.ForeignKey.Benchmarks/Akka.Persistence.Sql.ForeignKey.Benchmarks.csproj -c Release
```

Run against disposable local PostgreSQL or SQL Server instances using an administrative connection string with permission to create/drop databases. The harness rejects non-loopback hosts and creates uniquely named databases, dropping only those names on completion. Supply credentials through a local secret mechanism and do not commit them or include connection strings in results. Supply the sanitized image label and image ID digest observed for the actual container; labels must not contain registry/host names:

```sh
dotnet run --project src/Akka.Persistence.Sql.ForeignKey.Benchmarks -c Release -- \
  --provider postgresql --admin-connection-string "$PG_ADMIN_CONNECTION_STRING" \
  --image postgres:16-alpine --image-digest "$(docker inspect --format '{{.Image}}' codex-fkbench-postgres)" \
  --source-sha256 "$(sha256sum src/Akka.Persistence.Sql.ForeignKey.Benchmarks/Program.cs | cut -d' ' -f1)" \
  --output src/Akka.Persistence.Sql.ForeignKey.Benchmarks/results/postgresql.json

dotnet run --project src/Akka.Persistence.Sql.ForeignKey.Benchmarks -c Release -- \
  --provider sqlserver --admin-connection-string "$SQLSERVER_ADMIN_CONNECTION_STRING" \
  --image sqlserver:2022-CU18-ubuntu-22.04 --image-digest "$(docker inspect --format '{{.Image}}' codex-fkbench-sqlserver)" \
  --source-sha256 "$(sha256sum src/Akka.Persistence.Sql.ForeignKey.Benchmarks/Program.cs | cut -d' ' -f1)" \
  --output src/Akka.Persistence.Sql.ForeignKey.Benchmarks/results/sqlserver.json
```

The included result files are local disposable-container measurements on a shared development host, not isolated hardware or production workload predictions. Results are provider-specific, and small percentage differences should be interpreted in light of observed run-to-run variation.
