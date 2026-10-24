using static LoDb.Infrastructure.Robots.Identification.VerificationDomains;

namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// Uptime monitors and audits. Lighthouse is not verified: anyone runs it from a browser.
/// </summary>
internal static class MonitoringRobots
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Monitoring,
        [
            new("uptimerobot", "UptimeRobot", "UptimeRobot", "uptimerobot"),
            new("pingdom", "Pingdom", "SolarWinds", "pingdom"),
            new("statuscake", "StatusCake", "StatusCake", "statuscake"),
            new("betterstack", "Better Stack", "Better Stack",
                "betteruptime|better uptime|betterstack"),
            new("site24x7", "Site24x7", "Zoho", "site24x7"),
            new("freshping", "Freshping", "Freshworks", "freshping"),
            new("hetrixtools", "HetrixTools", "HetrixTools", "hetrixtools"),
            new("checkly", "Checkly", "Checkly", "checkly"),
            new("datadog", "Datadog Synthetics", "Datadog", "datadog"),
            new("newrelic", "New Relic Synthetics", "New Relic", "newrelicpinger|newrelic"),
            new("uptime-kuma", "Uptime Kuma", "Uptime Kuma", "uptime-kuma"),
            new("blackbox-exporter", "Blackbox Exporter", "Prometheus",
                "blackbox exporter|blackbox_exporter"),
            new("gatus", "Gatus", "Gatus", "gatus"),
            new("lighthouse", "Lighthouse", "Google", "chrome-lighthouse|lighthouse"),
            new("gtmetrix", "GTmetrix", "GTmetrix", "gtmetrix"),
            new("w3c", "W3C Validator", "W3C", "w3c_validator|w3c-checklink|validator.nu"),
            new("google-site-verification", "Google Site Verification", "Google",
                "google-site-verification", Google),
            new("google-safety", "Google Safety", "Google", "google-safety", Google),
            new("cloudflare", "Cloudflare", "Cloudflare",
                "cloudflare-alwaysonline|cloudflare-healthchecks|cloudflare-traffic-manager"),
        ]);
}
