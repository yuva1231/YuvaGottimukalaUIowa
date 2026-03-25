
using System.Text.Json.Serialization;

namespace TransactionsIngest.Models;

// matches the shape of the JSON coming from the transactions API
public class TransactionApiDto
{
    public int TransactionId { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
