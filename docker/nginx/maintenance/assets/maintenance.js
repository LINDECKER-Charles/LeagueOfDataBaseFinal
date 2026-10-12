// The maintenance page that nginx answers while a deploy replaces the stack
// (server.d/maintenance.conf). A classic script of <head>, after messages.js: it paints the
// visitor's identity and language before the first frame, then, once the page is parsed, writes
// the texts and the estimate and asks nginx every few seconds whether the maintenance is over.
//
// It reads the same cookies as the site (lod_theme, lod_prefs), in the browser only, as the
// site does. Mirror of src/app/core/theme (identities and browser colours) and of
// src/app/core/routing/locale/negotiate-locale.ts: tools/nginx/test/maintenance-page.test.mjs
// runs this very script against those lists.
(function () {
  'use strict';

  var THEMES = ['hextech', 'zaun', 'noxus', 'spirit-blossom'];
  var BROWSER_COLORS = ['#010a13', '#030706', '#08090b', '#07070e'];
  var THEME_COOKIE = 'lod_theme';
  // `loc=<locale>&l=<lang>&v=<version>`, written by the context switcher.
  var PREFERENCES_COOKIE = 'lod_prefs';
  var REMEMBERED_LOCALE = /(?:^|&)loc=([a-z]{2}(?:-han[st])?)(?:&|$)/;
  var DEFAULT_LOCALE = 'en';
  var RIGHT_TO_LEFT_LOCALES = ['ar'];
  // Chinese is split by script; a tag that names none goes by its region.
  var TRADITIONAL_REGIONS = ['tw', 'hk', 'mo'];
  // nginx answers the deploy's state there while it lasts, then a 404 (server.d/maintenance.conf).
  var STATUS_URL = '/maintenance/status.json';
  var NOT_FOUND = 404;
  var POLL_INTERVAL_MS = 15000;
  var MS_PER_SECOND = 1000;
  var SECONDS_PER_MINUTE = 60;
  // What a deploy usually takes, shown until the host has measured one
  // (tools/deploy/maintenance.sh).
  var TYPICAL_MINUTES = { from: 10, to: 15 };

  var messages = window.lodbMaintenanceMessages || {};
  var root = document.documentElement;
  var state = { status: null, clockOffsetMs: 0 };

  function cookieValue(name) {
    var entries = [];
    try {
      entries = document.cookie.split(';');
    } catch (unreadable) {
      // Sandboxed documents throw on cookie access: the defaults apply.
    }
    for (var i = 0; i < entries.length; i++) {
      var entry = entries[i].trim();
      if (entry.indexOf(name + '=') === 0) {
        return entry.slice(name.length + 1);
      }
    }
    return '';
  }

  function paintTheme() {
    var index = Math.max(THEMES.indexOf(cookieValue(THEME_COOKIE)), 0);
    root.setAttribute('data-theme', THEMES[index]);
    var color = document.querySelector('meta[name="theme-color"]');
    if (color) {
      color.setAttribute('content', BROWSER_COLORS[index]);
    }
  }

  function isLocale(value) {
    return Object.prototype.hasOwnProperty.call(messages, value);
  }

  function chineseLocale(subtags) {
    if (subtags.indexOf('hant') !== -1) {
      return 'zh-hant';
    }
    if (subtags.indexOf('hans') !== -1) {
      return 'zh-hans';
    }
    var isTraditional = subtags.some(function (subtag) {
      return TRADITIONAL_REGIONS.indexOf(subtag) !== -1;
    });
    return isTraditional ? 'zh-hant' : 'zh-hans';
  }

  function localeOfTag(tag) {
    var subtags = String(tag).toLowerCase().split('-');
    if (subtags[0] === 'zh') {
      return chineseLocale(subtags);
    }
    return isLocale(subtags[0]) ? subtags[0] : null;
  }

  function browserLocale() {
    var tags = navigator.languages || [navigator.language];
    for (var i = 0; i < tags.length; i++) {
      var locale = tags[i] ? localeOfTag(tags[i]) : null;
      if (locale) {
        return locale;
      }
    }
    return DEFAULT_LOCALE;
  }

  // The page's own locale first (/{locale}/…), then the one the visitor asked the switcher to
  // remember, then the browser's languages, as `/` negotiates them.
  function chooseLocale() {
    var segment = location.pathname.split('/')[1] || '';
    if (isLocale(segment)) {
      return segment;
    }
    var remembered = REMEMBERED_LOCALE.exec(cookieValue(PREFERENCES_COOKIE));
    return remembered && isLocale(remembered[1]) ? remembered[1] : browserLocale();
  }

  function paintLocale(locale) {
    root.setAttribute('lang', locale);
    root.setAttribute('dir', RIGHT_TO_LEFT_LOCALES.indexOf(locale) === -1 ? 'ltr' : 'rtl');
    if (messages[locale]) {
      document.title = messages[locale].documentTitle;
    }
  }

  function minutesFormat(locale) {
    return new Intl.NumberFormat(locale, { style: 'unit', unit: 'minute', unitDisplay: 'long' });
  }

  function typicalDuration(locale) {
    var format = minutesFormat(locale);
    if (typeof format.formatRange === 'function') {
      return format.formatRange(TYPICAL_MINUTES.from, TYPICAL_MINUTES.to);
    }
    return format.format(TYPICAL_MINUTES.from) + ' – ' + format.format(TYPICAL_MINUTES.to);
  }

  function isPositiveNumber(value) {
    return typeof value === 'number' && isFinite(value) && value > 0;
  }

  function hasEstimate(status) {
    return (
      !!status && isPositiveNumber(status.startedAt) && isPositiveNumber(status.expectedSeconds)
    );
  }

  // The label and value of the estimate, and which of them it is (page.css sets a sentence smaller
  // than a duration): the time left on the last deploy's duration, measured on the host, or the
  // usual duration when the host has none.
  function estimateOf(locale, text) {
    var status = state.status;
    if (!hasEstimate(status)) {
      return { kind: 'typical', label: text.typicalLabel, value: typicalDuration(locale) };
    }
    var nowSeconds = (Date.now() + state.clockOffsetMs) / MS_PER_SECOND;
    var leftSeconds = status.startedAt + status.expectedSeconds - nowSeconds;
    if (leftSeconds <= 0) {
      return { kind: 'overdue', label: text.remainingLabel, value: text.overdue };
    }
    var minutes = Math.max(1, Math.ceil(leftSeconds / SECONDS_PER_MINUTE));
    var value = minutesFormat(locale).format(minutes);
    return { kind: 'remaining', label: text.remainingLabel, value: value };
  }

  // Writes only what changed: the estimate is a live region, read again on every change.
  function writeText(element, text) {
    if (element && element.textContent !== text) {
      element.textContent = text;
    }
  }

  function render(locale) {
    var text = messages[locale];
    if (!text) {
      return;
    }
    var elements = document.querySelectorAll('[data-message]');
    for (var i = 0; i < elements.length; i++) {
      writeText(elements[i], text[elements[i].getAttribute('data-message')]);
    }
    var estimate = estimateOf(locale, text);
    writeText(document.querySelector('[data-estimate-label]'), estimate.label);
    var value = document.querySelector('[data-estimate-value]');
    writeText(value, estimate.value);
    if (value) {
      value.setAttribute('data-estimate-kind', estimate.kind);
    }
  }

  // The host's clock, from the answer's Date: a visitor's clock may be minutes off.
  function readStatus(response) {
    var serverDate = Date.parse(response.headers.get('Date') || '');
    return response.json().then(function (status) {
      state.status = status;
      state.clockOffsetMs = isFinite(serverDate) ? serverDate - Date.now() : 0;
    });
  }

  // Over when nginx no longer has a state to answer: the page asked for comes back. Any other
  // failure (the edge between two nginx containers, a dropped connection) keeps on waiting.
  function poll(locale) {
    return fetch(STATUS_URL, { cache: 'no-store', credentials: 'same-origin' })
      .then(function (response) {
        if (response.status === NOT_FOUND) {
          location.reload();
          return null;
        }
        return response.ok ? readStatus(response) : null;
      })
      .catch(function () {
        return null;
      })
      .then(function () {
        render(locale);
      });
  }

  function start(locale) {
    render(locale);
    poll(locale);
    setInterval(function () {
      poll(locale);
    }, POLL_INTERVAL_MS);
  }

  paintTheme();
  var locale = chooseLocale();
  paintLocale(locale);
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      start(locale);
    });
  } else {
    start(locale);
  }
})();
