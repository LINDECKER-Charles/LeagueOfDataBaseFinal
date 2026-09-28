import { TestBed } from '@angular/core/testing';
import { ExternalLinks } from './external-links';

describe('ExternalLinks', () => {
  let opened: string[];
  let links: HTMLElement;

  // Whether the click was taken from the WebView; jsdom's own navigation is then cancelled,
  // since it cannot load another page.
  function click(href: string, init: MouseEventInit = {}): boolean {
    const link = links.appendChild(document.createElement('a'));
    link.href = href;
    let taken = false;
    link.addEventListener('click', (event) => {
      taken = event.defaultPrevented;
      event.preventDefault();
    });
    const icon = link.appendChild(document.createElement('span'));
    icon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, ...init }));
    return taken;
  }

  beforeEach(() => {
    opened = [];
    links = document.body.appendChild(document.createElement('div'));
    TestBed.inject(ExternalLinks).intercept(async (url) => {
      opened.push(url);
    });
  });

  afterEach(() => {
    links.remove();
  });

  it('sends a link to another site to the system browser', () => {
    expect(click('https://www.leagueoflegends.com/fr-fr/')).toBe(true);
    expect(opened).toEqual(['https://www.leagueoflegends.com/fr-fr/']);
  });

  it.each([`${location.origin}/fr/champions`, '/fr/builds', 'mailto:contact@example.test'])(
    'leaves %s to the WebView',
    (href) => {
      expect(click(href)).toBe(false);
      expect(opened).toEqual([]);
    },
  );

  it('leaves a click with another button alone', () => {
    expect(click('https://x.example/', { button: 1 })).toBe(false);
    expect(opened).toEqual([]);
  });

  it('stops intercepting once the application is destroyed', () => {
    TestBed.resetTestingModule();

    click('https://x.example/');

    expect(opened).toEqual([]);
  });
});
