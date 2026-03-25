using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Data;
using TransactionsIngest.Models;
using TransactionsIngest.Services;

namespace TransactionsIngest.Tests;

public class IngestionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public IngestionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private IngestionService BuildService(List<TransactionApiDto> data)
    {
        return new IngestionService(_db, new FakeApiService(data));
    }

    [Fact]
    public async Task NewTransactions_AreInserted()
    {
        var data = new List<TransactionApiDto>
        {
            MakeDto(1001, DateTime.UtcNow.AddHours(-1))
        };

        await BuildService(data).RunAsync();

        var tx = await _db.Transactions.FindAsync(1001);
        Assert.NotNull(tx);
        Assert.Equal("Active", tx.Status);

        var revision = _db.TransactionRevisions.First(r => r.TransactionId == 1001);
        Assert.Equal("Insert", revision.ChangeType);
    }

    [Fact]
    public async Task RunTwice_WithSameData_DoesNotCreateDuplicateRevisions()
    {
        var data = new List<TransactionApiDto>
        {
            MakeDto(1001, DateTime.UtcNow.AddHours(-1))
        };

        await BuildService(data).RunAsync();
        await BuildService(data).RunAsync();

        // should still only have the one Insert revision
        var count = _db.TransactionRevisions.Count(r => r.TransactionId == 1001);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ChangedField_IsDetectedAndRecorded()
    {
        var original = MakeDto(1001, DateTime.UtcNow.AddHours(-1), amount: 19.99m);
        await BuildService(new List<TransactionApiDto> { original }).RunAsync();

        var updated = MakeDto(1001, DateTime.UtcNow.AddHours(-1), amount: 29.99m);
        await BuildService(new List<TransactionApiDto> { updated }).RunAsync();

        var updateRevision = _db.TransactionRevisions
            .FirstOrDefault(r => r.TransactionId == 1001 && r.ChangeType == "Update");

        Assert.NotNull(updateRevision);
        Assert.Equal("Amount", updateRevision.FieldName);
        Assert.Equal("19.99", updateRevision.OldValue);
        Assert.Equal("29.99", updateRevision.NewValue);
    }

    [Fact]
    public async Task MissingTransaction_IsRevoked()
    {
        // first run: both present
        var data = new List<TransactionApiDto>
        {
            MakeDto(1001, DateTime.UtcNow.AddHours(-1)),
            MakeDto(1002, DateTime.UtcNow.AddHours(-2))
        };
        await BuildService(data).RunAsync();

        // second run: 1002 is gone from the snapshot
        await BuildService(new List<TransactionApiDto> { MakeDto(1001, DateTime.UtcNow.AddHours(-1)) }).RunAsync();

        var tx = await _db.Transactions.FindAsync(1002);
        Assert.Equal("Revoked", tx!.Status);

        var revokeRevision = _db.TransactionRevisions
            .FirstOrDefault(r => r.TransactionId == 1002 && r.ChangeType == "Revoke");
        Assert.NotNull(revokeRevision);
    }

    [Fact]
    public async Task AlreadyRevoked_NotRevokedAgain()
    {
        var data = new List<TransactionApiDto>
        {
            MakeDto(1001, DateTime.UtcNow.AddHours(-1)),
            MakeDto(1002, DateTime.UtcNow.AddHours(-2))
        };
        await BuildService(data).RunAsync();

        // run without 1002 twice
        var dataWithout1002 = new List<TransactionApiDto> { MakeDto(1001, DateTime.UtcNow.AddHours(-1)) };
        await BuildService(dataWithout1002).RunAsync();
        await BuildService(dataWithout1002).RunAsync();

        // should only have one Revoke revision
        var revokeCount = _db.TransactionRevisions.Count(r => r.TransactionId == 1002 && r.ChangeType == "Revoke");
        Assert.Equal(1, revokeCount);
    }

    [Fact]
    public async Task OldTransaction_IsFinalized()
    {
        // add a transaction older than 24 hours directly into the DB
        var old = new Transaction
        {
            TransactionId = 2001,
            CardNumberHash = "abc",
            CardNumberLast4 = "1111",
            LocationCode = "STO-01",
            ProductName = "Old Item",
            Amount = 10.00m,
            TransactionTime = DateTime.UtcNow.AddHours(-25),
            Status = "Active",
            CreatedAt = DateTime.UtcNow.AddHours(-25),
            UpdatedAt = DateTime.UtcNow.AddHours(-25)
        };
        _db.Transactions.Add(old);
        await _db.SaveChangesAsync();

        // run with an empty snapshot
        await BuildService(new List<TransactionApiDto>()).RunAsync();

        var tx = await _db.Transactions.FindAsync(2001);
        Assert.Equal("Finalized", tx!.Status);

        var finalizeRevision = _db.TransactionRevisions
            .FirstOrDefault(r => r.TransactionId == 2001 && r.ChangeType == "Finalize");
        Assert.NotNull(finalizeRevision);
    }

    [Fact]
    public async Task FinalizedTransaction_IsNotUpdated()
    {
        // put a finalized record directly in the DB
        var finalized = new Transaction
        {
            TransactionId = 3001,
            CardNumberHash = "abc",
            CardNumberLast4 = "1111",
            LocationCode = "STO-01",
            ProductName = "Old Item",
            Amount = 10.00m,
            TransactionTime = DateTime.UtcNow.AddHours(-25),
            Status = "Finalized",
            CreatedAt = DateTime.UtcNow.AddHours(-25),
            UpdatedAt = DateTime.UtcNow.AddHours(-25)
        };
        _db.Transactions.Add(finalized);
        await _db.SaveChangesAsync();

        // snapshot contains the same ID with a different amount
        var dto = new TransactionApiDto
        {
            TransactionId = 3001,
            CardNumber = "4111111111111111",
            LocationCode = "STO-01",
            ProductName = "Old Item",
            Amount = 99.99m,
            Timestamp = DateTime.UtcNow.AddHours(-25)
        };
        await BuildService(new List<TransactionApiDto> { dto }).RunAsync();

        var tx = await _db.Transactions.FindAsync(3001);
        Assert.Equal("Finalized", tx!.Status);
        Assert.Equal(10.00m, tx.Amount); // amount should not have changed

        var updateRevision = _db.TransactionRevisions
            .FirstOrDefault(r => r.TransactionId == 3001 && r.ChangeType == "Update");
        Assert.Null(updateRevision);
    }

    // helper to build a test DTO
    private static TransactionApiDto MakeDto(int id, DateTime time, decimal amount = 10.00m)
    {
        return new TransactionApiDto
        {
            TransactionId = id,
            CardNumber = "4111111111111111",
            LocationCode = "STO-01",
            ProductName = "Test Item",
            Amount = amount,
            Timestamp = time
        };
    }
}

// simple stand-in for the real API service used in tests
class FakeApiService : ITransactionApiService
{
    private readonly List<TransactionApiDto> _data;

    public FakeApiService(List<TransactionApiDto> data)
    {
        _data = data;
    }

    public Task<List<TransactionApiDto>> GetTransactionsAsync() => Task.FromResult(_data);
}