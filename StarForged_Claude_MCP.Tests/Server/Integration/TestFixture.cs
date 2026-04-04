using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Embeddings;
using StarForged_Claude_MCP.Embeddings.Services;
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

        await CreateDatabase(masterConnectionString, _databaseName);
        await CreateTable(_connectionString);

        var services = new ServiceCollection();

        services.AddLogging();

        services.AddEmbeddingsServices();

        services.AddSingleton<EmbeddingsFacade>();
        services.AddSingleton<McpServer>();

        services.AddSingleton<IConfiguration>(configuration);

        Services = services.BuildServiceProvider();

        var vectorCache = Services.GetRequiredService<VectorCacheService>();
        await vectorCache.StartAsync(CancellationToken.None);
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

        var createTableSql = @"
            if not exists (select * from sys.tables where name = 'Embeddings')
            begin
                create table Embeddings
                (
                    Id int identity(1,1) primary key,
                    Text nvarchar(max) not null,
                    Vector varbinary(max) not null,
                    SourceDocument nvarchar(500) not null,
                    TokenCount int not null
                )
            end";

        await connection.ExecuteAsync(createTableSql);
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
