using System.Security.Cryptography;
using System.Text;

namespace SlackActionTracker.Middleware;

public class SlackSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _signingSecret;

    // These paths are NOT signed by Slack — skip verification for them
    private static readonly string[] _unsignedPaths = new[]
    {
        "/slack/oauth/callback"
    };

    public SlackSignatureMiddleware(RequestDelegate next)
    {
        _next = next;
        _signingSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? "";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip signature check for non-Slack routes and OAuth callback
        if (!path.StartsWith("/slack") || _unsignedPaths.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();

        var timestamp = context.Request.Headers["X-Slack-Request-Timestamp"].ToString();
        var signature = context.Request.Headers["X-Slack-Signature"].ToString();

        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (VerifySignature(_signingSecret, timestamp, signature, body))
        {
            await _next(context);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
    }

    private static bool VerifySignature(string secret, string timestamp, string signature, string body)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            return false;

        var basestring = $"v0:{timestamp}:{body}";
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(basestring);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(bodyBytes);
        var computed = "v0=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        return computed == signature;
    }
}
