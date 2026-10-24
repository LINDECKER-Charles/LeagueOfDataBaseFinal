-- Demo traffic, first part: the settings, the draws and the catalogue the robots and the
-- readers walk through. The parts run in their order, in one transaction
-- (../seed-admin-demo.sh).
\set days 90
\set hit_days 30
\set robot_scale 0.5
\set sessions 650

SET LOCAL TimeZone = 'UTC';
SET LOCAL statement_timeout = 0;
SELECT setseed(0.2026) \g /dev/null

-- Draws ---------------------------------------------------------------------------------------

CREATE FUNCTION pg_temp.pick(choices text[], weights float8[]) RETURNS text
LANGUAGE plpgsql VOLATILE AS $$
DECLARE
    total float8 := 0;
    draw float8;
BEGIN
    FOR i IN 1 .. cardinality(weights) LOOP total := total + weights[i]; END LOOP;
    draw := random() * total;
    FOR i IN 1 .. cardinality(weights) LOOP
        draw := draw - weights[i];
        IF draw < 0 THEN RETURN choices[i]; END IF;
    END LOOP;
    RETURN choices[cardinality(choices)];
END $$;

-- The first choices come more often, the more so as the bias grows.
CREATE FUNCTION pg_temp.skewed(choices text[], bias float8) RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT choices[1 + floor(power(random(), bias) * cardinality(choices))::int]
$$;

CREATE FUNCTION pg_temp.span(low float8, high float8, bias float8) RETURNS int
LANGUAGE sql VOLATILE AS $$ SELECT floor(low + (high - low) * power(random(), bias))::int $$;

-- The catalogue -------------------------------------------------------------------------------

CREATE TEMP TABLE seed_entry (resource text PRIMARY KEY, entries text[] NOT NULL) ON COMMIT DROP;
INSERT INTO seed_entry VALUES
    ('champions', ARRAY['Ahri', 'Yasuo', 'Jinx', 'LeeSin', 'Kaisa', 'Ezreal', 'Yone', 'Viego',
        'Lux', 'Thresh', 'Zed', 'Akali', 'Ambessa', 'Mel', 'Aurora', 'Jhin', 'Vayne',
        'MissFortune', 'Caitlyn', 'Darius', 'Garen', 'Irelia', 'Riven', 'Sylas', 'Lucian',
        'Senna', 'Nautilus', 'Leona', 'Morgana', 'Teemo', 'Fiora', 'Camille', 'Gwen', 'Aatrox',
        'Sett', 'Mordekaiser', 'Graves', 'Kindred', 'Khazix', 'Rengar', 'Evelynn', 'Elise',
        'Nidalee', 'Smolder', 'Hwei', 'Briar', 'Naafiri', 'Milio', 'KSante', 'Nilah', 'Belveth',
        'Zeri', 'Renata', 'Vex', 'Akshan', 'Seraphine', 'Samira', 'Rell', 'Yuumi', 'Pyke',
        'Neeko', 'Qiyana', 'Sona', 'Nami', 'Soraka', 'Janna', 'Lulu', 'Karma', 'Zyra', 'Brand',
        'Velkoz', 'Chogath', 'KogMaw', 'RekSai', 'MonkeyKing', 'Fiddlesticks']),
    ('items', ARRAY['3031-infinity-edge', '6672-kraken-slayer', '3078-trinity-force',
        '3157-zhonyas-hourglass', '3089-rabadons-deathcap', '6655-ludens-companion',
        '3153-blade-of-the-ruined-king', '3071-black-cleaver', '6333-deaths-dance',
        '3026-guardian-angel', '3020-sorcerers-shoes', '3047-plated-steelcaps',
        '3111-mercurys-treads', '3006-berserkers-greaves', '3065-spirit-visage',
        '3068-sunfire-aegis', '3075-thornmail', '3135-void-staff', '3165-morellonomicon',
        '3036-lord-dominiks-regards', '3033-mortal-reminder', '3094-rapid-firecannon',
        '3087-statikk-shiv', '6610-sundered-sky', '3161-spear-of-shojin',
        '3742-dead-mans-plate', '2065-shurelyas-battlesong', '3504-ardent-censer',
        '1036-long-sword', '1001-boots']),
    ('runes', ARRAY['8100-domination', '8000-precision', '8200-sorcery', '8400-resolve',
        '8300-inspiration']),
    ('summoners', ARRAY['SummonerFlash', 'SummonerDot', 'SummonerTeleport', 'SummonerSmite',
        'SummonerHeal', 'SummonerExhaust', 'SummonerBarrier', 'SummonerHaste', 'SummonerBoost',
        'SummonerSnowball']);

