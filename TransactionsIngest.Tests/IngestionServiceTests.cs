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
        
    }
}