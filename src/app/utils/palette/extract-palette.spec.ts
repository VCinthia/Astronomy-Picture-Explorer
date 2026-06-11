import { extractPalette, rgbToHex, type Rgb } from './extract-palette';

/** Build an ImageData fixture from a flat list of opaque RGB pixels. */
function imageDataFrom(pixels: Rgb[]): ImageData {
  const data = new Uint8ClampedArray(pixels.length * 4);
  pixels.forEach(({ r, g, b }, i) => {
    data[i * 4] = r;
    data[i * 4 + 1] = g;
    data[i * 4 + 2] = b;
    data[i * 4 + 3] = 255;
  });
  return new ImageData(data, pixels.length, 1);
}

const RED: Rgb = { r: 255, g: 0, b: 0 };
const GREEN: Rgb = { r: 0, g: 255, b: 0 };
const BLUE: Rgb = { r: 0, g: 0, b: 255 };

describe('extractPalette', () => {
  it('returns the most dominant color first', () => {
    const image = imageDataFrom([RED, RED, RED, GREEN, BLUE]);

    const palette = extractPalette(image, 3);

    expect(palette[0]).toEqual(RED);
    expect(palette.length).toBe(3);
  });

  it('ranks colors by population', () => {
    const image = imageDataFrom([RED, GREEN, GREEN, GREEN, BLUE, BLUE]);

    const palette = extractPalette(image, 3).map(rgbToHex);

    expect(palette).toEqual(['#00FF00', '#0000FF', '#FF0000']);
  });

  it('limits the result to the requested count', () => {
    const image = imageDataFrom([RED, GREEN, BLUE]);

    expect(extractPalette(image, 2).length).toBe(2);
  });

  it('groups same-bucket colors into one entry and averages them', () => {
    // Both colors quantize to the same 4-bit-per-channel bucket (>> 4 each).
    const image = imageDataFrom([
      { r: 240, g: 16, b: 16 },
      { r: 242, g: 18, b: 18 }
    ]);

    const palette = extractPalette(image, 5);

    expect(palette.length).toBe(1);
    expect(palette[0]).toEqual({ r: 241, g: 17, b: 17 });
  });

  it('skips a near-black background so the actual colors win', () => {
    const sky = { r: 6, g: 6, b: 10 };
    const nebula = { r: 210, g: 60, b: 90 };
    const image = imageDataFrom([sky, sky, sky, sky, sky, nebula, nebula]);

    const palette = extractPalette(image, 3);

    expect(palette[0]).toEqual(nebula);
    expect(palette).not.toContain(sky);
  });

  it('falls back to all pixels for a near-monochrome dark image', () => {
    const sky = { r: 6, g: 6, b: 10 };
    const image = imageDataFrom([sky, sky, sky]);

    expect(extractPalette(image, 3)).toEqual([sky]);
  });

  it('ignores transparent pixels', () => {
    const data = new Uint8ClampedArray([
      0, 255, 0, 0, // transparent green -> ignored
      255, 0, 0, 255 // opaque red
    ]);
    const image = new ImageData(data, 2, 1);

    expect(extractPalette(image, 5)).toEqual([RED]);
  });

  it('returns an empty array for fully transparent input', () => {
    const image = new ImageData(new Uint8ClampedArray(8), 2, 1);

    expect(extractPalette(image, 5)).toEqual([]);
  });
});

describe('rgbToHex', () => {
  it('formats channels as uppercase zero-padded hex', () => {
    expect(rgbToHex({ r: 12, g: 14, b: 45 })).toBe('#0C0E2D');
  });
});
