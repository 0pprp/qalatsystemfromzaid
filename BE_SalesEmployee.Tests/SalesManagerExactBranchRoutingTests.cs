using System.Collections.Concurrent;
using System.Net;
using System.Text;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Production-like proof: cityValue selects exactly one branch HTTP target.
/// Fake Najaf/Karkh/Basra listeners return distinct employee payloads.
/// </summary>
public sealed class SalesManagerExactBranchRoutingTests
{
    [Fact]
    public async Task City_1_Hits_Najaf_Only()
    {
        await using var harness = await BranchHttpHarness.StartAsync();
        var (status, body) = await harness.Aggregator.GetExactBranchArrayAsync(
            Manager(), "1", "sales-manager/employees", CancellationToken.None);

        Assert.Equal(200, status);
        var json = Assert.IsType<List<System.Text.Json.Nodes.JsonNode>>(body);
        Assert.Single(json);
        Assert.Equal("N1", json[0]!["employeeName"]!.GetValue<string>());
        Assert.Equal(1, harness.Hits["najaf"]);
        Assert.Equal(0, harness.Hits.GetValueOrDefault("karkh"));
        Assert.Equal(0, harness.Hits.GetValueOrDefault("basra"));
    }

    [Fact]
    public async Task City_3_Hits_Karkh_Only_Never_Najaf()
    {
        await using var harness = await BranchHttpHarness.StartAsync();
        var (status, body) = await harness.Aggregator.GetExactBranchArrayAsync(
            Manager(), "3", "sales-manager/employees", CancellationToken.None);

        Assert.Equal(200, status);
        var json = Assert.IsType<List<System.Text.Json.Nodes.JsonNode>>(body);
        Assert.Single(json);
        Assert.Equal("K1", json[0]!["employeeName"]!.GetValue<string>());
        Assert.Equal(0, harness.Hits.GetValueOrDefault("najaf"));
        Assert.Equal(1, harness.Hits["karkh"]);
        Assert.Equal(0, harness.Hits.GetValueOrDefault("basra"));
    }

    [Fact]
    public async Task City_9_Hits_Basra_Only_Never_Najaf()
    {
        await using var harness = await BranchHttpHarness.StartAsync();
        var (status, body) = await harness.Aggregator.GetExactBranchArrayAsync(
            Manager(), "9", "sales-manager/employees", CancellationToken.None);

        Assert.Equal(200, status);
        var json = Assert.IsType<List<System.Text.Json.Nodes.JsonNode>>(body);
        Assert.Single(json);
        Assert.Equal("B1", json[0]!["employeeName"]!.GetValue<string>());
        Assert.Equal(0, harness.Hits.GetValueOrDefault("najaf"));
        Assert.Equal(0, harness.Hits.GetValueOrDefault("karkh"));
        Assert.Equal(1, harness.Hits["basra"]);
    }

    [Fact]
    public async Task Unknown_City_Returns_404_Without_Any_Branch_Call()
    {
        await using var harness = await BranchHttpHarness.StartAsync();
        var (status, _) = await harness.Aggregator.GetExactBranchArrayAsync(
            Manager(), "999", "sales-manager/employees", CancellationToken.None);

        Assert.Equal(404, status);
        Assert.Equal(0, harness.Hits.GetValueOrDefault("najaf"));
        Assert.Equal(0, harness.Hits.GetValueOrDefault("karkh"));
        Assert.Equal(0, harness.Hits.GetValueOrDefault("basra"));
    }

    [Fact]
    public async Task Unreachable_Exact_Branch_Returns_502_Not_Empty_Ok()
    {
        await using var harness = await BranchHttpHarness.StartAsync(killBasra: true);
        var (status, body) = await harness.Aggregator.GetExactBranchArrayAsync(
            Manager(), "9", "sales-manager/employees", CancellationToken.None);

        Assert.Equal(502, status);
        Assert.NotNull(body);
    }

    private static GatewayUser Manager() => new()
    {
        UserName = "sm-test",
        UserType = "مدير مبيعات"
    };

    private sealed class BranchHttpHarness : IAsyncDisposable
    {
        private readonly List<HttpListener> _listeners = [];
        public required ConcurrentDictionary<string, int> Hits { get; init; }
        public required SalesManagerBranchAggregator Aggregator { get; init; }

