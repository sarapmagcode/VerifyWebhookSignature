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

static bool VerifyWebhookSignature(string rawBody, string signature, IHeaderDictionary headers, string secret)
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
        else
        {
            valueList.Add(string.Empty);
        }
    }

    string headerValues = string.Join('.', valueList);

    // Use raw body directly
    string signedData = !string.IsNullOrWhiteSpace(parts["h"])
        ? $"{parts["t"]}.{parts["h"]}.{headerValues}.{rawBody}"
        : $"{parts["t"]}.{rawBody}";

    // HMAC is your go-to for internal services needing fast, stateless auth. Like
    // an order service pinging inventory. Avoid using it for public-facing APIs
    // (use OAuth or JWT instead) or when encryption is a must (stick with TLS).
    // Refer to https://www.ronnydelgado.com/my-blog/security-hmac-authentication-demo
    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    byte[] signedDataBytes = Encoding.UTF8.GetBytes(signedData);

    using var hmac = new HMACSHA256(secretBytes);
    byte[] computedHash = hmac.ComputeHash(signedDataBytes);
    string expected = Convert.ToHexStringLower(computedHash);

    return parts["v1"] == expected;
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

    string? signature = request.Headers["x-hook0-signature"];
    if (string.IsNullOrWhiteSpace(signature))
    {
        // Add proper logging here
        return Results.Unauthorized();
    }

    // Read HttpRequest Body into byte array
    // Refer to https://stackoverflow.com/questions/9948463/read-http-request-into-byte-array
    using MemoryStream memoryStream = new();
    await request.BodyReader.CopyToAsync(memoryStream);
    string rawBodyStr = Encoding.UTF8.GetString(memoryStream.ToArray());

    if (!VerifyWebhookSignature(rawBodyStr, signature, request.Headers, secret))
    {
        // Add proper logging
        return Results.Unauthorized();
    }

    return Results.Ok(new { Status = "verified", Signature = signature });
})
.WithName("VerifyWebhookSignature");

app.Run();
