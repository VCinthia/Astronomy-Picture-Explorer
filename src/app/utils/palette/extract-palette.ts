/** An RGB color with 0-255 channels. */
export interface Rgb {
  r: number;
  g: number;
  b: number;
}

/**
 * Extract the `count` most dominant colors from raw image pixels.
 *
 * Uses a simple fixed-grid quantization (4 bits per channel) rather than a
 * costly clustering pass (ADR-0002): pixels are binned into up to 4096 buckets,
 * buckets are ranked by population, and each winning bucket reports the average
 * of the pixels that fell into it (more faithful than the bucket center).
 *
 * Fully/near transparent pixels are ignored. Returns fewer than `count` colors
 * when the image has fewer distinct buckets, and an empty array for empty input;
 * callers decide on a fallback palette in that case.
 */
export function extractPalette(imageData: ImageData, count = 5): Rgb[] {
  const { data } = imageData;
  const buckets = new Map<number, { r: number; g: number; b: number; n: number }>();

  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] < 125) {
      continue;
    }
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
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

  return [...buckets.values()]
    .sort((a, b) => b.n - a.n)
    .slice(0, count)
    .map((bucket) => ({
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
