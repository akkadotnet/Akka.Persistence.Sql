---
uid: sql-performance
title: Performance Benchmarks 
---

# Performance Benchmarks

## Journal Events Persistence Performance

Starting in August 29th, 2024, performance calculations are based on 100 test iteration with Z-score outlier rejection (sigma 2).

All values are the median of all non-rejected measurements.

### Microsoft SQL Server 2022 (August 29th, 2024)

```
Windows 10 (10.0.19045.4780/22H2/2022Update)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores, 64 GB RAM
.NET SDK 8.0.304
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
```

Databases running on Docker for Desktop with WSL2 integration with 10 virtual CPU and 8 GB RAM

All numbers are in msg/sec.

| Test            | SqlServer | SqlServer<br/>Batching | Sql CSV | Sql TagTable | CSV vs Normal | CSV vs Batching | Tag Table vs Normal | Tag Table vs Batching |
|:----------------|----------:|-----------------------:|--------:|-------------:|--------------:|----------------:|--------------------:|----------------------:|
| Persist         | 233       | 232                    | 230     | 228          | \-1.29%       | \-0.86%         | \-2.15%             | \-1.72%               |
| PersistAsync    | 1315      | 1611                   | 25347   | 27541        | 1827.53%      | 1473.37%        | 1994.37%            | 1609.56%              |
| PersistAll      | 1393      | 1412                   | 6438    | 6446         | 362.17%       | 355.95%         | 362.74%             | 356.52%               |
| PersistAllAsync | 4718      | 1569                   | 25720   | 26247        | 445.15%       | 1539.26%        | 456.32%             | 1572.85%              |
| PersistGroup10  | 795       | 774                    | 1236    | 1239         | 55.47%        | 59.69%          | 55.85%              | 60.08%                |
| PersistGroup25  | 1024      | 1030                   | 2440    | 2357         | 138.28%       | 136.89%         | 130.18%             | 128.83%               |
| PersistGroup50  | 1209      | 1254                   | 3832    | 3827         | 216.96%       | 205.58%         | 216.54%             | 205.18%               |
| PersistGroup100 | 1277      | 1546                   | 6050    | 6094         | 373.77%       | 291.33%         | 377.21%             | 294.18%               |
| PersistGroup200 | 1284      | 1611                   | 9128    | 9275         | 610.90%       | 466.60%         | 622.35%             | 475.73%               |
| PersistGroup400 | 1287      | 2567                   | 9787    | 9907         | 660.45%       | 281.26%         | 669.77%             | 285.94%               |
| Recovering      | 94935     | 101857                 | 71970   | 72191        | \-24.19%      | \-29.34%        | \-23.96%            | \-29.13%              |
| RecoveringTwo   | 43232     | 43486                  | 43477   | 43574        | 0.57%         | \-0.02%         | 0.79%               | 0.20%                 |
| RecoveringFour  | 51886     | 52082                  | 51909   | 52001        | 0.04%         | \-0.33%         | 0.22%               | \-0.16%               |
| Recovering8     | 57813     | 57934                  | 57607   | 57591        | \-0.36%       | \-0.56%         | \-0.38%             | \-0.59%               |

### PostgreSQL 15 (August 29th, 2024)

```
Windows 10 (10.0.19045.4780/22H2/2022Update)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores, 64 GB RAM
.NET SDK 8.0.304
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
```

Databases running on Docker for Desktop with WSL2 integration with 10 virtual CPU and 8 GB RAM

All numbers are in msg/sec.

| Test            | PostgreSql | Sql CSV | Sql TagTable | CSV vs Normal | Tag Table vs Normal |
|:----------------|-----------:|--------:|-------------:|--------------:|--------------------:|
| Persist         | 368        | 358     | 395          | \-2.72%       | 7.34%               |
| PersistAsync    | 4455       | 12549   | 12740        | 181.68%       | 185.97%             |
| PersistAll      | 1909       | 13003   | 13528        | 581.14%       | 608.64%             |
| PersistAllAsync | 9557       | 12620   | 14040        | 32.05%        | 46.91%              |
| PersistGroup10  | 1988       | 1861    | 2007         | \-6.39%       | 0.96%               |
| PersistGroup25  | 3595       | 3653    | 3635         | 1.61%         | 1.11%               |
| PersistGroup50  | 4302       | 5150    | 5815         | 19.71%        | 35.17%              |
| PersistGroup100 | 3698       | 8668    | 9431         | 134.40%       | 155.03%             |
| PersistGroup200 | 4091       | 13115   | 14369        | 220.58%       | 251.23%             |
| PersistGroup400 | 4423       | 13545   | 14279        | 206.24%       | 222.84%             |
| Recovering      | 78549      | 96602   | 114976       | 22.98%        | 46.37%              |
| RecoveringTwo   | 37348      | 42651   | 42127        | 14.20%        | 12.80%              |
| RecoveringFour  | 52139      | 51232   | 50819        | \-1.74%       | \-2.53%             |
| Recovering8     | 57855      | 56530   | 56559        | \-2.29%       | \-2.24%             |

