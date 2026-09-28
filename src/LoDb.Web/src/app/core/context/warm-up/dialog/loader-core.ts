import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * The loader's signature: a charging Hextech core, the legacy one, purely decorative. Its
 * motion lives in styles/layout/loader-core.css and stops under reduced motion.
 */
@Component({
  selector: 'lodb-loader-core',
  template: `
    <span class="hx-core__glow"></span>
    <svg viewBox="0 0 120 120" class="hx-core__svg">
      <polygon class="hx-core__ring" points="112,60 86,105 34,105 8,60 34,15 86,15" fill="none" />
      <polygon
        class="hx-core__sweep"
        points="112,60 86,105 34,105 8,60 34,15 86,15"
        fill="none"
        pathLength="100"
      />
      <polygon class="hx-core__inner" points="98,60 79,93 41,93 22,60 41,27 79,27" />
      <g class="hx-core__orbit">
        <circle cx="60" cy="8" r="2.6" />
        <circle cx="105" cy="86" r="2.6" />
        <circle cx="15" cy="86" r="2.6" />
      </g>
      <rect class="hx-core__gem" x="50" y="50" width="20" height="20" rx="1.5" />
    </svg>
  `,
  host: { class: 'hx-core block', 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoaderCore {}
