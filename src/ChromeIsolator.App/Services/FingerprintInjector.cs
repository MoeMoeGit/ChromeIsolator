using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ChromeIsolator.Services;

public sealed class FingerprintInjector : IAsyncDisposable
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private readonly int _debugPort;
    private readonly string _script;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource> _pendingResponses = [];
    private readonly ConcurrentDictionary<string, byte> _injectedTargets = [];
    private readonly ConcurrentDictionary<string, byte> _attachingTargets = [];
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _webSocket;
    private Task? _runTask;
    private int _nextCommandId;
    private int _disposed;

    public FingerprintInjector(int debugPort, int instanceNumber)
    {
        _debugPort = debugPort;
        _script = GenerateScript(instanceNumber);
    }

    public event Action<Exception>? Failed;

    public Task StartAsync()
    {
        return _runTask ??= Task.Run(RunAsync);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _cts.CancelAsync();
        await CleanupWebSocketAsync();

        foreach (var pending in _pendingResponses.Values)
        {
            pending.TrySetCanceled();
        }

        _pendingResponses.Clear();

        if (_runTask is not null)
        {
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch
            {
                // Disposal must stay best-effort; injection is not user-visible browsing state.
            }
        }

        _sendLock.Dispose();
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        const int maxRetries = 5;
        var retryCount = 0;
        Exception? lastError = null;

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var browserWebSocketUrl = await PollBrowserWebSocketUrlAsync(_cts.Token);
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(browserWebSocketUrl, _cts.Token);

                retryCount = 0;
                lastError = null;

                var receiveTask = Task.Run(ReceiveLoopAsync);

                await SendCommandAsync("Target.setDiscoverTargets", new { discover = true });
                await SendCommandAsync("Target.setAutoAttach", new
                {
                    autoAttach = true,
                    waitForDebuggerOnStart = false,
                    flatten = true
                });

                foreach (var targetId in await PollPageTargetsAsync(_cts.Token))
                {
                    await AttachToTargetIfNeededAsync(targetId);
                }

                await receiveTask;

                if (_cts.IsCancellationRequested) break;

                ClearTargetState();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (retryCount >= maxRetries)
                {
                    Failed?.Invoke(lastError);
                    break;
                }

                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1));
                try
                {
                    await Task.Delay(delay, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await CleanupWebSocketAsync();
                ClearTargetState();
            }
        }
    }

    private async Task CleanupWebSocketAsync()
    {
        if (_webSocket is not null)
        {
            try
            {
                if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }

            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    private static string GenerateScript(int instanceNumber)
    {
        var cores = new[] { 4, 6, 8, 10 };
        var memory = new[] { 4, 8, 16 };
        var num = Math.Max(instanceNumber, 1);
        var core = cores[(num - 1) % cores.Length];
        var mem = memory[(num - 1) % memory.Length];

        return
            $"Object.defineProperty(navigator, 'hardwareConcurrency', {{get: () => {core}, configurable: true}});" +
            $"Object.defineProperty(navigator, 'deviceMemory', {{get: () => {mem}, configurable: true}});";
    }

    private async Task<Uri> PollBrowserWebSocketUrlAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(200);
        var maxDelay = TimeSpan.FromSeconds(2);
        var url = $"http://127.0.0.1:{_debugPort}/json/version";

        for (var attempt = 0; attempt < 15; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await HttpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    if (json.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsUrl) &&
                        Uri.TryCreate(wsUrl.GetString(), UriKind.Absolute, out var uri))
                    {
                        return uri;
                    }
                }
            }
            catch
            {
                // Chrome is still starting; retry.
            }

            await Task.Delay(delay, cancellationToken);
            delay = delay < maxDelay ? delay + delay : maxDelay;
        }

        throw new InvalidOperationException("Chrome CDP 未就绪");
    }

    private async Task<IReadOnlyList<string>> PollPageTargetsAsync(CancellationToken cancellationToken)
    {
        var url = $"http://127.0.0.1:{_debugPort}/json";
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return json.RootElement
            .EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "page")
            .Where(item => item.TryGetProperty("id", out _))
            .Select(item => item.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        while (!_cts.IsCancellationRequested && _webSocket is { State: WebSocketState.Open })
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            await HandleMessageAsync(message.ToArray());
        }
    }

    private async Task HandleMessageAsync(byte[] payload)
    {
        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;

        if (root.TryGetProperty("id", out var idElement) &&
            _pendingResponses.TryRemove(idElement.GetInt32(), out var pending))
        {
            if (root.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : "CDP command failed";
                pending.TrySetException(new InvalidOperationException(message));
            }
            else
            {
                pending.TrySetResult();
            }

            return;
        }

        if (!root.TryGetProperty("method", out var methodElement) ||
            methodElement.GetString() != "Target.attachedToTarget" ||
            !root.TryGetProperty("params", out var parameters))
        {
            return;
        }

        if (!parameters.TryGetProperty("sessionId", out var sessionElement) ||
            !parameters.TryGetProperty("targetInfo", out var targetInfo) ||
            !targetInfo.TryGetProperty("type", out var typeElement) ||
            typeElement.GetString() != "page" ||
            !targetInfo.TryGetProperty("targetId", out var targetElement))
        {
            return;
        }

        var sessionId = sessionElement.GetString();
        var targetId = targetElement.GetString();
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await InjectSessionAsync(sessionId, targetId);
            }
            catch
            {
                // Injection failures should not interrupt browsing.
            }
        });
    }

    private async Task AttachToTargetIfNeededAsync(string targetId)
    {
        if (!MarkAttachingIfNeeded(targetId))
        {
            return;
        }

        try
        {
            await SendCommandAsync("Target.attachToTarget", new
            {
                targetId,
                flatten = true
            });
        }
        catch
        {
            _attachingTargets.TryRemove(targetId, out _);
            throw;
        }
    }

    private async Task InjectSessionAsync(string sessionId, string targetId)
    {
        if (_injectedTargets.ContainsKey(targetId))
        {
            return;
        }

        await SendCommandAsync("Page.addScriptToEvaluateOnNewDocument", new { source = _script }, sessionId);
        await SendCommandAsync("Runtime.evaluate", new { expression = _script }, sessionId);
        _attachingTargets.TryRemove(targetId, out _);
        _injectedTargets.TryAdd(targetId, 0);
    }

    private async Task SendCommandAsync(string method, object parameters, string? sessionId = null)
    {
        if (_webSocket is not { State: WebSocketState.Open })
        {
            throw new InvalidOperationException("CDP WebSocket 未连接");
        }

        await _sendLock.WaitAsync(_cts.Token);
        try
        {
            var id = Interlocked.Increment(ref _nextCommandId);
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingResponses[id] = pending;

            var payload = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            };
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                payload["sessionId"] = sessionId;
            }

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);

            var completed = await Task.WhenAny(pending.Task, Task.Delay(TimeSpan.FromSeconds(10), _cts.Token));
            if (completed != pending.Task)
            {
                _pendingResponses.TryRemove(id, out _);
                throw new TimeoutException($"等待 CDP 响应超时（command id: {id}）");
            }

            await pending.Task;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private bool MarkAttachingIfNeeded(string targetId)
    {
        if (_injectedTargets.ContainsKey(targetId))
        {
            return false;
        }

        return _attachingTargets.TryAdd(targetId, 0);
    }

    private void ClearTargetState()
    {
        _injectedTargets.Clear();
        _attachingTargets.Clear();
    }
}