### Old Benchmark (Microsoft SQL Server 2019)

Tests based on AMD Ryzen 9 3900X, 32GB Ram, Windows 10 Version 22H2.
Databases running on Docker WSL2.

All numbers are in msg/sec.

| Test            | SqlServer | SqlServer<br/>Batching |    Sql | vs Normal | vs Batching |
|:----------------|----------:|-----------------------:|-------:|----------:|------------:|
| Persist         |       304 |                    299 |    496 |    63.16% |      65.89% |
| PersistAll      |      1139 |                   1275 |   7893 |   592.98% |     519.06% |
| PersistAsync    |      1021 |                   1371 |  31813 |  3015.87% |    2220.42% |
| PersistAllAsync |      2828 |                   1395 |  29634 |   947.88% |    2024.30% |
| PersistGroup10  |       986 |                   1034 |   1675 |    69.88% |      61.99% |
| PersistGroup100 |      1054 |                   1304 |   6249 |   492.88% |     379.22% |
| PersistGroup200 |       990 |                   1662 |   8086 |   716.77% |     386.52% |
| PersistGroup25  |      1034 |                   1010 |   3054 |   195.36% |     202.38% |
| PersistGroup400 |      1049 |                   2113 |   7237 |   589.59% |     242.50% |
| PersistGroup50  |       971 |                    980 |   4932 |   407.93% |     403.27% |
| Recovering      |     60516 |                  77688 |  64457 |     6.51% |     -17.03% |
| Recovering8     |    116401 |                 101549 | 103463 |   -11.12% |       1.88% |
| RecoveringFour  |     86107 |                  73218 |  66512 |   -22.76% |      -9.16% |
| RecoveringTwo   |     60730 |                  53062 |  43325 |   -28.66% |     -18.35% |

## Tag Query Performance

Tested on 3 million events database with tagged events at the end of the table (worst possible scenario).
Last performance benchmark measurement were taken on August 29th 2024

```
BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.4780/22H2/2022Update)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores, 64 GB RAM
.NET SDK 8.0.304
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
```

### Microsoft SQL Server 2019

Docker image: mcr.microsoft.com/mssql/server:2019-latest
Provider name : SqlServer.2019

| Tag Count | TagMode  | Mean         | Error      | StdDev     | Gen0       | Gen1      | Gen2     | Allocated    |
|---------- |--------- |-------------:|-----------:|-----------:|-----------:|----------:|---------:|-------------:|
|        10 | Csv      | 1,945.455 ms | 19.1740 ms | 16.0112 ms |          - |         - |        - |    458.24 KB |
|       100 | Csv      | 1,968.562 ms | 30.9421 ms | 28.9433 ms |          - |         - |        - |   1344.79 KB |
|      1000 | Csv      | 1,960.306 ms | 37.1813 ms | 45.6620 ms |  1000.0000 |         - |        - |  10462.49 KB |
|     10000 | Csv      | 2,085.894 ms | 20.2763 ms | 18.9665 ms | 12000.0000 | 3000.0000 |        - | 100992.06 KB |
|        10 | TagTable |     3.782 ms |  0.0754 ms |  0.1129 ms |    39.0625 |         - |        - |    343.84 KB |
|       100 | TagTable |     4.864 ms |  0.0970 ms |  0.1567 ms |   148.4375 |   31.2500 |        - |   1232.87 KB |
|      1000 | TagTable |    20.057 ms |  0.3781 ms |  0.4045 ms |  1250.0000 |  562.5000 |        - |  10396.16 KB |
|     10000 | TagTable |   174.057 ms |  3.4529 ms |  6.8957 ms | 12500.0000 | 2500.0000 | 500.0000 | 101172.05 KB |

**Legend:**
* Tag Count : The number of tagged events retrieved per query (events/operation)
* Tag Mode  : The tag read and write mode of the journal and query, either using a CSV formatted string or a dedicated tag table.
* Mean      : The average time to complete each query operation in milliseconds 
* Error     : Half of 99.9% confidence interval
* StdDev    : Standard deviation of all measurements
* Median    : Value separating the higher half of all measurements (50th percentile)
* Gen0      : GC Generation 0 collects per 1000 operations
* Gen1      : GC Generation 1 collects per 1000 operations
* Gen2      : GC Generation 2 collects per 1000 operations
* Allocated : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)