        public static async Task<BranchHttpHarness> StartAsync(bool killBasra = false)
        {
            var hits = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var najaf = StartListener("najaf", """[{"employeeName":"N1","employeeId":1}]""", hits);
            var karkh = StartListener("karkh", """[{"employeeName":"K1","employeeId":2}]""", hits);
            HttpListener? basra = null;
            string basraPrefix;
            if (killBasra)
            {
                // Bind a port then close it so the URL is unreachable.
                basraPrefix = ReserveClosedPort();
            }
            else
            {
                basra = StartListener("basra", """[{"employeeName":"B1","employeeId":3}]""", hits);
                basraPrefix = basra.Prefixes.First();
            }

            var lab = new List<AdminCityConfig>
            {
                new()
                {
                    Value = "1",
                    Name = "النجف",
                    Database = "DatabaseCompanyNajaf",
                    Link = najaf.Prefixes.First()
                },
                new()
                {
                    Value = "3",
                    Name = "الكرخ",
                    Database = "DatabaseCompanyBaghdadKarak",
                    Link = karkh.Prefixes.First()
                },
                new()
                {
                    Value = "9",
                    Name = "البصرة",
                    Database = "DatabaseCompanyBasra",
                    Link = basraPrefix
                },
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApiKey"] = "test-key",
                ["SalesManagement:RequireDemoDatabase"] = "false",
                ["LabCities:0:Value"] = lab[0].Value,
                ["LabCities:0:Name"] = lab[0].Name,
                ["LabCities:0:Database"] = lab[0].Database,
                ["LabCities:0:Link"] = lab[0].Link,
                ["LabCities:1:Value"] = lab[1].Value,
                ["LabCities:1:Name"] = lab[1].Name,
                ["LabCities:1:Database"] = lab[1].Database,
                ["LabCities:1:Link"] = lab[1].Link,
                ["LabCities:2:Value"] = lab[2].Value,
                ["LabCities:2:Name"] = lab[2].Name,
                ["LabCities:2:Database"] = lab[2].Database,
                ["LabCities:2:Link"] = lab[2].Link,
            }).Build();

            var cities = new AdminCitiesService(new HttpClient(), config, NullLogger<AdminCitiesService>.Instance);
            var proxy = new BranchProxyService(new HttpClient { Timeout = TimeSpan.FromSeconds(2) }, config);
            var aggregator = new SalesManagerBranchAggregator(cities, proxy);

            var harness = new BranchHttpHarness
            {
                Hits = hits,
                Aggregator = aggregator
            };
            harness._listeners.Add(najaf);
            harness._listeners.Add(karkh);
            if (basra != null)
            {
                harness._listeners.Add(basra);
            }

            // Give listeners a tick to accept.
            await Task.Delay(50);
            return harness;
        }

        private static HttpListener StartListener(string key, string jsonBody, ConcurrentDictionary<string, int> hits)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var port = 18000 + Random.Shared.Next(1000, 8000);
                var prefix = $"http://127.0.0.1:{port}/api/";
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                try
                {
                    listener.Start();
                }
                catch
                {
                    listener.Close();
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try
                        {
                            ctx = await listener.GetContextAsync();
                        }
                        catch
                        {
                            break;
                        }

                        hits.AddOrUpdate(key, 1, (_, n) => n + 1);
                        var bytes = Encoding.UTF8.GetBytes(jsonBody);
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        ctx.Response.ContentLength64 = bytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(bytes);
                        ctx.Response.Close();
                    }
                });

                return listener;
            }

            throw new InvalidOperationException("Could not bind local HttpListener for branch harness.");
        }

        private static string ReserveClosedPort()
        {
            var listener = new HttpListener();
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var port = 19000 + Random.Shared.Next(1000, 8000);
                var prefix = $"http://127.0.0.1:{port}/api/";
                listener.Prefixes.Clear();
                listener.Prefixes.Add(prefix);
                try
                {
                    listener.Start();
                    listener.Stop();
                    listener.Close();
                    return prefix;
                }
                catch
                {
                    // try next port
                }
            }

            listener.Close();
            return "http://127.0.0.1:1/api/";
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch
                {
                    // ignore
                }
            }

            await Task.CompletedTask;
        }
    }
}
