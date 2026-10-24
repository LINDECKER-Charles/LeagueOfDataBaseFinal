-- Demo traffic, second part: the robot journal. Each robot of the fleet comes back every day
-- of its stretch (days ago, from since to until) at about its pace, on the sections of its
-- mix; the same agent can come from several networks under several verdicts (a Googlebot
-- forged from a server farm). Every hit is folded into robot_daily, the last ones kept whole.

CREATE TEMP TABLE seed_mix (
    mix text PRIMARY KEY,
    sections text[] NOT NULL,
    weights float8[] NOT NULL) ON COMMIT DROP;
INSERT INTO seed_mix VALUES
    ('search', '{archive,detail,list,home,sitemap,robots,asset,page}', '{42,30,8,3,6,2,6,3}'),
    ('image', '{asset,detail}', '{85,15}'),
    ('ai', '{detail,archive,list,home,page,robots}', '{48,28,10,4,6,4}'),
    ('assistant', '{detail,list,home,page}', '{72,14,9,5}'),
    ('archive_heavy', '{archive,detail,list,robots}', '{70,22,6,2}'),
    ('seo', '{detail,archive,list,home,robots,sitemap,other,asset}', '{38,30,10,4,5,5,4,4}'),
    ('social', '{detail,home,list}', '{80,12,8}'),
    ('monitor', '{home}', '{1}'),
    ('audit', '{home,detail,list}', '{40,40,20}'),
    ('tool_api', '{api,detail,list,other}', '{60,20,12,8}'),
    ('scraper', '{detail,archive,list,api}', '{55,25,12,8}'),
    ('scan', '{other,asset,home}', '{55,40,5}'),
    ('undeclared', '{archive,detail,list}', '{68,27,5}');

CREATE TEMP TABLE seed_fleet (
    agent text NOT NULL,
    mix text NOT NULL REFERENCES seed_mix,
    per_day float8 NOT NULL,
    since int NOT NULL,
    until int NOT NULL,
    verdict text NOT NULL,
    network text,
    detection text NOT NULL DEFAULT 'agent',
    method text NOT NULL DEFAULT 'GET',
    growth float8 NOT NULL DEFAULT 0) ON COMMIT DROP;

