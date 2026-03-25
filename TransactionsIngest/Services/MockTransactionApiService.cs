using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

// Here i am reading transactions from the local JSON file instead of a real API endpoint
public class MockTransactionApiService : ITransactionApiService
{
    private readonly string _feedPath;

    public MockTransactionApiService(IConfiguration config)
    {
        _feedPath = config["AppSettings:MockFeedPath"] ?? "mock-transactions.json";
    }

    public async Task<List<TransactionApiDto>> GetTransactionsAsync()
    {
        if (!File.Exists(_feedPath))
        {
            Console.WriteLine($"Mock feed not found at '{_feedPath}'. Using empty list.");
            return new List<TransactionApiDto>();
        }

        var json = await File.ReadAllTextAsync(_feedPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<TransactionApiDto>>(json, options)
               ?? new List<TransactionApiDto>();
    }
}