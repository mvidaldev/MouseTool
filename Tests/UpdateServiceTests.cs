using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MouseTool.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAndDownloadInstaller_UsesManifestAndValidatesHash()
    {
        var payload = Encoding.UTF8.GetBytes("installer-binary-for-test");
        var hash = Convert.ToHexString(SHA256.HashData(payload));
        using var server = await SimpleHttpServer.StartAsync();
        var manifestBody = $$"""
            {
              "version": "99.0.0",
              "installerUrl": "{{server.BaseUrl}}MouseTool-Setup.exe",
              "sha256": "{{hash}}"
            }
            """;

        server.Responder = requestPath => requestPath switch
        {
            "/update.json" => SimpleHttpServer.Json(manifestBody),
            "/MouseTool-Setup.exe" => SimpleHttpServer.Binary(payload, "application/octet-stream"),
            _ => SimpleHttpServer.NotFound()
        };

        var service = new UpdateService($"{server.BaseUrl}update.json");

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsSuccessful);
        Assert.True(result.IsUpdateAvailable);
        Assert.NotNull(result.Manifest);

        var installerPath = await service.DownloadInstallerAsync(result.Manifest!);

        Assert.True(File.Exists(installerPath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(installerPath));
    }

    [Fact]
    public async Task CheckForUpdates_WhenManifestHashDoesNotMatch_ThrowsOnDownload()
    {
        var payload = Encoding.UTF8.GetBytes("installer-binary-for-test");

        using var server = await SimpleHttpServer.StartAsync();
        server.Responder = requestPath => requestPath switch
        {
            "/update.json" => SimpleHttpServer.Json($$"""
                {
                  "version": "99.0.0",
                  "installerUrl": "{{server.BaseUrl}}MouseTool-Setup.exe",
                  "sha256": "BADHASH"
                }
                """),
            "/MouseTool-Setup.exe" => SimpleHttpServer.Binary(payload, "application/octet-stream"),
            _ => SimpleHttpServer.NotFound()
        };

        var service = new UpdateService($"{server.BaseUrl}update.json");
        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadInstallerAsync(result.Manifest!));
    }

    private sealed class SimpleHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;

        private SimpleHttpServer(TcpListener listener)
        {
            _listener = listener;
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            _worker = Task.Run(RunAsync);
        }

        public string BaseUrl { get; }

        public Func<string, HttpResponse> Responder { get; set; } = _ => NotFound();

        public static async Task<SimpleHttpServer> StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            await Task.Yield();
            return new SimpleHttpServer(listener);
        }

        public static HttpResponse Json(string body) => new(200, "application/json", Encoding.UTF8.GetBytes(body));

        public static HttpResponse Binary(byte[] body, string contentType) => new(200, contentType, body);

        public static HttpResponse NotFound() => new(404, "text/plain", Encoding.UTF8.GetBytes("not found"));

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore shutdown exceptions in tests.
            }

            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    client?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream, Encoding.ASCII, leaveOpen: true);

                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    return;
                }

                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                }

                var path = requestLine.Split(' ')[1];
                var response = Responder(path);
                var header = $"HTTP/1.1 {response.StatusCode} {(response.StatusCode == 200 ? "OK" : "Not Found")}\r\nContent-Type: {response.ContentType}\r\nContent-Length: {response.Body.Length}\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await networkStream.WriteAsync(headerBytes, _cts.Token);
                await networkStream.WriteAsync(response.Body, _cts.Token);
                await networkStream.FlushAsync(_cts.Token);
            }
        }

        internal sealed record HttpResponse(int StatusCode, string ContentType, byte[] Body);
    }
}
