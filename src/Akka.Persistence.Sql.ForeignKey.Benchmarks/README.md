# Tag ordering foreign-key benchmark

This standalone executable compares the journal schema initialized by the current Akka.Persistence.Sql source library with the same schema plus `tags(ordering_id) REFERENCES journal(ordering) ON DELETE CASCADE`. It writes through Akka.Persistence.Sql `UntypedPersistentActor` persistence and the configured SQL journal; it does not time raw SQL inserts. The delete case uses the plugin's `DeleteMessages` path, which explicitly deletes tag rows, so it measures the FK alongside the existing cleanup behavior. In each pair, both databases are initialized by the same library version/configuration and differ only by the added foreign key.

The original measurements use literal multi-row SQL (the default); see the parameterized tuning results below for a tested comparison.

Payloads are small `Int32` values. Each insert case sends one batch at a time to one persistent actor; concurrent writers and large payloads are outside this workload.

The primary workload covers tagged inserts with two and four tags per event and batch sizes 1 and 50, plus ranged deletes over 50 persistence IDs with 100 events each. Insert cases use 2,000 measured events after 200 untimed warmup events per persistence ID. Warmup uses batches of 50 even for single-write cases, so the first timed single-row query shape can still incur compilation/setup. Delete seeding and a warmup delete are outside the timed interval. Five paired repetitions alternate baseline/FK order. Each run verifies journal and tag row counts; setup also verifies the FK exists and rejects an orphan tag. Persistence IDs and initial rows match across the paired databases. The result JSON includes raw per-run throughput and batch-operation/per-event-normalized p95 latency, medians, and throughput coefficient of variation.

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

| Engine | Tag SQL | Operation | Tags/event | Batch/boundary | No FK/s | FK/s | Change |
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

Absolute rates differ substantially from the historical measurements.
The historical runs used .NET 8.0.26; this run used 8.0.8, a shared development
host, and the container configuration above. These are fresh within-run FK/no-FK
comparisons, not a controlled before/after measurement of #608 or an explanation
of the absolute-rate difference. Configuration-mode runs were sequential, so
cross-mode comparisons also include possible host/time effects. Small gaps need
to be interpreted alongside the retained run-to-run variation.

This run is on **v1.5**. It does not include #580's LinqToDB 6 upgrade or enable
its opt-in parent-insert optimization on dev. It cannot establish whether that
optimization offsets FK costs by itself; see the dev matrix below. Historical
measurements remain unchanged.

## 2026-10-29 rerun on current dev

This matrix references actual `dev` commit `1f23b84`,
including merged #580 and #609, using harness commit `de68751`.
It uses LinqToDB 6.5.1, Akka 1.6.0-beta3, and .NET 10.0.12.
Provider images and the SQL Server 2,048 MB memory cap match the stable rerun.
The [dev manifest](results/rerun-2026-10-29/dev/manifest.json) retains source and
compiled library/harness hashes, resolved dependencies, container image IDs,
timestamps, and host observations.

All 12 matrices completed: **600 individual measurements in 300 matched pairs
across 60 cases**. Each provider runs all three parent modes (`off`,
`parameterized`, `inline`) with both tag SQL settings. FK violation probes and
all existing row/tag/deletion checks passed. All observations are retained;
throughput, medians, variation, and medians of the recorded per-run p95 values
were independently recomputed. Per-operation latency samples are not retained,
so individual runs' p95 values cannot be independently reconstructed from JSON.
Together with stable, the rerun covers **800 measurements and 400 matched pairs**.

The harness adds only mode selection, result metadata, and a guard against using
an unsupported parent mode on stable. Timed loops, event counts, warmups, payload,
paired ordering, and correctness checks are unchanged from the stable harness
commit `20d0c2c`. These runs exercise the real plugin with an enforced cascading
FK; they do not use a raw-SQL substitute or disable constraint checks. #580's
parent optimization batches INSERT ... OUTPUT/RETURNING; this matrix does not
measure native PostgreSQL COPY or SQL Server SqlBulkCopy.

The SQL Server parameterized-parent/parameterized-tag batch cases have nearly
equal FK/no-FK medians (two tags: 2,745 vs 2,750 events/s; four tags: 1,868 vs
1,909). Their small positive differences are within the observed variation,
not evidence that the FK improves speed. With the same parent mode and literal
tags, FK overhead is 44.5% and 56.4%. PostgreSQL's parameterized-parent/tag batch
cases still show 30.9% and 31.6% overhead. Delete overhead in that configuration
is 28.0% on SQL Server and 38.0% on PostgreSQL. Provider and configuration matter;
parent batching does not uniformly eliminate FK cost.

Median events/second; `Tag SQL` selects `prefer-parameters-on-multirow-insert`,
while `Parent` selects #580's `tagtable-asqueryable-insert-mode`.

