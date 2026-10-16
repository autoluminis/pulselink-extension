using System.Net;
using System.Text.Json;
using PulseLink.NotificationPlugins.GenericWebhook;
using PulseLink.Plugin.Abstractions.Notifications;

var count = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    count++;
}
async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { count++; return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

var handler = new FakeHandler();
using var client = new HttpClient(handler);
var channel = new GenericWebhookChannel(client);
NotificationDispatchMessage Message(string config = "{\"url\":\"https://example.invalid/hook\"}") =>
    new("delivery-42", "Triggered", "Critical", "测试主题", "正文", [], new Dictionary<string, string> { ["device"] = "pump" }, config);
foreach (var invalid in new[] { "{", "null", "[]", "{}", "{\"url\":12}", "{\"url\":\"/hook\"}", "{\"url\":\"file:///secret\"}", "{\"url\":\"https://u:p@example.invalid\"}", "{\"url\":\"https://example.invalid/#secret\"}" })
{
    Check(!(await channel.ValidateConfigurationAsync(invalid)).IsValid, "invalid configuration rejected");
    await Throws<Exception>(async () => await channel.SendAsync(Message(invalid)));
}
Check(handler.Calls == 0, "invalid configuration never sends HTTP");
Check((await channel.ValidateConfigurationAsync("{\"URL\":\"https://example.invalid/hook\"}")).IsValid, "case insensitive URL");
Check((await channel.ValidateConfigurationAsync("{\"url\":\"http://127.0.0.1/hook\"}")).IsValid, "local receiver");
var result = await channel.SendAsync(Message());
Check(result.Confirmation == NotificationDeliveryConfirmation.Accepted, "HTTP success is accepted, not delivered");
Check(handler.IdempotencyKey == "delivery-42", "deduplication header");
using (var payload = JsonDocument.Parse(handler.Body!))
{
    Check(payload.RootElement.GetProperty("deliveryId").GetString() == "delivery-42", "payload identity");
    Check(payload.RootElement.GetProperty("subject").GetString() == "测试主题", "UTF-8 payload");
    Check(!payload.RootElement.TryGetProperty("configurationJson", out _), "configuration excluded from payload");
}
foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
{
    handler.Status = status;
    await Throws<HttpRequestException>(async () => await channel.SendAsync(Message()));
}
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var before = handler.Calls;
await Throws<OperationCanceledException>(async () => await channel.SendAsync(Message(), cancelled.Token));
await Throws<OperationCanceledException>(async () => await channel.ValidateConfigurationAsync("{}", cancelled.Token));
Check(handler.Calls == before, "cancelled operation never sends");
handler.WaitForCancellation = true;
using var during = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
await Throws<OperationCanceledException>(async () => await channel.SendAsync(Message(), during.Token));
await Throws<OperationCanceledException>(async () => await channel.SendAsync(Message()));
var plugin = new GenericWebhookPlugin();
var owned = plugin.CreateChannels().Single();
Check(plugin.Id == "example.generic-webhook" && plugin.Version == new Version(1, 0, 0), "entry identity");
await plugin.StopAsync();
await plugin.StopAsync();
await Throws<ObjectDisposedException>(async () => await owned.SendAsync(Message()));
Console.WriteLine($"PASS: {count} Webhook regression checks");

sealed class FakeHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Body { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;
    public bool WaitForCancellation { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        Body = await request.Content!.ReadAsStringAsync(token);
        IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
        if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return new HttpResponseMessage(Status);
    }
}
