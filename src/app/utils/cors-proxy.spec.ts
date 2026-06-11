import { toCorsSafeImageUrl } from './cors-proxy';

describe('toCorsSafeImageUrl', () => {
  it('routes an https NASA image through the CORS proxy', () => {
    const result = toCorsSafeImageUrl('https://apod.nasa.gov/apod/image/2606/Thor.jpg');

    expect(result).toContain('images.weserv.nl');
    expect(result).toContain('ssl:');
    expect(result).toContain(encodeURIComponent('apod.nasa.gov/apod/image/2606/Thor.jpg'));
  });

  it('leaves local/relative urls unchanged', () => {
    expect(toCorsSafeImageUrl('/assets/mock/x.jpg')).toBe('/assets/mock/x.jpg');
    expect(toCorsSafeImageUrl('data:image/png;base64,AAAA')).toBe('data:image/png;base64,AAAA');
  });
});
