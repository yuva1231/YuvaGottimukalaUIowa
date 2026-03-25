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

        
    }

}