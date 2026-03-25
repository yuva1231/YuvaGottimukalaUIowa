
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionsIngest.Data;
using TransactionsIngest.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(config.GetConnectionString("DefaultConnection")));

services.AddSingleton<IConfiguration>(config);

// perhaps can put in real implementation here for live API
if (config.GetValue<bool>("AppSettings:UseMockFeed"))
    services.AddTransient<ITransactionApiService, MockTransactionApiService>();
else
    services.AddTransient<ITransactionApiService, MockTransactionApiService>();

services.AddTransient<IngestionService>();

var provider = services.BuildServiceProvider();

// create the database tables if they don't exist yet
var db = provider.GetRequiredService<AppDbContext>();
db.Database.EnsureCreated();

// run the ingestion job
var ingestion = provider.GetRequiredService<IngestionService>();
await ingestion.RunAsync();