| Engine | Parent | Tag SQL | Operation | Tags/event | Batch/boundary | No FK/s | FK/s | Change |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL | off | literal | Insert | 2 | 1 | 249 | 220 | -11.9% |
| PostgreSQL | off | literal | Insert | 2 | 50 | 2,384 | 1,791 | -24.9% |
| PostgreSQL | off | literal | Insert | 4 | 1 | 198 | 186 | -5.7% |
| PostgreSQL | off | literal | Insert | 4 | 50 | 2,021 | 1,554 | -23.1% |
| PostgreSQL | off | literal | Delete | 4 | 100 | 18,014 | 9,349 | -48.1% |
| PostgreSQL | off | parameterized | Insert | 2 | 1 | 197 | 199 | +1.0% |
| PostgreSQL | off | parameterized | Insert | 2 | 50 | 2,956 | 2,518 | -14.8% |
| PostgreSQL | off | parameterized | Insert | 4 | 1 | 289 | 254 | -12.3% |
| PostgreSQL | off | parameterized | Insert | 4 | 50 | 2,711 | 2,101 | -22.5% |
| PostgreSQL | off | parameterized | Delete | 4 | 100 | 16,493 | 10,230 | -38.0% |
| PostgreSQL | parameterized | literal | Insert | 2 | 1 | 244 | 228 | -6.7% |
| PostgreSQL | parameterized | literal | Insert | 2 | 50 | 6,100 | 4,006 | -34.3% |
| PostgreSQL | parameterized | literal | Insert | 4 | 1 | 241 | 224 | -7.2% |
| PostgreSQL | parameterized | literal | Insert | 4 | 50 | 5,884 | 3,429 | -41.7% |
| PostgreSQL | parameterized | literal | Delete | 4 | 100 | 17,023 | 9,479 | -44.3% |
| PostgreSQL | parameterized | parameterized | Insert | 2 | 1 | 238 | 224 | -5.9% |
| PostgreSQL | parameterized | parameterized | Insert | 2 | 50 | 7,042 | 4,866 | -30.9% |
| PostgreSQL | parameterized | parameterized | Insert | 4 | 1 | 214 | 181 | -15.5% |
| PostgreSQL | parameterized | parameterized | Insert | 4 | 50 | 5,384 | 3,683 | -31.6% |
| PostgreSQL | parameterized | parameterized | Delete | 4 | 100 | 19,842 | 12,301 | -38.0% |
| PostgreSQL | inline | literal | Insert | 2 | 1 | 236 | 218 | -7.5% |
| PostgreSQL | inline | literal | Insert | 2 | 50 | 7,544 | 5,136 | -31.9% |
| PostgreSQL | inline | literal | Insert | 4 | 1 | 182 | 169 | -7.3% |
| PostgreSQL | inline | literal | Insert | 4 | 50 | 5,571 | 3,674 | -34.0% |
| PostgreSQL | inline | literal | Delete | 4 | 100 | 18,576 | 10,803 | -41.8% |
| PostgreSQL | inline | parameterized | Insert | 2 | 1 | 257 | 234 | -9.0% |
| PostgreSQL | inline | parameterized | Insert | 2 | 50 | 6,398 | 5,123 | -19.9% |
| PostgreSQL | inline | parameterized | Insert | 4 | 1 | 181 | 171 | -5.6% |
| PostgreSQL | inline | parameterized | Insert | 4 | 50 | 5,144 | 3,703 | -28.0% |
| PostgreSQL | inline | parameterized | Delete | 4 | 100 | 12,835 | 11,430 | -11.0% |
| SQL Server | off | literal | Insert | 2 | 1 | 102 | 91 | -11.0% |
| SQL Server | off | literal | Insert | 2 | 50 | 1,796 | 1,395 | -22.4% |
| SQL Server | off | literal | Insert | 4 | 1 | 102 | 82 | -19.9% |
| SQL Server | off | literal | Insert | 4 | 50 | 464 | 423 | -8.9% |
| SQL Server | off | literal | Delete | 4 | 100 | 3,207 | 3,372 | +5.1% |
| SQL Server | off | parameterized | Insert | 2 | 1 | 98 | 107 | +8.9% |
| SQL Server | off | parameterized | Insert | 2 | 50 | 1,283 | 1,229 | -4.2% |
| SQL Server | off | parameterized | Insert | 4 | 1 | 103 | 89 | -13.3% |
| SQL Server | off | parameterized | Insert | 4 | 50 | 1,245 | 1,216 | -2.3% |
| SQL Server | off | parameterized | Delete | 4 | 100 | 5,108 | 4,111 | -19.5% |
| SQL Server | parameterized | literal | Insert | 2 | 1 | 122 | 92 | -24.5% |
| SQL Server | parameterized | literal | Insert | 2 | 50 | 3,370 | 1,871 | -44.5% |
| SQL Server | parameterized | literal | Insert | 4 | 1 | 112 | 92 | -18.6% |
| SQL Server | parameterized | literal | Insert | 4 | 50 | 2,879 | 1,256 | -56.4% |
| SQL Server | parameterized | literal | Delete | 4 | 100 | 4,580 | 3,609 | -21.2% |
| SQL Server | parameterized | parameterized | Insert | 2 | 1 | 122 | 125 | +2.6% |
| SQL Server | parameterized | parameterized | Insert | 2 | 50 | 2,745 | 2,750 | +0.2% |
| SQL Server | parameterized | parameterized | Insert | 4 | 1 | 101 | 103 | +1.7% |
| SQL Server | parameterized | parameterized | Insert | 4 | 50 | 1,868 | 1,909 | +2.2% |
| SQL Server | parameterized | parameterized | Delete | 4 | 100 | 4,622 | 3,329 | -28.0% |
| SQL Server | inline | literal | Insert | 2 | 1 | 95 | 76 | -19.5% |
| SQL Server | inline | literal | Insert | 2 | 50 | 2,087 | 1,373 | -34.2% |
| SQL Server | inline | literal | Insert | 4 | 1 | 97 | 79 | -18.5% |
| SQL Server | inline | literal | Insert | 4 | 50 | 1,917 | 978 | -49.0% |
| SQL Server | inline | literal | Delete | 4 | 100 | 4,104 | 1,793 | -56.3% |
| SQL Server | inline | parameterized | Insert | 2 | 1 | 77 | 91 | +17.5% |
| SQL Server | inline | parameterized | Insert | 2 | 50 | 2,364 | 2,323 | -1.7% |
| SQL Server | inline | parameterized | Insert | 4 | 1 | 100 | 64 | -36.3% |
| SQL Server | inline | parameterized | Insert | 4 | 50 | 1,306 | 1,220 | -6.6% |
| SQL Server | inline | parameterized | Delete | 4 | 100 | 4,331 | 3,302 | -23.8% |

