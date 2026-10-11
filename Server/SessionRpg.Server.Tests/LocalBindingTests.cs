using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SessionRpg.Server.Tests
{
    public sealed class LocalBindingTests
    {
        [Theory]
        [InlineData("kestrel_endpoint")]
        [InlineData("hosting_urls")]
        [InlineData("hosting_ports")]
        [InlineData("all")]
        public async Task ExternalConfigurationCannotAddOrReplaceLoopbackListener(string scenario)
        {
            var overrides = new Dictionary<string, string?>();
            if (scenario is "kestrel_endpoint" or "all")
            {
                overrides["Kestrel:Endpoints:External:Url"] = "http://0.0.0.0:0";
            }
            if (scenario is "hosting_urls" or "all")
            {
                overrides["urls"] = "http://0.0.0.0:0";
                overrides["preferHostingUrls"] = "true";
            }
            if (scenario is "hosting_ports" or "all")
            {
                overrides["http_ports"] = "0";
            }
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(overrides).Build();
            string directory = Path.Combine(Path.GetTempPath(),
                "session-rpg-local-binding-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                await using var app = DevelopmentServer.Build(Path.Combine(directory, "worlds.db"),
                    port: 0, configuration: configuration);
                await app.StartAsync();
                try
                {
                    var addresses = app.Services.GetRequiredService<IServer>()
                        .Features.Get<IServerAddressesFeature>()!.Addresses;
                    string address = Assert.Single(addresses);
                    var endpoint = new Uri(address);
                    Assert.Equal("127.0.0.1", endpoint.Host);
                    Assert.True(IPAddress.IsLoopback(IPAddress.Parse(endpoint.Host)));
                    Assert.True(endpoint.Port > 0);

                    using var client = new HttpClient
                    {
                        BaseAddress = endpoint,
                        Timeout = TimeSpan.FromSeconds(10)
                    };
                    using HttpResponseMessage response = await client.GetAsync("/health");
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
                finally
                {
                    await app.StopAsync();
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
