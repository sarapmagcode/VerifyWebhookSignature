using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

static bool VerifyWebhookSignature(byte[] rawBody, string signature, IHeaderDictionary headers, string secret)
{
    List<string> components = signature.Split(",").ToList();

    var parts = new Dictionary<string, string>();
    foreach (string component in components)
    {
        // [0] = [t]=1793067124
        // [1] = [h]=content-type x-custom-header x-event-id x-event-type
        // [2] = [v1]=39a9b17c6d441b6a372c999dc7eccffddb8e8381c1fe3440a615a3b72777b23a
        List<string> componentPair = component.Split("=").ToList();
        parts.TryAdd(componentPair[0], componentPair[1]); // Doesn't override the element
    }
    
    // [0] = "content-type"
    // [1] = "x-custom-header"
    // [2] = "x-event-id"
    // [3] = "x-event-type"
    List<string> headerNames = parts["h"].Split(" ").ToList();

    // Get the actual headers from the request
    var valueList = new List<StringValues>();
    foreach (string h in headerNames)
    {
        if (!StringValues.IsNullOrEmpty(headers[h]))
        {
            valueList.Add(headers[h]);
        }
    }

    string headerValues = string.Join('.', valueList);

    // Use raw body directly
    string rawBodyStr = Encoding.UTF8.GetString(rawBody);
    string signedData = !string.IsNullOrWhiteSpace(parts["h"])
        ? $"{parts["t"]}.{parts["h"]}.{headerValues}.{rawBodyStr}"
        : $"{parts["t"]}.{rawBodyStr}";

    // HMAC is your go-to for internal services needing fast, stateless auth. Like
    // an order service pinging inventory. Avoid using it for public-facing APIs
    // (use OAuth or JWT instead) or when encryption is a must (stick with TLS).
    // Refer to https://www.ronnydelgado.com/my-blog/security-hmac-authentication-demo
    //
    // 
    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    byte[] signedDataBytes = Encoding.UTF8.GetBytes(signedData);

    using var hmac = new HMACSHA256(secretBytes);
    byte[] computedHash = hmac.ComputeHash(signedDataBytes);
    string computedToken = Convert.ToHexStringLower(computedHash);

    // FIXME: Doesn't match
    // parts.v1         = 39a9b17c6d441b6a372c999dc7eccffddb8e8381c1fe3440a615a3b72777b23a
    // computedToken    = fe3e7ce89768840e3f5121e2f604001f00ed1791e5c74ed04ebe565e6f1ddbaf

    // I think there's something lacking in my test request/headers/body etc.

    // Try #1: Generate another webhook event
    // Try #2: Read the reference code again (maybe I did something different)

    return false;
}

// Access 'HttpRequest' instance in endpoint
// Refer to https://stackoverflow.com/questions/70210260/how-to-get-httprequest-from-minimal-api-action-method
app.MapPost("/webhooks/secure", async (HttpRequest request) =>
{
    // PowerShell command to set an env variable at the user level:
    // [System.Environment]::SetEnvironmentVariable("WEBHOOK_SECRET", "PLACE_YOUR_SUBSCRIPTION_SECRET", "User")
    // 
    // Get environment variable value at the user level.
    string? secret = Environment.GetEnvironmentVariable("WEBHOOK_SECRET", EnvironmentVariableTarget.User);
    if (string.IsNullOrWhiteSpace(secret))
    {
        // Add proper logging here
        return Results.Unauthorized();
    }

    var key = "x-hook0-signature";
    string? signature = request.Headers[key];
    if (string.IsNullOrWhiteSpace(signature))
    {
        // Add proper logging here
        return Results.Unauthorized();
    }

    // Read HttpRequest Body into byte array
    // Refer to https://stackoverflow.com/questions/9948463/read-http-request-into-byte-array
    var memoryStream = new MemoryStream();
    await request.BodyReader.CopyToAsync(memoryStream);
    byte[] rawBody = memoryStream.ToArray();

    if (!VerifyWebhookSignature(rawBody, signature, request.Headers, secret))
    {
        // Add proper logging
        return Results.Unauthorized();
    }

    return Results.Ok(new { Status = "verified", Signature = signature });
})
.WithName("VerifyWebhookSignature");

// Request Modification Middleware allows you to modify incoming HTTP requests before
// they reach your endpoints.
// Refer to https://www.c-sharpcorner.com/article/middleware-in-minimal-api-with-net-8
app.Use(async (context, next) =>
{
    // Reading request body in middleware
    // Refer to https://stackoverflow.com/questions/67768912/reading-request-body-in-middleware-for-net-5
    HttpRequest httpRequest = context.Request;
    if (httpRequest.Method == HttpMethods.Post && httpRequest.ContentLength > 0)
    {
        // In .NET, the request body stream is forward-only by default. If you read it once (like for 
        // HMAC verification), it's gone for downstream code. Buffering loads it into memory, so you
        // can rewind it with Body.Position = 0. This is crucial for middleware setups where multiple
        // components need the body.
        // Refer to https://www.ronnydelgado.com/my-blog/security-hmac-authentication-demo
        httpRequest.EnableBuffering();

        var buffer = new byte[Convert.ToInt32(httpRequest.ContentLength)];
        //await httpRequest.Body.ReadAsync(buffer, 0, buffer.Length);
        await httpRequest.Body.ReadExactlyAsync(buffer);

        httpRequest.Body.Position = 0; // Rewinding the stream to 0
    }

    await next(context);
});

app.Run();
