# TransactionsIngest

A .NET 10 console app that ingests card transactions for the last 24 hours, upserts records by transaction ID, tracks field-level changes, marks missing transactions as revoked, and finalizes records older than 24 hours. It is meant to be run once per hour by an external scheduler.

## Build and Run

Requires .NET 10 SDK.

```bash
dotnet build

cd TransactionsIngest
dotnet run
```

The app reads from `mock-transactions.json` by default. You can edit that file to change the input. The SQLite database is created automatically on first run.

## Run Tests

```bash
cd ..
cd TransactionsIngest.Tests
dotnet test
```
