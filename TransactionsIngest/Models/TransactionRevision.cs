namespace TransactionsIngest.Models;

// records every change made to a transaction so we have a full audit trail
public class TransactionRevision
{
    public int Id { get; set; }
    public int TransactionId { get; set; }

    // Insert, Update, Revoke, or Finalize
    public string ChangeType { get; set; } = string.Empty;

    // only populated for Update changes
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime ChangedAt { get; set; }
}