namespace TransactionsIngest.Models;

public class Transaction
{
    public int TransactionId { get; set; }

    // store only the hash and last 4 digits to avoid keeping raw card numbers
    public string CardNumberHash { get; set; } = string.Empty;
    public string CardNumberLast4 { get; set; } = string.Empty;

    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionTime { get; set; }

    // Active, Revoked, or Finalized
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}