
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransactionsIngest.Data;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;
public class IngestionService
{
    private readonly AppDbContext _db;
    private readonly ITransactionApiService _api;
    private readonly IConfiguration? _config;


    public IngestionService(AppDbContext db, ITransactionApiService api, IConfiguration? config = null)
    {
        _db = db;
        _api = api;
        _config = config;
    }

    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-24);
        var stopwatch = Stopwatch.StartNew();

        // log the start of the job and configuration details
        Console.WriteLine("=========================================");
        Console.WriteLine(" Transactions Ingestion Job");
        Console.WriteLine($" Run Time: {now:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        Console.WriteLine("Loading configuration...");
        Console.WriteLine($"Database: {_config?["ConnectionStrings:DefaultConnection"] ?? "transactions.db"}");
        Console.WriteLine($"Snapshot source: {_config?["AppSettings:MockFeedPath"] ?? "mock-transactions.json"}");
        Console.WriteLine();

        Console.WriteLine("Fetching last 24-hour transaction snapshot...");
        var snapshot = await _api.GetTransactionsAsync();
        Console.WriteLine($"Records received: {snapshot.Count}");
        Console.WriteLine();

        // counters for logging
        int newCount = 0, updatedCount = 0, revokedCount = 0, finalizedCount = 0, unchangedCount = 0;

        // wrap everything in a DB transaction so repeated runs don't create duplicates
        using var dbTx = await _db.Database.BeginTransactionAsync();

        try
        {
            //stub will fill in after i write the insert/update/revoke logic
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine("Database transaction rolled back.");
            Console.WriteLine("=========================================");
            throw;
        }
    }

    // inserts a new record; returns true if it was immediately finalized
    private bool InsertTransaction(TransactionApiDto dto, DateTime cutoff)
    {
        var status = dto.Timestamp < cutoff ? "Finalized" : "Active";

        // hash card number, store last 4 digits
        var transaction = new Transaction
        {
            TransactionId = dto.TransactionId,
            CardNumberHash = HashCardNumber(dto.CardNumber),
            CardNumberLast4 = dto.CardNumber.Length >= 4 ? dto.CardNumber[^4..] : dto.CardNumber,
            LocationCode = dto.LocationCode,
            ProductName = dto.ProductName,
            Amount = dto.Amount,
            TransactionTime = dto.Timestamp,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);
        _db.TransactionRevisions.Add(new TransactionRevision
        {
            TransactionId = dto.TransactionId,
            ChangeType = status == "Finalized" ? "Finalize" : "Insert",
            ChangedAt = DateTime.UtcNow
        });

        if (status == "Finalized")
            Console.WriteLine($"[NEW] Transaction {dto.TransactionId} inserted (finalized - older than 24 hours)");
        else
            Console.WriteLine($"[NEW] Transaction {dto.TransactionId} inserted");

        return status == "Finalized";
    }

    // updates the existing record if any of the relevant fields have changed
    private bool UpdateTransactionIfChanged(Transaction existing, TransactionApiDto dto)
    {
        //
        var changes = new List<(string Field, string Old, string New)>();

        if (existing.LocationCode != dto.LocationCode)
            changes.Add(("LocationCode", existing.LocationCode, dto.LocationCode));

        if (existing.ProductName != dto.ProductName)
            changes.Add(("ProductName", existing.ProductName, dto.ProductName));

        if (existing.Amount != dto.Amount)
            changes.Add(("Amount", existing.Amount.ToString("F2"), dto.Amount.ToString("F2")));

        if (changes.Count == 0)
        {
            Console.WriteLine($"[UNCHANGED] Transaction {existing.TransactionId}");
            return false;
        }

        existing.LocationCode = dto.LocationCode;
        existing.ProductName = dto.ProductName;
        existing.Amount = dto.Amount;
        existing.Status = "Active"; // reactivate if it was previously revoked
        existing.UpdatedAt = DateTime.UtcNow;

        Console.WriteLine($"[UPDATE] Transaction {existing.TransactionId}");
        foreach (var (field, old, newVal) in changes)
        {
            _db.TransactionRevisions.Add(new TransactionRevision
            {
                TransactionId = existing.TransactionId,
                ChangeType = "Update",
                FieldName = field,
                OldValue = old,
                NewValue = newVal,
                ChangedAt = DateTime.UtcNow
            });

            Console.WriteLine($"  Field Changed: {field}");
            Console.WriteLine($"  Old Value:     {old}");
            Console.WriteLine($"  New Value:     {newVal}");
        }

        return true;
    }

   // marks active transactions within the 24h window that are missing from the snapshot
    private async Task<int> RevokeAbsent(DateTime cutoff, HashSet<int> snapshotIds)
    {
        var toRevoke = await _db.Transactions
            .Where(t => t.Status == "Active"
                     && t.TransactionTime >= cutoff
                     && !snapshotIds.Contains(t.TransactionId))
            .ToListAsync();

        foreach (var t in toRevoke)
        {
            t.Status = "Revoked";
            t.UpdatedAt = DateTime.UtcNow;

            _db.TransactionRevisions.Add(new TransactionRevision
            {
                TransactionId = t.TransactionId,
                ChangeType = "Revoke",
                ChangedAt = DateTime.UtcNow
            });

            Console.WriteLine($"[REVOKED] Transaction {t.TransactionId}");
            Console.WriteLine($"  Reason: Missing from latest snapshot");
        }

        return toRevoke.Count;
    }


    // finalizes active records older than 24 hours so they can no longer change
    private async Task<int> FinalizeOld(DateTime cutoff)
    {
        var toFinalize = await _db.Transactions
            .Where(t => t.Status == "Active" && t.TransactionTime < cutoff)
            .ToListAsync();

        foreach (var t in toFinalize)
        {
            t.Status = "Finalized";
            t.UpdatedAt = DateTime.UtcNow;

            _db.TransactionRevisions.Add(new TransactionRevision
            {
                TransactionId = t.TransactionId,
                ChangeType = "Finalize",
                ChangedAt = DateTime.UtcNow
            });

            Console.WriteLine($"[FINALIZED] Transaction {t.TransactionId}");
        }

        return toFinalize.Count;
    }

    private static string HashCardNumber(string cardNumber)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(cardNumber));
        return Convert.ToHexString(bytes).ToLower();
    }

 
}