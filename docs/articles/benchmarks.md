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
