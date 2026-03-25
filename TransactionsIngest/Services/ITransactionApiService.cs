using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public interface ITransactionApiService
{
    Task<List<TransactionApiDto>> GetTransactionsAsync();
}

