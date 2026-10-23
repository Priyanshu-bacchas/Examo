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

    // Render env var:  Resend__ReplyTo  (ya RESEND_REPLY_TO bhi chalega)
    // Support reply par student "Reply" dabaye to is address par aayega.
    // Khaali ho to reply-to header nahi lagta (From no-reply hai).
    public string ReplyTo { get; set; } = string.Empty;
}

public class ResendEmailService : IEmailService
{
    // Tumhara Resend-verified domain
    private const string DefaultFrom =
        "Examo <no-reply@examoaspirants.co.in>";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _from;
    private readonly string _replyTo;
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

        _replyTo = FirstNonEmpty(
            o.ReplyTo,
            Environment.GetEnvironmentVariable("RESEND_REPLY_TO"));

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

        await SendAsync(toEmail, subject, html, text, null);
    }

    // =====================================================
    // SUPPORT REPLY (Admin > Help & Support)
    // =====================================================
    public async Task SendSupportReplyAsync(
        string toEmail,
        string name,
        string subject,
        string originalMessage,
        string reply)
    {
        var mailSubject = subject.StartsWith(
            "Re:",
            StringComparison.OrdinalIgnoreCase)
            ? subject
            : $"Re: {subject}";

        var safeName = WebUtility.HtmlEncode(name);

        var safeReply = WebUtility.HtmlEncode(reply)
            .Replace("\r\n", "\n")
            .Replace("\n", "<br>");

        var safeOriginal = WebUtility.HtmlEncode(originalMessage)
            .Replace("\r\n", "\n")
            .Replace("\n", "<br>");

        var html = $@"
<div style=""font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;padding:24px;"">
  <p style=""color:#444;"">Hi {safeName},</p>
  <div style=""color:#222;font-size:15px;line-height:1.65;"">{safeReply}</div>
  <p style=""color:#444;margin-top:24px;"">Regards,<br>Examo Support</p>
  <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0 14px;"">
  <p style=""color:#888;font-size:12px;margin:0 0 6px;"">Your message:</p>
  <div style=""color:#666;font-size:13px;line-height:1.6;border-left:3px solid #d1d5db;padding-left:12px;"">{safeOriginal}</div>
</div>";

        var text =
            $"Hi {name},\n\n{reply}\n\nRegards,\nExamo Support\n\n" +
            $"---- Your message ----\n{originalMessage}\n";

        await SendAsync(
            toEmail,
            mailSubject,
            html,
            text,
            string.IsNullOrWhiteSpace(_replyTo) ? null : _replyTo);
    }

    // =====================================================
    // NOTIFICATION EMAIL (form last date / exam reminder / result)
    // =====================================================
    public async Task SendNotificationEmailAsync(
        string toEmail,
        string name,
        string title,
        string message)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeTitle = WebUtility.HtmlEncode(title);

        var safeMessage = WebUtility.HtmlEncode(message)
            .Replace("\r\n", "\n")
            .Replace("\n", "<br>");

        var html = $@"
<div style=""font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;padding:24px;"">
  <h2 style=""color:#1b2a52;margin:0 0 12px;"">{safeTitle}</h2>
  <p style=""color:#444;"">Hi {safeName},</p>
  <p style=""color:#222;font-size:15px;line-height:1.65;"">{safeMessage}</p>
  <p style=""color:#444;margin-top:24px;"">Regards,<br>Team Examo</p>
  <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0 12px;"">
  <p style=""color:#999;font-size:12px;margin:0;"">You are receiving this because you use Examo to track your exams.</p>
</div>";

        var text =
            $"Hi {name},\n\n{message}\n\nRegards,\nTeam Examo\n";

        await SendAsync(toEmail, title, html, text, null);
    }

    // =====================================================
    // COMMON: Resend API call
    // =====================================================
    private async Task SendAsync(
        string toEmail,
        string subject,
        string html,
        string text,
        string? replyTo)
    {
        // Local testing: API key nahi hai to console me log karo
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning(
                    "RESEND API KEY MISSING (dev mode). Email to {Email}, subject: {Subject}",
                    toEmail,
                    subject);

                return;
            }

            throw new InvalidOperationException(
                "Email service is not configured.");
        }

        var data = new Dictionary<string, object>
        {
            ["from"] = _from,
            ["to"] = new[] { toEmail },
            ["subject"] = subject,
            ["html"] = html,
            ["text"] = text
        };

        if (!string.IsNullOrWhiteSpace(replyTo))
            data["reply_to"] = replyTo;

        var payload = JsonSerializer.Serialize(data);

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