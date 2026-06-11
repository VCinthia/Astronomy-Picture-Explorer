/** An RGB color with 0-255 channels. */
export interface Rgb {
  r: number;
  g: number;
  b: number;
}

interface Bucket {
  r: number;
  g: number;
  b: number;
  n: number;
}

/**
 * Bin opaque pixels into a 4-bit-per-channel grid (up to 4096 buckets). When
 * `skipExtremes` is set, near-black and near-white pixels are excluded so the
 * dominant *colors* win instead of a mostly-black sky. The test is per-channel
 * (not luma) so vivid-but-dark hues such as deep blue are still kept.
 */
function collectBuckets(data: Uint8ClampedArray, skipExtremes: boolean): Bucket[] {
  const buckets = new Map<number, Bucket>();

  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] < 125) {
      continue;
    }
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];

    if (skipExtremes) {
      const max = Math.max(r, g, b);
      const min = Math.min(r, g, b);
      if (max < 40 || min > 235) {
        continue;
      }
    }

    const key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
    const bucket = buckets.get(key);
    if (bucket) {
      bucket.r += r;
      bucket.g += g;
      bucket.b += b;
      bucket.n += 1;
    } else {
      buckets.set(key, { r, g, b, n: 1 });
    }
  }

  return [...buckets.values()].sort((a, b) => b.n - a.n);
}

/**
 * Extract the `count` most dominant colors from raw image pixels.
 *
 * Uses a simple fixed-grid quantization (4 bits per channel) rather than a
 * costly clustering pass (ADR-0002): pixels are binned, buckets are ranked by
 * population, and each winning bucket reports the average of the pixels that
 * fell into it (more faithful than the bucket center).
 *
 * Near-black and near-white pixels are skipped first so deep-space backgrounds
 * don't dominate the palette; if that leaves nothing (a near-monochrome image)
 * it falls back to ranking every pixel. Fully/near transparent pixels are
 * ignored, and empty input yields an empty array (callers supply a fallback).
 */
export function extractPalette(imageData: ImageData, count = 5): Rgb[] {
  const { data } = imageData;
  const vivid = collectBuckets(data, true);
  const ranked = vivid.length > 0 ? vivid : collectBuckets(data, false);

  return ranked.slice(0, count).map((bucket) => ({
    r: Math.round(bucket.r / bucket.n),
    g: Math.round(bucket.g / bucket.n),
    b: Math.round(bucket.b / bucket.n)
  }));
}

/** Format an RGB color as an uppercase `#RRGGBB` hex string. */
export function rgbToHex({ r, g, b }: Rgb): string {
  const toHex = (channel: number) => channel.toString(16).padStart(2, '0');
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`.toUpperCase();
}
