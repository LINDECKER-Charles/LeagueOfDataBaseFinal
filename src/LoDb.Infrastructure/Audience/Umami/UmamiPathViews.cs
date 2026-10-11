namespace LoDb.Infrastructure.Audience.Umami;

/// <summary>A path as Umami counted it: its page views and its distinct visitors.</summary>
internal sealed record UmamiPathViews(string Path, long Views, long Visitors);
