import { srcsetOf } from './srcset-of';

describe('srcsetOf', () => {
  it('lists the stored variants with their widths, narrowest first as the API gives them', () => {
    const variants = [
      { url: '/cdn/blobs/a.webp', width: 480 },
      { url: '/cdn/blobs/b.webp', width: 960 },
      { url: '/cdn/blobs/c.webp', width: 1280 },
    ];

    expect(srcsetOf(variants)).toBe(
      '/cdn/blobs/a.webp 480w, /cdn/blobs/b.webp 960w, /cdn/blobs/c.webp 1280w',
    );
  });

  it('gives no srcset without variants, so the hotlinked art stands alone', () => {
    expect(srcsetOf([])).toBeNull();
  });
});
