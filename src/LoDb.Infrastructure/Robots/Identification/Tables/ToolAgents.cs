namespace LoDb.Infrastructure.Robots.Identification.Tables;

/// <summary>
/// HTTP libraries, scripts, headless browsers and network scanners: a library has no
/// operator, a scanner its project's.
/// </summary>
internal static class ToolAgents
{
    public static IReadOnlyList<RobotProfile> All { get; } = RobotTable.Of(
        RobotCategories.Tool,
        [
            new("curl", "curl", string.Empty, "curl/"),
            new("wget", "Wget", string.Empty, "wget/"),
            new("python-requests", "python-requests", string.Empty, "python-requests"),
            new("aiohttp", "aiohttp", string.Empty, "aiohttp"),
            new("httpx", "HTTPX", string.Empty, "python-httpx|httpx"),
            new("python-urllib", "Python urllib", string.Empty, "python-urllib"),
            new("scrapy", "Scrapy", string.Empty, "scrapy"),
            new("python", "Python", string.Empty, "python"),
            new("go-http-client", "Go http client", string.Empty, "go-http-client"),
            new("okhttp", "OkHttp", string.Empty, "okhttp"),
            new("java", "Java", string.Empty, "java/|apache-httpclient|jakarta"),
            new("node", "Node.js", string.Empty, "node-fetch|undici|axios|node/"),
            new("php", "PHP", string.Empty, "guzzlehttp|php/"),
            new("ruby", "Ruby", string.Empty, "ruby|faraday"),
            new("perl", "Perl", string.Empty, "libwww-perl|lwp::"),
            new("powershell", "PowerShell", string.Empty, "powershell"),
            new("headless-chrome", "Headless Chrome", string.Empty, "headlesschrome"),
            new("phantomjs", "PhantomJS", string.Empty, "phantomjs"),
            new("zgrab", "ZGrab", string.Empty, "zgrab"),
            new("masscan", "masscan", string.Empty, "masscan"),
            new("nmap", "Nmap", string.Empty, "nmap"),
            new("nuclei", "Nuclei", "ProjectDiscovery", "nuclei"),
            new("censys", "Censys", "Censys", "censysinspect"),
            new("expanse", "Expanse", "Palo Alto Networks", "expanse"),
            new("leakix", "LeakIX", "LeakIX", "l9explore|leakix"),
            new("internet-measurement", "Internet Measurement", "Driftnet",
                "internet-measurement"),
        ]);
}
