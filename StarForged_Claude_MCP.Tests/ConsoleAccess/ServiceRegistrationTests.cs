using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.ConsoleAccess;
using StarForged_Claude_MCP.ConsoleAccess.Download;
using StarForged_Claude_MCP.ConsoleAccess.Export;
using StarForged_Claude_MCP.ConsoleAccess.Search;
using StarForged_Claude_MCP.ConsoleAccess.Upload;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class ServiceRegistrationTests
{
    [Theory]
    [InlineData(typeof(FileUploader))]
    [InlineData(typeof(FileDownloader))]
    [InlineData(typeof(CategoryExporter))]
    [InlineData(typeof(Searcher))]
    [InlineData(typeof(ISummaryPrompt))]
    public void EveryConsoleCommand_ShouldResolveFromTheConfiguredContainer(Type service)
    {
        using var provider = BuildProvider();

        provider.Invoking(p => p.GetRequiredService(service))
            .Should().NotThrow(because: $"{service.Name} is needed to run a console command");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build());

        Program.ConfigureServices(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
