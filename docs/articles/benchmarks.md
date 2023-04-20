---
uid: sql-performance
title: Performance Benchmarks 
---

# Performance Benchmarks

## Persistence Performance

Tests based on AMD Ryzen 9 3900X, 32GB Ram, Windows 10 Version 22H2.
Databases running on Docker WSL2.

All numbers are in msg/sec.

| Test            | SqlServer | SqlServer<br/>Batching |    Sql | vs Normal | vs Batching |
|:----------------|----------:|-----------------------:|-------:|----------:|------------:|
| Persist         |       304 |                    299 |    496 |   163.16% |     165.89% |
| PersistAll      |      1139 |                   1275 |   7893 |   692.98% |     619.06% |
| PersistAsync    |      1021 |                   1371 |  31813 |  3115.87% |    2320.42% |
| PersistAllAsync |      2828 |                   1395 |  29634 |  1047.88% |    2124.30% |
| PersistGroup10  |       986 |                   1034 |   1675 |   169.88% |     161.99% |
| PersistGroup100 |      1054 |                   1304 |   6249 |   592.88% |     479.22% |
| PersistGroup200 |       990 |                   1662 |   8086 |   816.77% |     486.52% |
| PersistGroup25  |      1034 |                   1010 |   3054 |   295.36% |     302.38% |
| PersistGroup400 |      1049 |                   2113 |   7237 |   689.59% |     342.50% |
| PersistGroup50  |       971 |                    980 |   4932 |   507.93% |     503.27% |
| Recovering      |     60516 |                  77688 |  64457 |   106.51% |      82.96% |
| Recovering8     |    116401 |                 101549 | 103463 |    88.89% |     101.88% |
| RecoveringFour  |     86107 |                  73218 |  66512 |    77.24% |      90.84% |
| RecoveringTwo   |     60730 |                  53062 |  43325 |    71.34% |      81.65% |

## Tag Query Performance

| Tag Count | Tag Mode |         Mean |      Error |     StdDev |
|----------:|----------|-------------:|-----------:|-----------:|
|        10 | Csv      | 1,760.393 ms | 27.1970 ms | 25.4401 ms |
|       100 | Csv      | 1,766.355 ms | 25.0182 ms | 23.4021 ms |
|      1000 | Csv      | 1,755.960 ms | 33.8171 ms | 34.7276 ms |
|     10000 | Csv      | 1,905.026 ms | 22.3564 ms | 20.9122 ms |
|        10 | TagTable |     2.336 ms |  0.0389 ms |  0.0344 ms |
|       100 | TagTable |     3.943 ms |  0.0705 ms |  0.0660 ms |
|      1000 | TagTable |    18.597 ms |  0.3570 ms |  0.3506 ms |
|     10000 | TagTable |   184.446 ms |  3.3447 ms |  2.9650 ms |

**Legend:**
* Tag Count: The number of tagged events retrieved per query (events/operation)
* Tag Mode: The tag read and write mode of the journal and query, either using a CSV formatted string or a dedicated tag table.
* Mean: The average time to complete each query operation in milliseconds 