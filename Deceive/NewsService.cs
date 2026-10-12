using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Deceive;

internal sealed class NewsItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Date { get; set; } = "";
    public string? Url { get; set; }
    public string ButtonText { get; set; } = "Saiba mais";
}

/// Downloads the "Novidades" feed published on the SkoreLoBoost site.
/// The feed is plain data (title, text, optional link). It is never rendered as HTML,
/// and links are only opened if they point to SkoreLoBoost's own pages.
internal static class NewsService
{
    internal const string FeedUrl = "https://skoreloboost.com/status-news.json";

    private const int MaxItems = 10;
    private const int MaxFeedChars = 100_000;

    /// Returns the latest news. If the site can't be reached, returns the last saved copy and Online = false.
    internal static async Task<(List<NewsItem> Items, bool Online)> FetchAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkoreLoBoostStatus", Utils.DeceiveVersion));

            var content = await httpClient.GetStringAsync(FeedUrl);
            if (content.Length > MaxFeedChars)
                throw new InvalidDataException("News feed is too large.");

            var items = Parse(content);
            Persistence.SetNewsCache(content);
            return (items, true);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to fetch news: {ex.Message}");
            try
            {
                var cached = Persistence.GetNewsCache();
                return (string.IsNullOrEmpty(cached) ? new List<NewsItem>() : Parse(cached), false);
            }
            catch
            {
                return (new List<NewsItem>(), false);
            }
        }
    }

    internal static List<NewsItem> Parse(string json)
    {
        var result = new List<NewsItem>();
        var array = JsonSerializer.Deserialize<JsonNode>(json)?["items"]?.AsArray();
        if (array is null)
            return result;

        foreach (var node in array)
        {
            if (result.Count >= MaxItems)
                break;

            var title = Limit(node?["title"]?.ToString(), 80);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var url = node?["url"]?.ToString();
            var button = Limit(node?["button"]?.ToString(), 24);
            result.Add(new NewsItem
            {
                Id = Limit(node?["id"]?.ToString(), 64) is { Length: > 0 } id ? id : title,
                Title = title,
                Text = Limit(node?["text"]?.ToString(), 600),
                Date = Limit(node?["date"]?.ToString(), 20),
                Url = IsAllowedUrl(url) ? url : null,
                ButtonText = string.IsNullOrWhiteSpace(button) ? "Saiba mais" : button
            });
        }

        return result;
    }

    /// Only SkoreLoBoost's site, its Discord invite and this project's GitHub pages may be opened from the feed.
    internal static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host is "skoreloboost.com" or "www.skoreloboost.com"
               || (host == "discord.gg" && uri.AbsolutePath.StartsWith("/vFp2YRZFCy", StringComparison.Ordinal))
               || (host == "github.com" && uri.AbsolutePath.StartsWith("/Helyver12/", StringComparison.OrdinalIgnoreCase));
    }

    private static string Limit(string? value, int max)
    {
        value = (value ?? "").Trim();
        return value.Length <= max ? value : value.Substring(0, max);
    }
}
