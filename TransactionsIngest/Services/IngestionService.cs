namespace TransactionsIngest.Services;

public class IngestionService
{
    private readonly AppDbContext _db;
    private readonly ITransactionApiService _api;
    private readonly IConfiguration? _config;

}