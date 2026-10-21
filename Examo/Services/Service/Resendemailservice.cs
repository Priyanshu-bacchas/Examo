using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Examo.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Examo.Services;

public class ResendOptions
{
    // Render env var:  Resend__ApiKey   (ya RESEND_API_KEY bhi chalega)
    public string ApiKey { get; set; } = string.Empty;

    // Render env var:  Resend__From     (ya RESEND_FROM bhi chalega)
    // Khaali ho to neeche DefaultFrom use hota hai.
    public string From { get; set; } = string.Empty;
}

public class ResendEmailService : IEmailService
{
    // Tumhara Resend-verified domain
    private const string DefaultFrom =
        "Examo <no-reply@examoaspirants.co.in>";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _from;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IOptions<ResendOptions> options,
        IHostEnvironment env,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _env = env;
        _logger = logger;

        var o = options.Value;

        _apiKey = FirstNonEmpty(
            o.ApiKey,
            Environment.GetEnvironmentVariable("RESEND_API_KEY"));

        _from = FirstNonEmpty(
            o.From,
            Environment.GetEnvironmentVariable("RESEND_FROM"),
            DefaultFrom);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning(
                "Resend API key is NOT configured. Emails will not be sent.");
        }
        else
        {
            _logger.LogInformation(
                "Resend configured. From = {From}, key = {KeyStart}...",
                _from,
                _apiKey.Length >= 6 ? _apiKey[..6] : "?");
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return string.Empty;
    }

    public async Task SendOtpAsync(
        string toEmail,
        string name,
        string otp,
        string purpose)
    {
        var isReset = purpose == "reset";
        var isLogin = purpose == "login";

        var subject = isReset
            ? "Your Examo password reset code"
            : isLogin
                ? "Your Examo login code"
                : "Your Examo verification code";

        var heading = isReset
            ? "Reset your password"
            : isLogin
                ? "Confirm your login"
                : "Verify your email";

        var intro = isReset
            ? "Use this code to reset your Examo password."
            : isLogin
                ? "Use this code to finish logging in to Examo."
                : "Use this code to finish creating your Examo account.";

        // Local testing: API key nahi hai to OTP console me print karo
        if (string.IsNullOrWhiteSpace(_apiKey))
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

        // Plain-text version: Gmail spam filter ke liye better
        var text =
            $"Hi {name},\n\n{intro}\n\nYour code: {otp}\n\n" +
            "This code expires in 10 minutes. " +
            "If you did not request it, you can ignore this email.\n\n- Examo";

        var payload = JsonSerializer.Serialize(new
        {
            from = _from,
            to = new[] { toEmail },
            subject,
            html,
            text
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

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

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Resend REJECTED email to {Email} ({Status}): {Body}",
                toEmail,
                (int)response.StatusCode,
                body);

            // Development me asli error screen par dikhao, production me generic
            throw new InvalidOperationException(
                _env.IsDevelopment()
                    ? $"Resend error {(int)response.StatusCode}: {body}"
                    : "Could not send the email right now. Please try again.");
        }

        // Is id ko Resend dashboard > Emails me dhoondh sakte ho
        _logger.LogInformation(
            "Resend ACCEPTED email to {Email} from {From}: {Body}",
            toEmail,
            _from,
            body);
    }
}