### Microsoft SQL Server 2022

Docker image: mcr.microsoft.com/mssql/server:2022-latest
Provider name : SqlServer.2022

| Tag Count | Tag Mode | Mean         | Error      | StdDev     | Gen0       | Gen1      | Gen2     | Allocated    |
|---------- |--------- |-------------:|-----------:|-----------:|-----------:|----------:|---------:|-------------:|
|        10 | Csv      | 2,246.063 ms | 11.9246 ms | 10.5709 ms |          - |         - |        - |    458.98 KB |
|       100 | Csv      | 2,211.957 ms | 20.3342 ms | 18.0257 ms |          - |         - |        - |   1343.73 KB |
|      1000 | Csv      | 2,219.356 ms | 26.1975 ms | 23.2234 ms |  1000.0000 |         - |        - |  10462.74 KB |
|     10000 | Csv      | 2,353.012 ms | 25.7767 ms | 24.1115 ms | 12000.0000 | 3000.0000 |        - | 100990.66 KB |
|        10 | TagTable |     3.772 ms |  0.0745 ms |  0.1620 ms |    39.0625 |         - |        - |     343.7 KB |
|       100 | TagTable |     4.937 ms |  0.0981 ms |  0.1407 ms |   148.4375 |   31.2500 |        - |   1233.44 KB |
|      1000 | TagTable |    21.374 ms |  0.4234 ms |  0.7743 ms |  1250.0000 |  562.5000 |        - |   10396.1 KB |
|     10000 | TagTable |   183.989 ms |  3.5695 ms |  8.6887 ms | 12500.0000 | 2500.0000 | 500.0000 | 101139.02 KB |

**Legend:**
* Tag Count : The number of tagged events retrieved per query (events/operation)
* Tag Mode  : The tag read and write mode of the journal and query, either using a CSV formatted string or a dedicated tag table.
* Mean      : The average time to complete each query operation in milliseconds 
* Error     : Half of 99.9% confidence interval
* StdDev    : Standard deviation of all measurements
* Median    : Value separating the higher half of all measurements (50th percentile)
* Gen0      : GC Generation 0 collects per 1000 operations
* Gen1      : GC Generation 1 collects per 1000 operations
* Gen2      : GC Generation 2 collects per 1000 operations
* Allocated : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)

### PostgreSQL

Docker image : postgres:latest
Provider name: PostgreSQL.15

| Tag Count | Tag Mode | Mean       | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2     | Allocated   |
|---------- |--------- |-----------:|-----------:|-----------:|-------------:|-----------:|----------:|---------:|------------:|
|        10 | Csv      | 136.310 ms |  2.2432 ms |  2.0983 ms |   135.824 ms |          - |         - |        - |    339.2 KB |
|       100 | Csv      | 136.946 ms |  2.6499 ms |  2.7212 ms |   136.083 ms |          - |         - |        - |  1194.83 KB |
|      1000 | Csv      | 374.642 ms |  7.3159 ms | 10.7236 ms |   376.253 ms |  1000.0000 |         - |        - | 10008.45 KB |
|     10000 | Csv      | 961.051 ms | 25.1088 ms | 74.0338 ms | 1,008.935 ms | 11000.0000 | 3000.0000 |        - |  97764.2 KB |
|        10 | TagTable |   2.505 ms |  0.0495 ms |  0.1054 ms |     2.477 ms |    39.0625 |    3.9063 |        - |   339.75 KB |
|       100 | TagTable |   3.554 ms |  0.0708 ms |  0.1447 ms |     3.526 ms |   140.6250 |   23.4375 |        - |  1197.86 KB |
|      1000 | TagTable |  16.368 ms |  0.3225 ms |  0.4193 ms |    16.269 ms |  1218.7500 |  250.0000 |        - | 10075.77 KB |
|     10000 | TagTable | 155.378 ms |  3.0692 ms |  4.4988 ms |   153.688 ms | 12333.3333 | 2333.3333 | 666.6667 |  98024.7 KB |

**Legend:**
* Tag Count : The number of tagged events retrieved per query (events/operation)
* Tag Mode  : The tag read and write mode of the journal and query, either using a CSV formatted string or a dedicated tag table.
* Mean      : The average time to complete each query operation in milliseconds 
* Error     : Half of 99.9% confidence interval
* StdDev    : Standard deviation of all measurements
* Median    : Value separating the higher half of all measurements (50th percentile)
* Gen0      : GC Generation 0 collects per 1000 operations
* Gen1      : GC Generation 1 collects per 1000 operations
* Gen2      : GC Generation 2 collects per 1000 operations
* Allocated : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)