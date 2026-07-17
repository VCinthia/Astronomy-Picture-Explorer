# Figma Design Tokens — Astronomy Picture Explorer

Verified reconciled 1:1 with `src/styles.css` `@theme` block (P1-W2 evidence).

## Colors

| Token | Hex | Usage |
|---|---|---|
| `space-base` | `#08080f` | Page / frame background |
| `space-surface` | `#11111c` | Card backgrounds, inputs |
| `space-surface-hi` | `#191927` | Date chips, stepper buttons, hover |
| `space-border` | `#1e1e30` | Dividers, borders |
| `accent` | `#4d78ff` | Active nav, links, CTA, focused borders, favorited heart |
| `content-primary` | `#f0f0f5` | Headings, body text |
| `content-secondary` | `#8888aa` | Subtitles, labels, secondary text |
| `content-tertiary` | `#7c7ca4` | Date chips, captions (raised from Figma #555577 for WCAG AA) |

## Typography (Inter)

| Scale | Size | Weight |
|---|---|---|
| Display | 32px | Bold |
| Title | 26px | Semi Bold |
| Body | 15px | Regular |
| Caption | 13px | Regular |
| Label | 11px | Regular |
| Nav link | 15px | Regular |
| Logo | 17px | Semi Bold |

## Spacing & Radii

| Token | Value |
|---|---|
| Page margin (desktop) | 120px |
| Page margin (mobile) | 20px |
| Card radius | 8px |
| Chip radius | 4px |
| Swatch radius | 3px |
| Button radius | 6–8px |
| Circle radius | 50% |

## Layout

- Desktop content width: 1200px (120px margins each side of 1440)
- Grid: 3 cols, card 380px wide, gap 30px, step 410px
- Header height: 63px
- Hero image: 1200×500px (desktop), 350×240px (mobile)

## Component patterns

### Nav links (P2 addition)
Home | Explore | Favorites — at x=918/972/1035 in P2 frames; active link in `accent`, inactive in `content-secondary`

### Date stepper (all frames)
← button (36×36, `space-surface-hi`, r=8) · date text · → button

### Grid card (PictureGridComponent, 380×260)
- Card bg: `space-surface`, r=8
- Image area: top 150px, `space-surface-hi`, r=8
- Heart toggle: 36×36 solid dark circle (r=18, bg `space-base`), **top-right corner of image**;
  SVG heart 20×20, filled accent if favorited, outline secondary if not. No emoji glyphs.
- Date chip: ~80×22, `space-surface-hi`, r=4; text 11px `content-tertiary`; **overlaid on bottom-left of image area** (not below)
- Title: 14px–15px Semi Bold `content-primary`, below image area, max 2 lines
- Swatches: 3 small colored squares (~16×12px, r=3), bottom of card below title

### Search bar (Explorer + Search frame)
- Toolbar desktop: 3-column grid; search ocupa 2 columnas (~800px) y date picker
  1 columna (~400px), gap incluido dentro de los 1200px del contenido.
- Search y date control: 44px de alto, bg `space-surface`, r=8.
- Mobile: ambos controles se apilan full-width (350px) con gap vertical de 8px.
- Default: no border
- Focused: 1px border `accent` inside
- Left: SVG search icon (14px `content-secondary`) at x+18
- Query text: 15px `content-primary` at x+48
- Clear button: 28×28 `space-surface-hi` circle, × icon