The [60-case CSV](results/rerun-2026-10-29/dev/summary.csv) includes paired change
ranges, coefficients of variation, and operation/normalized p95 summaries.
[Mode-versus-off comparisons](results/rerun-2026-10-29/dev/mode-vs-off.csv) compare
the same provider, tag setting, tags/event, and batch/boundary within dev. Every
raw result file is listed with its SHA-256 in the dev manifest.

Interpretation limits apply to both reruns. Host contention was substantial
(stable load average reached 22.5 on eight logical CPUs), and some cases have
large run-to-run variation. Mode matrices ran at different times; differences
between them include possible host/time effects. Stable and dev also differ in
.NET and Akka versions, so their absolute rates cannot isolate #580's effect.
The data does not provide confidence intervals, a clean hardware comparison,
query concurrency coverage, or production throughput predictions. Five paired
repetitions and a shared host are insufficient to treat small changes as wins.
Deletion timing includes the existing parent-first delete and subsequent explicit
tag cleanup alongside cascade enforcement; it is not a benchmark of a redesigned
child-first deletion algorithm.

For the customer remediation, keep orphan cleanup independent of FK adoption
and keep the FK migration operator-applied. These results do not justify making
FK enforcement a production default. Stable customers can measure the existing
tag parameterization option; #580's parent modes are dev-only and require a
separate compatibility/release decision before recommending them on v1.5.
Native bulk loading and child-first deletion remain separate, unmeasured options.

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

### Measuring another source checkout

The default build references this checkout's stable library and targets .NET 8.
To measure a clean `dev` checkout containing #580, reference that project and
match its .NET 10 target using a .NET 10 SDK. Keep the source commit fixed
throughout the matrix:

```sh
dotnet build src/Akka.Persistence.Sql.ForeignKey.Benchmarks/Akka.Persistence.Sql.ForeignKey.Benchmarks.csproj \
  -c Release -p:BenchmarkTargetFramework=net10.0 \
  -p:BenchmarkSqlProject="$DEV_CHECKOUT/src/Akka.Persistence.Sql/Akka.Persistence.Sql.csproj" \
  -o "$DEV_BENCH_OUTPUT"
```

Run the resulting DLL directly with the same provider/connection/image/source
arguments shown above. Add `--parent-insert-mode off`, `parameterized`, or
`inline`. This controls `tagtable-asqueryable-insert-mode` independently of
`--prefer-parameters`, which controls tag multi-row SQL. Non-off parent modes
are rejected before database creation when the referenced library lacks #580.
Schema version 2 records the requested parent mode; version 1 historical/stable
records predate that option and use the original parent-insert path.

For each provider, run all six combinations of parent mode and tag SQL with
fresh uniquely named databases. Each invocation still produces five matched
FK/no-FK pairs for each of the five scenarios. Keep invocations sequential and
avoid builds or tests during timing. Preserve exact library/harness commits,
compiled assembly hashes, resolved dependencies, runtime, container image IDs,
SQL Server memory cap, and all raw observations alongside the results.

The included result files are local disposable-container measurements on a shared development host, not isolated hardware or production workload predictions. Results are provider-specific, and small percentage differences should be interpreted in light of observed run-to-run variation.
