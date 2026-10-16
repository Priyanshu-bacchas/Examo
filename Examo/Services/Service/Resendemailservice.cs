using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Examo.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Examo.Services;

public class ResendOptions
{
    // Render env var:  Resend__ApiKey
    public string ApiKey { get; set; } = string.Empty;

    // Render env var:  Resend__From     e.g.  Examo <no-reply@yourdomain.com>
    public string From { get; set; } = "Examo <onboarding@resend.dev>";
}

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly ResendOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IOptions<ResendOptions> options,
        IHostEnvironment env,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task SendOtpAsync(
        string toEmail,
        string name,
        string otp,
        string purpose)
    {
        var isReset = purpose == "reset";

        var subject = isReset
            ? "Examo password reset code"
            : "Verify your Examo account";

        var heading = isReset
            ? "Reset your password"
            : "Verify your email";

        var intro = isReset
            ? "Use this code to reset your Examo password."
            : "Use this code to finish creating your Examo account.";

        // Local testing: API key nahi hai to OTP console me print karo
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning(
                    "RESEND API KEY MISSING (dev mode). OTP for {Email} = {Otp}",
                    toEmail,
                    otp);

                return;
            }

            throw new InvalidOperationException(
                "Email service is not configured.");
        }

        var safeName = WebUtility.HtmlEncode(name);

        var html = $@"
<div style=""font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:24px;"">
  <h2 style=""color:#1b2a52;margin:0 0 8px;"">{heading}</h2>
  <p style=""color:#444;"">Hi {safeName},</p>
  <p style=""color:#444;"">{intro}</p>
  <div style=""font-size:34px;letter-spacing:10px;font-weight:bold;color:#1e3a8a;
              background:#eef3ff;border-radius:10px;padding:16px;text-align:center;margin:20px 0;"">
    {otp}
  </div>
  <p style=""color:#666;font-size:13px;"">This code expires in 10 minutes.
     If you did not request it, you can safely ignore this email.</p>
</div>";

        var payload = JsonSerializer.Serialize(new
        {
            from = _options.From,
            to = new[] { toEmail },
            subject,
            html
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        // Resend bina User-Agent ke 403 deta hai
        request.Headers.UserAgent.ParseAdd("Examo/1.0");

        request.Content =
            new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend request failed.");

            throw new InvalidOperationException(
                "Could not send the email right now. Please try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            _logger.LogError(
                "Resend rejected the email ({Status}): {Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                "Could not send the email right now. Please try again.");
        }
    }
}