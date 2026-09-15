using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Linq;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Task 8.2 (Application): gzip + brotli response compression for every JSON/CSV/XLSX
/// payload the API returns. Compressing authenticated payloads is safe here — the
/// CRIME/BREACH class of attacks targets secrets reflected alongside attacker-chosen
/// input in the *same* compressed body (session cookies negotiated into login pages);
/// our payloads are bulk data (assets, tickets, exports) whose shape does not leak
/// the JWT itself, and the Authorization header is never part of the response body.
/// SignalR hub traffic rides WebSocket frames the compressor never touches, and the
/// health endpoints answer in a few hundred bytes where compression is a wash.
/// </summary>
public static class ResponseCompressionExtensions
{
    public static IServiceCollection AddDarabanResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            // Extend the defaults (json, text/plain, text/csv are already in) with the
            // binary spreadsheet format so large XLSX exports also shrink on the wire.
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/csv",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            });
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);

        return services;
    }
}
