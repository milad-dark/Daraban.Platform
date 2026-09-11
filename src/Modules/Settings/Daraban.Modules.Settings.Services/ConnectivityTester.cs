using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Services.Dtos;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Connectivity testers behind POST /api/v1/settings/test/{key} (Task 7.4). The route key
/// selects a category: any "email.*" key tests SMTP, any "ldap.*" key tests the directory.
/// Implementations are deliberately low-level (raw sockets, no MailKit/LDAP client yet):
/// the point is to answer "are these coordinates reachable and speaking the protocol",
/// not to send a real email or perform a real bind. Swapping in full clients later means
/// replacing the innards of one class each, contract unchanged.
///
/// Security: never logs or echoes the password; error text returned to the caller is a
/// fixed category message plus the exception type -- server exception strings can carry
/// internal hostnames/paths and must not be relayed to the browser.
/// </summary>
public interface IConnectivityTester
{
    Task<ConnectionTestResultDto> TestEmailAsync(SmtpSettings settings, CancellationToken ct = default);
    Task<ConnectionTestResultDto> TestLdapAsync(LdapConnectionSettings settings, CancellationToken ct = default);
}

public class ConnectivityTester(ILogger<ConnectivityTester> logger) : IConnectivityTester
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    public async Task<ConnectionTestResultDto> TestEmailAsync(SmtpSettings s, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.Host))
        {
            return new ConnectionTestResultDto(false, "SMTP host is not configured.", 0);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ConnectTimeout);

            await tcp.ConnectAsync(s.Host, s.Port, timeoutCts.Token);
            var (stream, tlsApplied) = await BuildStreamAsync(tcp, s.TlsMode, s.Host, timeoutCts.Token);
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: false);

            // Read the 220 greeting, then say NOOP (safe no-op command that requires no
            // auth on every sane server) and check for 250. QUIT cleanly, close, done.
            var greeting = await reader.ReadLineAsync(timeoutCts.Token);
            if (greeting is null || !greeting.StartsWith("220", StringComparison.Ordinal))
            {
                return Fail(sw, "The server did not send an SMTP greeting.", greeting);
            }

            var noopResponse = await IssueCommandAsync(stream, reader, "NOOP", timeoutCts.Token);
            if (!noopResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return Fail(sw, "The server did not accept the NOOP handshake.", noopResponse);
            }

            await IssueCommandAsync(stream, reader, "QUIT", timeoutCts.Token);

            logger.LogInformation("SMTP connectivity test to {Host}:{Port} succeeded in {Elapsed} ms", s.Host, s.Port, sw.ElapsedMilliseconds);
            return Ok(sw, tlsApplied
                ? "SMTP connection established over TLS."
                : "SMTP connection established (no TLS applied -- consider starttls or ssl).");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ConnectionTestResultDto(false, "Connection timed out after 5 seconds.", (int)sw.ElapsedMilliseconds);
        }
        catch (SocketException ex)
        {
            logger.LogWarning("SMTP test to {Host}:{Port} failed: {SocketError}", s.Host, s.Port, ex.SocketErrorCode);
            return new ConnectionTestResultDto(false, "Could not reach the SMTP host.", (int)sw.ElapsedMilliseconds);
        }
        catch (AuthenticationException)
        {
            return new ConnectionTestResultDto(false, "TLS handshake failed -- check certificates and the configured TLS mode.", (int)sw.ElapsedMilliseconds);
        }
    }

    public async Task<ConnectionTestResultDto> TestLdapAsync(LdapConnectionSettings s, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.Server))
        {
            return new ConnectionTestResultDto(false, "LDAP server is not configured.", 0);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var ping = new Ping();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ConnectTimeout);

            // Cheap liveness check; the full bind/search lands with the LDAP module.
            var reply = await ping.SendPingAsync(s.Server, ConnectTimeout);
            return reply.Status == IPStatus.Success
                ? Ok(sw, $"Directory host responded (round-trip {(int)reply.RoundtripTime} ms). Full bind test lands with the LDAP sync module.")
                : new ConnectionTestResultDto(false, "Directory host did not respond to ping.", (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ConnectionTestResultDto(false, "Connection timed out after 5 seconds.", (int)sw.ElapsedMilliseconds);
        }
        catch (PingException)
        {
            return new ConnectionTestResultDto(false, "Could not reach the directory host.", (int)sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Wraps the TCP stream per the configured TLS mode; returns whether TLS was applied.</summary>
    private static async Task<(Stream Stream, bool TlsApplied)> BuildStreamAsync(
        TcpClient tcp, string tlsMode, string host, CancellationToken ct)
    {
        var plain = tcp.GetStream();
        if (tlsMode.Equals("ssl", StringComparison.OrdinalIgnoreCase))
        {
            var ssl = new SslStream(plain, leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                // Platform defaults: TLS 1.2+ and the OS certificate store -- never disable
                // certificate validation for a "test" button.
            }, ct);
            return (ssl, true);
        }

        return (plain, false);
    }

    private static async Task<string> IssueCommandAsync(
        Stream stream, StreamReader reader, string command, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(command + "\r\n");
        await stream.WriteAsync(bytes, ct);
        return await reader.ReadLineAsync(ct) ?? string.Empty;
    }

    private static ConnectionTestResultDto Ok(Stopwatch sw, string message) =>
        new(true, message, (int)sw.ElapsedMilliseconds);

    private static ConnectionTestResultDto Fail(Stopwatch sw, string message, string? serverLine)
    {
        // Include only the leading protocol verb, never the raw server line: greetings can
        // carry internal hostnames and we do not relay them to the browser.
        var verb = serverLine is { Length: >= 3 } ? $" (server answered '{serverLine[..3]}')" : string.Empty;
        return new ConnectionTestResultDto(false, message + verb, (int)sw.ElapsedMilliseconds);
    }
}
