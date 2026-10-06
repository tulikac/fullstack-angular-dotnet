using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;

namespace FullstackAngularDotnet.Api.Tests;

public sealed class AppTests : IClassFixture<RunningApp>
{
    private readonly HttpClient _client;

    public AppTests(RunningApp app)
    {
        _client = app.Client;
    }

    [Fact]
    public async Task ReportsHealthAndStatus()
    {
        var health = await _client.GetFromJsonAsync<ApiHealth>("/api/health");
        var status = await _client.GetFromJsonAsync<ApiStatus>("/api/status");

        Assert.Equal("ok", health!.Status);
        Assert.Equal("fullstack-angular-dotnet-api", health.App);
        Assert.Equal("available", status!.Status);
        Assert.Equal("fullstack-angular-dotnet-api", status.App);
    }

    [Fact]
    public async Task ReportsDeploymentIdentity()
    {
        var version = await _client.GetFromJsonAsync<ApiVersion>("/api/version");

        Assert.Equal("fullstack-angular-dotnet-api", version!.App);
        Assert.Equal("ASP.NET Core", version.Framework);
        Assert.NotEmpty(version.Runtime);
    }

    [Fact]
    public async Task EchoesJsonPayload()
    {
        var response = await _client.PostAsJsonAsync("/api/echo", new { message = "builder-apps" });
        var body = await response.Content.ReadFromJsonAsync<EchoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("builder-apps", body!.Message);
        Assert.True(body.Received);
    }

    [Fact]
    public async Task ReturnsJsonForMissingApiRoute()
    {
        var response = await _client.GetAsync("/api/missing");
        var body = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", body!.Error);
        Assert.Equal("The requested API route does not exist.", body.Message);
    }

    private sealed record ApiHealth(string Status, string App);
    private sealed record ApiStatus(string Status, string App);
    private sealed record ApiVersion(string App, string Framework, string Runtime);
    private sealed record EchoResponse(string Message, bool Received);
    private sealed record ApiError(string Error, string Message);
}

public sealed class RunningApp : IAsyncLifetime
{
    private readonly StringBuilder _output = new();
    private Process? _process;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var port = FindAvailablePort();
        var appAssembly = typeof(Program).Assembly.Location;

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(appAssembly)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(appAssembly);
        startInfo.Environment["PORT"] = port.ToString();
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += CaptureOutput;
        _process.ErrorDataReceived += CaptureOutput;

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start the ASP.NET Core test process.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        Client = new HttpClient { BaseAddress = new($"http://127.0.0.1:{port}") };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The ASP.NET Core test process exited unexpectedly.{Environment.NewLine}{_output}");
            }

            try
            {
                var response = await Client.GetAsync("/api/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The ASP.NET Core test process did not become healthy.{Environment.NewLine}{_output}");
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private void CaptureOutput(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is not null)
        {
            _output.AppendLine(args.Data);
        }
    }

    private static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
