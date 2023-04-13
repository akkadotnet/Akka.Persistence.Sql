# Akka.Persistence.Sql Tag Query Benchmark

This benchmark uses BenchmarkDotNet to benchmark the performance of `CurrentEventsByTag` query using `Csv` and `TagTable` mode.

How to run this benchmark:
1. You have to have docker installed on your machine.
2. Go to the project directory.
3. Generate the test database by running: `dotnet run -c Release -- generate`
4. Run the benchmark by running `dotnet run -c Release`