import type { FooterLink } from './footer-link';

/** How to reach the author outside the site's own contact form. */
export const CONTACT_LINKS: readonly FooterLink[] = [
  { href: 'mailto:charles.lindecker@outlook.fr', label: 'footer.contact.email', icon: 'mail' },
  {
    href: 'https://github.com/LINDECKER-Charles',
    label: 'footer.contact.github',
    icon: 'github',
  },
  {
    href: 'https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal',
    label: 'footer.contact.repo',
    icon: 'code',
  },
  {
    href: 'https://www.linkedin.com/in/charles-lindecker',
    label: 'footer.contact.linkedin',
    icon: 'linkedin',
  },
];