CREATE FUNCTION pg_temp.entry(resource text) RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT pg_temp.skewed(entries, 2.2) FROM seed_entry WHERE seed_entry.resource = entry.resource
$$;

CREATE FUNCTION pg_temp.resource() RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT pg_temp.pick(ARRAY['champions', 'items', 'runes', 'summoners'], ARRAY[54, 28, 11, 7])
$$;

-- The patches before the latest, newest first: the archive the crawlers walk through.
CREATE TEMP TABLE seed_version ON COMMIT DROP AS
SELECT array_agg(format('%s.%s.1', season, patch) ORDER BY season DESC, patch DESC) AS versions
FROM generate_series(10, 16) AS season
CROSS JOIN generate_series(1, 24) AS patch
WHERE season < 16 OR patch <= 20;

CREATE FUNCTION pg_temp.past_version() RETURNS text
LANGUAGE sql VOLATILE AS $$ SELECT pg_temp.skewed(versions, 1.8) FROM seed_version $$;

CREATE TEMP TABLE seed_locale (
    locale text PRIMARY KEY,
    lang text NOT NULL,
    weight float8 NOT NULL,
    countries text[] NOT NULL,
    country_weights float8[] NOT NULL) ON COMMIT DROP;
INSERT INTO seed_locale VALUES
    ('fr', 'fr_FR', 34, '{FR,BE,CH,CA,MA,LU}', '{78,7,5,5,3,2}'),
    ('en', 'en_US', 30, '{US,GB,CA,AU,PH,IN,NL,SE,SG,NZ,IE}', '{38,18,9,6,6,5,4,3,3,3,2}'),
    ('de', 'de_DE', 6, '{DE,AT,CH}', '{82,10,8}'),
    ('es', 'es_ES', 6, '{ES,MX,AR,CL,CO,PE}', '{45,25,12,8,6,4}'),
    ('pt', 'pt_BR', 4, '{BR,PT}', '{92,8}'),
    ('pl', 'pl_PL', 3, '{PL}', '{1}'),
    ('it', 'it_IT', 2.5, '{IT,CH}', '{95,5}'),
    ('ko', 'ko_KR', 2.5, '{KR}', '{1}'),
    ('tr', 'tr_TR', 2, '{TR}', '{1}'),
    ('ru', 'ru_RU', 2, '{RU,UA,KZ,BY}', '{60,20,10,10}'),
    ('vi', 'vi_VN', 1.5, '{VN}', '{1}'),
    ('ja', 'ja_JP', 1, '{JP}', '{1}'),
    ('zh-hans', 'zh_CN', 1, '{CN,SG,MY}', '{60,20,20}'),
    ('cs', 'cs_CZ', 0.8, '{CZ,SK}', '{85,15}'),
    ('ro', 'ro_RO', 0.8, '{RO,MD}', '{90,10}'),
    ('hu', 'hu_HU', 0.6, '{HU}', '{1}'),
    ('el', 'el_GR', 0.5, '{GR,CY}', '{85,15}'),
    ('th', 'th_TH', 0.5, '{TH}', '{1}'),
    ('id', 'id_ID', 0.5, '{ID}', '{1}'),
    ('zh-hant', 'zh_TW', 0.4, '{TW,HK}', '{80,20}'),
    ('ar', 'ar_AE', 0.4, '{SA,AE,EG}', '{40,30,30}');

-- Crawlers follow the hreflang links: every language, English first.
CREATE FUNCTION pg_temp.robot_locale() RETURNS text
LANGUAGE sql VOLATILE AS $$
    SELECT pg_temp.pick(
        ARRAY['en', 'fr', 'de', 'es', 'pt', 'pl', 'it', 'ko', 'ja', 'tr', 'ru', 'vi', 'zh-hans',
            'zh-hant', 'cs', 'ro', 'hu', 'el', 'th', 'id', 'ar'],
        ARRAY[22, 12, 6, 6, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2])
$$;
