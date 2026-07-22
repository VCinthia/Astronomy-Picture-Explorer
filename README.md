# Astronomy Picture Explorer

Explore NASA's **Astronomy Picture of the Day (APOD)** in a fast, accessible web
app: browse the picture of the day, step through the archive by date, watch video
entries, and see a color palette extracted live from each image — right in the
browser.

🔭 **Live demo:** https://astronomy-picture-explorer.netlify.app/

🎨 **Design:** [Figma file](https://www.figma.com/design/miqqmNJAcF0Mbe1WizAJIu/01_Astronomy_Picture_Explorer?node-id=0-1&t=N9ZRRaB7gVw4pvpS-1)

![Astronomy Picture Explorer — home view](screenshots/home.png)

---

## What it does

- **Picture of the day** — a hero view with the image, title, date and the full
  description, plus image credit.
- **Explore by date** — a native calendar input queries the selected APOD date through
  the app API; Home keeps its UTC previous/next controls over the displayed picture.
- **Image *and* video entries** — when an entry is a video, the card shows a
  thumbnail and a link to watch it (no embedded players).
- **Live color palette** — the dominant colors of each image are computed in the
  browser from the picture's pixels and shown as swatches with their hex codes.
- **Accessible & responsive** — descriptive alt text, keyboard navigation, focus
  styles, WCAG AA color contrast, and a layout tuned from mobile to desktop.
- **Keyword search** — debounced title/description search uses the prepared PostgreSQL
  catalog, with explicit empty, cold-start and catalog-not-ready states.
- **Accounts and favorites** — sign-in, refresh, protected routes and per-user favorites
  use same-origin `/auth/*` and `/api/*` calls; browser storage is not the source of
  truth for favorites.

## Why it exists

A portfolio piece built around a real, recognizable data source. The goal is a
small but production-quality front end: a clean component architecture, a design
implemented faithfully from [Figma](https://www.figma.com/design/miqqmNJAcF0Mbe1WizAJIu/01_Astronomy_Picture_Explorer?node-id=0-1&t=N9ZRRaB7gVw4pvpS-1), in-browser image processing, and accessibility
treated as a first-class concern rather than an afterthought.

## Tech stack

- **Angular 22.0.7** — standalone components and **Signals** for state (no NgModules).
- **Tailwind CSS v4** — design implemented from [Figma](https://www.figma.com/design/miqqmNJAcF0Mbe1WizAJIu/01_Astronomy_Picture_Explorer?node-id=0-1&t=N9ZRRaB7gVw4pvpS-1) using named design tokens.
- **Canvas API** — dominant-color extraction performed entirely client-side, with
  no third-party color libraries.
- **TypeScript**, unit tests with **Karma + Jasmine**.
- **ASP.NET Core + PostgreSQL** — app-owned APOD contract, catalog search and account API.
- **Netlify** — P2 production hosting; P3 is accumulated on its integration branch until
  the zero-cost deployment/smoke gate is complete.

## How it works

The P3 integration app reads the app-owned APOD HTTP contract: `/home` requests today's
entry, Explorer requests exact dates and search uses the PostgreSQL-backed catalog. The
browser bundles no APOD JSON or date list. A small service exposes a requested date and
the response-confirmed entry as signals, so stale HTTP results are cancelled rather than
replacing the current view. For the palette, each image is drawn to an off-screen canvas
and its pixels are sampled and grouped into the most dominant colors; if the pixels
can't be read it falls back to a fixed brand palette.

## Roadmap

- **Stage 1:** picture of the day, explore by date, palette, video. ✅
- **Stage 2 — current production:** favorites (saved locally), keyword search,
  responsive toolbar and mobile bottom navigation. ✅
- **Stage 3 — in integration:** .NET 10 + Identity + PostgreSQL FTS + APOD HTTP frontend.
  Per-user favorites, local containers and the strictly zero-cost production promotion
  remain in the following waves.

## Run it locally

### Full local stack (recommended)

The Compose stack is entirely local: PostgreSQL, a one-shot migrator, an explicit demo
fixture seed, API and Angular/Nginx frontend. It includes a local email log sink and a
deterministic APOD mock, so it does not require NASA, Resend, Neon or a production
secret. Follow [the local runbook](docs/deploy/p3-local-runbook.md) to create ignored
Docker-secret files, then run:

```powershell
docker compose config
docker compose up -d --build
```

Open `http://localhost:8080`; API health is additionally available only on
`http://localhost:5179/health`. `docker compose down` keeps the named local database
volume; add `-v` only when intentionally discarding local data.

### Frontend-only development

```bash
npm ci
npm start        # dev server at http://localhost:4200
npm run build    # production build
npm test         # unit tests
```

`npm start` proxies `/api` and `/auth` to the local API at `http://localhost:5179`.
The public Netlify demo remains the P2 release until P3-W14 promotion.

## Credits

- Imagery and descriptions: **NASA Astronomy Picture of the Day** (apod.nasa.gov).
- Built by **[Cinthia Vota](https://cinthiavota.com.ar/)**.
