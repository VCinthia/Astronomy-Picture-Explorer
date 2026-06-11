import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';

import { type Rgb, extractPalette, rgbToHex } from '../../utils/palette/extract-palette';

/** Brand-token fallback palette used when pixels cannot be read (ADR-0002). */
const FALLBACK_PALETTE: Rgb[] = [
  { r: 0x19, g: 0x19, b: 0x27 }, // space.surface-hi
  { r: 0x1e, g: 0x1e, b: 0x30 }, // space.border
  { r: 0x4d, g: 0x78, b: 0xff }, // accent
  { r: 0x88, g: 0x88, b: 0xaa }, // content.secondary
  { r: 0x55, g: 0x55, b: 0x77 } // content.tertiary
];

/** Pixels are sampled from a small square canvas; tiny is plenty for a palette. */
const SAMPLE_SIZE = 50;
const SWATCH_COUNT = 5;

interface Swatch {
  hex: string;
}

@Component({
  selector: 'app-color-palette',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="palette-label" class="flex items-center gap-4">
      <p
        id="palette-label"
        class="text-micro font-medium uppercase tracking-widest text-content-secondary"
      >
        Dominant Colors
      </p>
      <ul role="list" class="flex gap-2.5">
        @for (swatch of swatches(); track swatch.hex; let i = $index) {
          <li class="flex flex-col items-center gap-1.5">
            <span
              class="block size-9 rounded-swatch lg:size-12"
              [style.background-color]="swatch.hex"
              role="img"
              [attr.aria-label]="'Dominant color ' + (i + 1) + ': ' + swatch.hex"
            ></span>
            <span class="text-micro text-content-tertiary">{{ swatch.hex }}</span>
          </li>
        }
      </ul>
    </section>
  `
})
export class ColorPaletteComponent {
  /** Source image URL to derive the palette from. */
  readonly imageUrl = input<string | undefined>(undefined);

  private readonly colors = signal<Rgb[]>([]);

  /** True when the on-image extraction failed and the fallback is shown. */
  readonly isFallback = signal(false);

  readonly swatches = computed<Swatch[]>(() =>
    this.colors().map((color) => ({ hex: rgbToHex(color) }))
  );

  constructor() {
    effect(() => {
      const url = this.imageUrl();
      if (!url) {
        this.colors.set([]);
        return;
      }
      void this.computePalette(url);
    });
  }

  private async computePalette(url: string): Promise<void> {
    try {
      this.colors.set(await this.sampleImage(url));
      this.isFallback.set(false);
    } catch {
      // CORS-tainted canvas (SecurityError) or a failed load: degrade, never break.
      this.colors.set(FALLBACK_PALETTE);
      this.isFallback.set(true);
    }
  }

  private sampleImage(url: string): Promise<Rgb[]> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.crossOrigin = 'anonymous';

      image.onload = () => {
        try {
          const canvas = document.createElement('canvas');
          canvas.width = SAMPLE_SIZE;
          canvas.height = SAMPLE_SIZE;
          const context = canvas.getContext('2d', { willReadFrequently: true });
          if (!context) {
            reject(new Error('Canvas 2D context unavailable'));
            return;
          }
          context.drawImage(image, 0, 0, SAMPLE_SIZE, SAMPLE_SIZE);
          // Throws SecurityError when the canvas is tainted by a cross-origin image.
          const pixels = context.getImageData(0, 0, SAMPLE_SIZE, SAMPLE_SIZE);
          const palette = extractPalette(pixels, SWATCH_COUNT);
          if (palette.length === 0) {
            reject(new Error('Empty palette'));
            return;
          }
          resolve(palette);
        } catch (error) {
          reject(error instanceof Error ? error : new Error('Palette extraction failed'));
        }
      };
      image.onerror = () => reject(new Error('Image failed to load'));
      image.src = url;
    });
  }
}
