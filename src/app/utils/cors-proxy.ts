import { IMAGE_PROXY_ORIGIN } from '../config/site.config';

/**
 * Rewrite an image URL so it can be read pixel-by-pixel from a `<canvas>`.
 *
 * NASA's APOD host does not send `Access-Control-Allow-Origin`, so a
 * `crossOrigin="anonymous"` image taints the canvas and `getImageData` throws.
 * Routing the *sampling* request through the public images.weserv.nl proxy
 * returns the same image with permissive CORS headers, which keeps the Canvas
 * palette extraction working client-side (the visible `<img>` keeps using the
 * original NASA URL). If extraction still fails the component falls back.
 *
 * Already-local URLs (assets, data/blob) are returned unchanged.
 */
export function toCorsSafeImageUrl(url: string): string {
  if (!/^https?:\/\//i.test(url)) {
    return url;
  }
  const withoutScheme = url.replace(/^https?:\/\//i, '');
  // `ssl:` tells the proxy to fetch the upstream over HTTPS; `w=120` downscales
  // the sample (a tiny image is plenty for a dominant-color palette).
  return `${IMAGE_PROXY_ORIGIN}/?url=ssl:${encodeURIComponent(withoutScheme)}&w=120`;
}
