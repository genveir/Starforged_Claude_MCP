using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class TestFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    private string _connectionString = null!;
    private string _databaseName = null!;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var builder = new SqlConnectionStringBuilder(_connectionString);
        _databaseName = builder.InitialCatalog;
        var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await DropDatabase();
        await CreateDatabase(masterConnectionString, _databaseName);
        await CreateTable(_connectionString);

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddEmbeddingsServices();

        services.AddSingleton<IEmbeddingsFacade, EmbeddingsFacade>();
        services.AddSingleton<IDocumentsFacade, DocumentsFacade>();
        services.AddSingleton<McpServer>();

        services.AddSingleton<IConfiguration>(configuration);

        Services = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await DropDatabase();
    }

    private static async Task CreateDatabase(string masterConnectionString, string databaseName)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        var checkDbSql = $"select database_id from sys.databases where name = '{databaseName}'";
        var exists = await connection.ExecuteScalarAsync<int?>(checkDbSql);

        if (exists == null)
        {
            var createDbSql = $"create database [{databaseName}]";
            await connection.ExecuteAsync(createDbSql);
        }
    }

    private static async Task CreateTable(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in DatabaseSchema.GetTableCreationBatches())
        {
            await connection.ExecuteAsync(batch);
        }
    }

    private async Task DropDatabase()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        var dropDbSql = $@"
            if exists (select database_id from sys.databases where name = '{_databaseName}')
            begin
                alter database [{_databaseName}] set single_user with rollback immediate;
                drop database [{_databaseName}];
            end";

        await connection.ExecuteAsync(dropDbSql);
    }
}