INSERT INTO seed_fleet (agent, mix, per_day, since, until, verdict, network, growth) VALUES
    ('Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, '
        || 'like Gecko) Chrome/141.0.7390.122 Mobile Safari/537.36 (compatible; Googlebot/2.1; '
        || '+http://www.google.com/bot.html)', 'search', 1600, 89, 0, 'verified', 'googlebot.com',
        0.2),
    ('Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)', 'search', 380,
        89, 0, 'verified', 'googlebot.com', 0),
    ('Googlebot-Image/1.0', 'image', 140, 89, 0, 'verified', 'googlebot.com', 0),
    ('Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 (KHTML, '
        || 'like Gecko) Chrome/141.0.7390.122 Mobile Safari/537.36 (compatible; GoogleOther)',
        'search', 50, 70, 0, 'verified', 'googlebot.com', 0),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; bingbot/2.0; '
        || '+http://www.bing.com/bingbot.htm) Chrome/116.0.1938.76 Safari/537.36', 'search', 820,
        89, 0, 'verified', 'msn.com', 0),
    ('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) '
        || 'Version/17.4 Safari/605.1.15 (Applebot/0.1; +http://www.apple.com/go/applebot)',
        'search', 300, 89, 0, 'verified', 'apple.com', 0),
    ('Mozilla/5.0 (compatible; YandexBot/3.0; +http://yandex.com/bots)', 'search', 230, 89, 0,
        'verified', 'yandex.com', 0),
    ('Mozilla/5.0 (compatible; Baiduspider/2.0; +http://www.baidu.com/search/spider.html)',
        'search', 60, 89, 0, 'verified', 'baidu.com', 0),
    ('DuckDuckBot/1.1; (+http://duckduckgo.com/duckduckbot.html)', 'search', 35, 89, 0,
        'unverifiable', NULL, 0),
    ('Mozilla/5.0 (Linux; Android 7.0;) AppleWebKit/537.36 (KHTML, like Gecko) Mobile '
        || 'Safari/537.36 (compatible; PetalBot;+https://webmaster.petalsearch.com/site/petalbot)',
        'search', 110, 89, 0, 'verified', 'petalsearch.com', 0),
    ('Mozilla/5.0 (compatible; SeznamBot/4.0; +https://o-seznam.cz/napoveda/vyhledavani/en/'
        || 'seznambot-crawler/)', 'search', 25, 89, 0, 'verified', 'seznam.cz', 0),
    ('Mozilla/5.0 (compatible; Qwantbot/1.0_4193332; +https://help.qwant.com/bot/)', 'search',
        45, 89, 0, 'unverifiable', 'qwant.com', 0),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; '
        || '+https://openai.com/gptbot)', 'ai', 1250, 89, 0, 'unverifiable', NULL, 0.9),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko); compatible; OAI-SearchBot/1.0; '
        || '+https://openai.com/searchbot', 'ai', 180, 60, 0, 'unverifiable', NULL, 0.5),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko); compatible; ChatGPT-User/1.0; '
        || '+https://openai.com/bot', 'assistant', 85, 89, 0, 'unverifiable', NULL, 1.2),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; ClaudeBot/1.0; '
        || '+claudebot@anthropic.com)', 'ai', 700, 89, 0, 'unverifiable', NULL, 0.5),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Claude-User/1.0; '
        || '+Claude-User@anthropic.com)', 'assistant', 20, 45, 0, 'unverifiable', NULL, 0.6),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; PerplexityBot/1.0; '
        || '+https://perplexity.ai/perplexitybot)', 'ai', 140, 89, 0, 'unverifiable', NULL, 0),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Perplexity-User/1.0; '
        || '+https://perplexity.ai/perplexity-user)', 'assistant', 35, 89, 0, 'unverifiable',
        NULL, 0.4),
    ('Mozilla/5.0 (Linux; Android 5.0) AppleWebKit/537.36 (KHTML, like Gecko) Mobile '
        || 'Safari/537.36 (compatible; Bytespider; spider-feedback@bytedance.com)',
        'archive_heavy', 1500, 89, 20, 'unverifiable', NULL, 0),
    ('meta-externalagent/1.1 (+https://developers.facebook.com/docs/sharing/webmasters/'
        || 'crawler)', 'ai', 420, 89, 0, 'unverifiable', NULL, 0.3),
    ('Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Amazonbot/0.1; '
        || '+https://developer.amazon.com/support/amazonbot) Chrome/119.0.6045.214 Safari/537.36',
        'search', 260, 89, 0, 'verified', 'amazonbot.amazon', 0),
    ('CCBot/2.0 (https://commoncrawl.org/faq/)', 'ai', 2200, 33, 29, 'unverifiable',
        'amazonaws.com', 0),
    ('CCBot/2.0 (https://commoncrawl.org/faq/)', 'ai', 2200, 3, 1, 'unverifiable',
        'amazonaws.com', 0),
    ('Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)', 'seo', 650, 89, 0,
        'unverifiable', 'ahrefs.net', 0),
    ('Mozilla/5.0 (compatible; SemrushBot/7~bl; +http://www.semrush.com/bot.html)', 'seo', 480,
        89, 0, 'unverifiable', 'semrush.com', 0),
    ('Mozilla/5.0 (compatible; MJ12bot/v1.4.8; http://mj12bot.com/)', 'seo', 140, 89, 0,
        'unverifiable', NULL, 0),
    ('Mozilla/5.0 (compatible; DotBot/1.2; +https://opensiteexplorer.org/dotbot; '
        || 'help@moz.com)', 'seo', 55, 89, 0, 'unverifiable', 'amazonaws.com', 0),
    ('Mozilla/5.0 (compatible; DataForSeoBot/1.0; +https://dataforseo.com/dataforseo-bot)',
        'seo', 75, 89, 0, 'unverifiable', 'your-server.de', 0),
    ('facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)', 'social', 60,
        89, 0, 'unverifiable', NULL, 0),
    ('Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)', 'social', 45, 89, 0,
        'unverifiable', NULL, 0),
    ('Twitterbot/1.0', 'social', 20, 89, 0, 'unverifiable', NULL, 0),
    ('Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)', 'social', 6, 89, 0,
        'unverifiable', NULL, 0),
    ('Mozilla/5.0 (Linux; Android 11; moto g power (2022)) AppleWebKit/537.36 (KHTML, like '
        || 'Gecko) Chrome/141.0.0.0 Mobile Safari/537.36 Chrome-Lighthouse', 'audit', 12, 89, 0,
        'unverifiable', NULL, 0),
    ('python-requests/2.32.3', 'tool_api', 260, 89, 0, 'unverifiable', NULL, 0),
    ('curl/8.7.1', 'tool_api', 30, 89, 0, 'unverifiable', NULL, 0),
    ('Go-http-client/1.1', 'scan', 110, 89, 0, 'unverifiable', NULL, 0),
    ('Mozilla/5.0 zgrab/0.x', 'scan', 25, 89, 0, 'unverifiable', NULL, 0),
    ('Scrapy/2.11.2 (+https://scrapy.org)', 'scraper', 3200, 16, 15, 'unverifiable', NULL, 0),
    ('Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) '
        || 'HeadlessChrome/141.0.0.0 Safari/537.36', 'scraper', 60, 89, 0, 'unverifiable',
        'amazonaws.com', 0),
    ('Mozilla/5.0 (compatible; DataHarvestBot/2.1; +http://dataharvest.io/bot)',
        'archive_heavy', 380, 22, 0, 'unverifiable', NULL, 0),
    ('LoLStatsCrawler/0.3 (+contact@lolstats.gg)', 'seo', 90, 50, 0, 'unverifiable', 'ovh.net',
        0);

