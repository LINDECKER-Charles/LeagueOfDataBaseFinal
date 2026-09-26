<?php
// Expected outputs of the legacy analytics, made by its own code: daily/{date}.json as
// app:analytics:rollup writes them from events.ndjson, report.json as the admin's "all" range
// builds it, visitors.json, the visitor ids of RequestEventFactory, and classify.json, what
// UserAgentParser and RefererClassifier make of a set of headers.
//
// Run from the repository root, with the PHP of the legacy stack:
//   php tests/LoDb.Infrastructure.Tests/Analytics/Legacy/generate.php
declare(strict_types=1);

$root = dirname(__DIR__, 4);
foreach (['Model/UserAgentProfile', 'Model/RefererSource', 'Model/RefererOrigin',
    'AnalyticsAggregator', 'RangeReportBuilder', 'UserAgentParser', 'RefererClassifier'] as $file) {
    require $root . "/app/src/Service/Analytics/{$file}.php";
}

use App\Service\Analytics\AnalyticsAggregator;
use App\Service\Analytics\RangeReportBuilder;
use App\Service\Analytics\RefererClassifier;
use App\Service\Analytics\UserAgentParser;

const VISITOR_SECRET = 'legacy-app-secret';
const JSON_FLAGS = JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE;

$byDay = [];
foreach (file(__DIR__ . '/events.ndjson', FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES) as $line) {
    $event = json_decode($line, true, flags: JSON_THROW_ON_ERROR);
    $byDay[substr($event['at'], 0, 10)][] = $event;
}
ksort($byDay);

$aggregator = new AnalyticsAggregator();
$dailies = [];
@mkdir(__DIR__ . '/daily');
foreach ($byDay as $date => $events) {
    $daily = $aggregator->aggregateDay($date, $events);
    file_put_contents(__DIR__ . "/daily/{$date}.json", json_encode($daily, JSON_FLAGS));
    // The report reads the stored files back, as AnalyticsReportService does.
    $dailies[] = json_decode(json_encode($daily, JSON_FLAGS), true);
}

$report = (new RangeReportBuilder())->build($dailies, 'all');
file_put_contents(
    __DIR__ . '/report.json',
    json_encode($report, JSON_FLAGS | JSON_PRETTY_PRINT) . "\n",
);

$cases = [
    ['ip' => '203.0.113.7', 'ua' => 'Mozilla/5.0 (X11; Linux x86_64) Firefox/140.0'],
    ['ip' => '2001:db8::7', 'ua' => 'curl/8.4.0'],
    ['ip' => '203.0.113.7', 'ua' => null],
    ['ip' => null, 'ua' => 'Mozilla/5.0'],
];
foreach ($cases as &$case) {
    $material = ($case['ip'] ?? 'unknown') . '|' . ($case['ua'] ?? '');
    $case['visitor'] = substr(hash_hmac('sha256', $material, VISITOR_SECRET), 0, 16);
}
unset($case);
file_put_contents(
    __DIR__ . '/visitors.json',
    json_encode(['secret' => VISITOR_SECRET, 'cases' => $cases], JSON_FLAGS | JSON_PRETTY_PRINT)
    . "\n",
);

$userAgents = [
    null,
    '',
    '   ',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6) AppleWebKit/605.1.15 (KHTML, like Gecko)'
    . ' Version/18.6 Safari/605.1.15',
    'Mozilla/5.0 (iPhone; CPU iPhone OS 18_6 like Mac OS X) AppleWebKit/605.1.15'
    . ' (KHTML, like Gecko) Version/18.6 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (iPad; CPU OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko)'
    . ' CriOS/140.0 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Mobile Safari/537.36',
    'Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' SamsungBrowser/27.0 Chrome/125.0.0.0 Safari/537.36',
    'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0',
    'Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Safari/537.36 OPR/120.0.0.0',
    'Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko',
    'Mozilla/5.0 (Windows Phone 10.0; Android 6.0.1; Microsoft; Lumia 950) AppleWebKit/537.36'
    . ' (KHTML, like Gecko) Chrome/52.0 Mobile Safari/537.36 Edge/15.14977',
    'Mozilla/5.0 (Linux; Android 11; KFTRWI) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Silk/128.0 like Chrome/128.0 Safari/537.36',
    'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' HeadlessChrome/140.0.0.0 Safari/537.36',
    'facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)',
    'curl/8.4.0',
    'python-requests/2.32.3',
    'Go-http-client/2.0',
    'UptimeRobot/2.0; http://www.uptimerobot.com/',
    'Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)',
    'Chrome Lighthouse',
    'Opera/9.80 (Windows NT 6.1) Presto/2.12.388 Version/12.16',
    'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0'
    . ' YaBrowser/25.8 Safari/537.36',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko)'
    . ' Chrome/140.0.0.0 Safari/537.36 Vivaldi/7.5',
    'Mozilla/5.0 (PlayBook; U; RIM Tablet OS 2.1.0; en-US) AppleWebKit/536.2+ (KHTML, like'
    . ' Gecko) Version/7.2.1.0 Safari/536.2+',
    'Mozilla/5.0 (BlackBerry; U; BlackBerry 9900; en) AppleWebKit/534.11+ (KHTML, like Gecko)'
    . ' Version/7.1.0.346 Mobile Safari/534.11+',
    'SomethingElse/1.0',
];
$parser = new UserAgentParser();
$agents = array_map(static function (?string $ua) use ($parser): array {
    $profile = $parser->parse($ua);

    return [
        'ua' => $ua,
        'browser' => $profile->browser,
        'os' => $profile->os,
        'device' => $profile->device,
        'bot' => $profile->isBot,
    ];
}, $userAgents);

$appHost = 'league-of-data-base.com';
$refererList = [
    null,
    '',
    'not a url',
    'www.google.com/search?q=ahri',
    'https://www.google.com/search?q=ahri',
    'https://www.google.fr/',
    'https://duckduckgo.com/?q=lol',
    'https://search.yahoo.co.jp/search?p=lol',
    'https://www.bing.com/search?q=lol',
    'https://www.reddit.com/r/leagueoflegends/',
    'https://t.co/abc',
    'https://x.com/someone/status/1',
    'https://www.youtube.com/watch?v=x',
    'https://discord.com/channels/1/2',
    'https://m.facebook.com/',
    'https://mastodon.social/@lol',
    'https://league-of-data-base.com/champion/Ahri',
    'https://fr.league-of-data-base.com/objects',
    'https://LEAGUE-OF-DATA-BASE.COM/',
    'https://notleague-of-data-base.com/',
    'https://some-blog.example/post',
    'https://user:pass@blog.example:8443/path',
    'http://[2001:db8::1]:8080/page',
    '//cdn.example.org/page',
    'android-app://com.google.android.gm/',
    'https://www.pinterest.fr/pin/1',
];
$classifier = new RefererClassifier();
$referers = array_map(static function (?string $referer) use ($classifier, $appHost): array {
    $origin = $classifier->classify($referer, $appHost);

    return ['referer' => $referer, 'host' => $origin->host, 'source' => $origin->source->value];
}, $refererList);

file_put_contents(
    __DIR__ . '/classify.json',
    json_encode(
        ['appHost' => $appHost, 'userAgents' => $agents, 'referers' => $referers],
        JSON_FLAGS | JSON_PRETTY_PRINT,
    ) . "\n",
);
