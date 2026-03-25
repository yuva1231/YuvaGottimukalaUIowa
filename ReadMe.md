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

## Configuration

Settings are in `TransactionsIngest/appsettings.json`. You can change the connection string, the API base URL, and whether to use the mock feed or not. Although, not entireley confident itll work 100% as I havent tested. 

## Approach

All the ingestion logic is in `IngestionService.RunAsync()`. It fetches the snapshot, then wraps everything in a single database transaction. For each item in the snapshot it either inserts a new record or checks if any tracked fields changed and logs those changes. After that it finds any active transactions from the last 24 hours that were not in the snapshot and marks them revoked. Finally it marks anything older than 24 hours as finalized. Wrapping it all in one DB transaction is what makes repeated runs idempotent.


## Time Estimate vs Actual

I estimated about 7 hours for this task. It ended up taking around 8 hours. I Know people can develop this fairly quickly but I am a bit rusty. Most of the extra time went into making sure the logic was correct and writing the tests to cover those cases properly. I also spent a lot of time reading up on EF Core with SQLite since I had not used that combination before. If this leads to potential employment, I am eager to learn and willing to pivot into whatver!

## Other Comments

One thing I ran into was figuring out the right scope for the revocation query. My first instinct was to look for all active transactions not in the snapshot, but that would incorrectly revoke transactions that were finalized or from a previous window. Limiting the query to transactions within the last 24 hours fixed that.

A few things I would point out in the code: the whole run is wrapped in a single DB transaction which keeps things atomic and idempotent without needing any extra logic. The audit table records every change including inserts, updates, revocations, and finalizations so there is a complete history of what happened to each record. Card number privacy is handled by storing only the hash and last 4 digits and never persisting the full number.

My implementation is probably a bit crude and development wise I had to use around 2 hours of it researching overnight about the framework and used AI to explain a few concepts and help with the tests and issues with the using declarations causing my builds to fail.

Thanks for your time,
Yuva 