-- A rush of Applebot a few days ago, and the smartphone Googlebot forged from server farms.
INSERT INTO seed_fleet (agent, mix, per_day, since, until, verdict, network)
SELECT agent, mix, rush.per_day, rush.since, rush.until, rush.verdict, rush.network
FROM seed_fleet
JOIN (VALUES
    ('%Applebot/0.1%', 2600, 6, 4, 'verified', 'apple.com'),
    ('%Nexus 5X%Googlebot/2.1%', 45, 60, 0, 'mismatch', 'your-server.de'),
    ('%Nexus 5X%Googlebot/2.1%', 20, 40, 0, 'unresolved', NULL)
) AS rush (pattern, per_day, since, until, verdict, network)
    ON seed_fleet.agent LIKE rush.pattern;

INSERT INTO seed_fleet (agent, mix, per_day, since, until, verdict, network, method) VALUES
    ('Mozilla/5.0+(compatible; UptimeRobot/2.0; http://www.uptimerobot.com/)', 'monitor', 288,
        89, 0, 'unverifiable', 'uptimerobot.com', 'HEAD');

-- Readers' browsers past a reader's pace: a farm's crawl of one day, two steady ones.
INSERT INTO seed_fleet (agent, mix, per_day, since, until, verdict, network, detection) VALUES
    ('Mozilla/5.0 (X11; Linux aarch64) AppleWebKit/537.36 (KHTML, like Gecko) '
        || 'Chrome/141.0.0.0 Safari/537.36', 'undeclared', 9000, 5, 5, 'unverifiable', NULL,
        'rate'),
    ('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) '
        || 'Chrome/138.0.0.0 Safari/537.36', 'undeclared', 1400, 12, 0, 'unverifiable',
        'your-server.de', 'rate'),
    ('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) '
        || 'Chrome/138.0.0.0 Safari/537.36', 'undeclared', 500, 30, 20, 'unverifiable',
        'ovh.net', 'rate');
