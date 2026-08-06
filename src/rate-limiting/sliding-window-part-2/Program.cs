using System.Threading.RateLimiting;
using SlidingWindow.Api.RateLimiting;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);


var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(redisConn);
    config.AbortOnConnectFail = false; 
    return ConnectionMultiplexer.Connect(config);
});


builder.Services.AddSingleton(sp => new RedisSlidingWindowLimiter(
    sp.GetRequiredService<IConnectionMultiplexer>(),
    permitLimit: 10,
    window: TimeSpan.FromSeconds(60)));


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-customer-inmemory", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: ResolveCustomer(context),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(60),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsync(
            "Rate limit exceeded (in-memory / replika başına).", token);
    };
});

var app = builder.Build();
app.UseRateLimiter();


var instanceId = Environment.GetEnvironmentVariable("HOSTNAME")
                 ?? Guid.NewGuid().ToString("N")[..8];


app.MapPost("/api/transfers/in-memory", (HttpContext ctx) =>
    Results.Ok(new
    {
        status = "accepted",
        mode = "in-memory",
        customer = ResolveCustomer(ctx),
        instance = instanceId
    }))
    .RequireRateLimiting("per-customer-inmemory");


app.MapPost("/api/transfers", async (HttpContext ctx, RedisSlidingWindowLimiter limiter) =>
{
    var customer = ResolveCustomer(ctx);

    if (!await limiter.TryAcquireAsync(customer))
    {
        ctx.Response.Headers.RetryAfter = "60";
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    return Results.Ok(new
    {
        status = "accepted",
        mode = "redis-global",
        customer,
        instance = instanceId
    });
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

static string ResolveCustomer(HttpContext ctx)
{
    var id = ctx.Request.Headers["X-Customer-Id"].ToString();
    return string.IsNullOrWhiteSpace(id) ? "anonymous" : id;
}
