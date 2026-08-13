# Archived Figma draft — Mobile Explorer + Bottom Nav

**Status:** ARCHIVED — do not execute. Este archivo conserva el borrador de Figma de
2026-07-08 para dar contexto a P1/P2. No es una tarea pendiente ni una especificación de
la UI publicada. P2-W4 entregó iconos SVG y navegación mobile; P3 sustituyó el mock y el
selector de fechas por los flujos de aplicación vigentes. P4-W4 archivó el borrador sin
reescribir su contenido histórico.

Para contexto histórico puede consultarse [`frames.md`](frames.md). La autoridad actual
de flujos es [`../architecture/p3-flow-overview.md`](../architecture/p3-flow-overview.md)
y ADR-0003.

## Contenido histórico del borrador

1. Agrega **bottom nav** (Home ⌂ | Explore ◎ | Favorites ♥) al frame `Mobile – Home`
2. Agrega **bottom nav** al frame `Mobile – Favorites`
3. Crea nuevo frame **`Mobile – Explorer`** en x=8540:
   - Header igual al resto de mobile (logo + date stepper ← →)
   - **Toolbar apilado** (2 filas full-width, 350px cada una):
     - Fila 1: search input con placeholder "Search by title or description..."
     - Fila 2: date input con 📅 Jun 09, 2026 ▾
   - Hero card (date mode default): imagen + palette + date chip + título + descripción
   - Bottom nav al pie (Explore activo)

## Bottom nav spec

- Height: 56px, fondo `space-surface` (#11111c), borde superior `space-border`
- 3 secciones de 130px: Home (cx=65) | Explore (cx=195) | Favorites (cx=325)
- Cada tab: ícono 16px arriba + label 10px abajo
- Activo: `accent` (#4d78ff) | Inactivo: `content-secondary` (#8888aa)

## Código use_figma

```javascript
await Promise.all([
  figma.loadFontAsync({ family: 'Inter', style: 'Regular' }),
  figma.loadFontAsync({ family: 'Inter', style: 'Semi Bold' }),
  figma.loadFontAsync({ family: 'Inter', style: 'Bold' }),
]);

const h2rgb = h => ({
  r: parseInt(h.slice(1,3),16)/255,
  g: parseInt(h.slice(3,5),16)/255,
  b: parseInt(h.slice(5,7),16)/255,
});
const sf = c => [{ type:'SOLID', color: typeof c==='string' ? h2rgb(c) : c }];

function bx(name,x,y,w,h,color,r) {
  var n=figma.createRectangle();
  n.name=name; n.x=x; n.y=y; n.resize(w,h);
  n.fills=sf(color); n.cornerRadius=r||0; return n;
}
function tx(name,chars,x,y,size,color,bold) {
  var n=figma.createText();
  n.name=name; n.fontName={family:'Inter',style:bold?'Semi Bold':'Regular'};
  n.fontSize=size; n.textAutoResize='WIDTH_AND_HEIGHT';
  n.characters=String(chars); n.x=x; n.y=y;
  n.fills=sf(color); return n;
}

var BG='#08080f',SURF='#11111c',HI='#191927',BRD='#1e1e30';
var ACC='#4d78ff',PRI='#f0f0f5',SEC='#8888aa',TER='#7c7ca4';

function addBottomNav(frame, activePage) {
  var navY = frame.height - 56;
  frame.appendChild(bx('bnav-border', 0, navY, 390, 1, BRD));
  frame.appendChild(bx('bnav-bg', 0, navY+1, 390, 55, SURF));
  var tabs = [
    { id:'home',      icon:'⌂', label:'Home',      cx:65  },
    { id:'explore',   icon:'◎', label:'Explore',   cx:195 },
    { id:'favorites', icon:'♥', label:'Favorites', cx:325 },
  ];
  tabs.forEach(function(tab) {
    var color = tab.id === activePage ? ACC : SEC;
    frame.appendChild(tx('bnav-icon-'+tab.id, tab.icon, tab.cx-8,  navY+8,  16, color));
    frame.appendChild(tx('bnav-lbl-'+tab.id,  tab.label, tab.cx-20, navY+30, 10, color));
  });
}

// 1. Bottom nav → Mobile – Home
var mHome = figma.currentPage.findOne(function(n) {
  return n.type==='FRAME' && n.name==='Mobile – Home';
});
if (mHome) { addBottomNav(mHome, 'home'); console.log('OK: Mobile – Home'); }

// 2. Bottom nav → Mobile – Favorites
var mFav = figma.currentPage.findOne(function(n) {
  return n.type==='FRAME' && n.name==='Mobile – Favorites';
});
if (mFav) { addBottomNav(mFav, 'favorites'); console.log('OK: Mobile – Favorites'); }

// 3. Create Mobile – Explorer
var mExp = figma.createFrame();
mExp.name = 'Mobile – Explorer';
mExp.x = 8540; mExp.y = 0;
mExp.resize(390, 820);
mExp.fills = sf(BG);
mExp.clipsContent = false;
figma.currentPage.appendChild(mExp);

// Header
mExp.appendChild(bx('hdr-border', 0, 55, 390, 1, BRD));
mExp.appendChild(tx('logo', '✦ Astronomy Explorer', 20, 17.5, 17, PRI, true));
mExp.appendChild(bx('btn-prev', 203, 10, 36, 36, HI, 8));
mExp.appendChild(tx('arrow-l', '←', 214, 19.5, 15, PRI));
mExp.appendChild(tx('date-hdr', 'Jun 09, 2026', 245, 20, 15, PRI));
mExp.appendChild(bx('btn-next', 334, 10, 36, 36, HI, 8));
mExp.appendChild(tx('arrow-r', '→', 345, 19.5, 15, PRI));

// Toolbar row 1: search
var sbar = bx('srch-bar', 20, 68, 350, 42, SURF, 8);
sbar.strokes=[{type:'SOLID',color:h2rgb(BRD)}]; sbar.strokeWeight=1; sbar.strokeAlign='INSIDE';
mExp.appendChild(sbar);
mExp.appendChild(tx('srch-icon', '🔍', 36, 81, 14, SEC));
mExp.appendChild(tx('srch-ph', 'Search by title or description...', 62, 82, 13, TER));

// Toolbar row 2: date
var dbar = bx('date-bar', 20, 118, 350, 42, SURF, 8);
dbar.strokes=[{type:'SOLID',color:h2rgb(BRD)}]; dbar.strokeWeight=1; dbar.strokeAlign='INSIDE';
mExp.appendChild(dbar);
mExp.appendChild(tx('cal-icon',  '📅',          36,  131, 14, SEC));
mExp.appendChild(tx('date-val',  'Jun 09, 2026', 62,  132, 15, PRI));
mExp.appendChild(tx('date-chev', '▾',            352, 133, 13, SEC));

// Hero image
mExp.appendChild(bx('hero-img', 20, 174, 350, 190, HI, 8));
mExp.appendChild(tx('hero-lbl', "APOD · Thor's Helmet", 109, 256, 13, TER));

// Dominant colors
mExp.appendChild(tx('pal-lbl', 'DOMINANT COLORS', 20, 380, 11, SEC));
['#2A2836','#352A38','#373646','#291C28','#29222D'].forEach(function(c,i) {
  mExp.appendChild(bx('sw'+i,  138+(i*46), 368, 36, 36, c, 6));
  mExp.appendChild(tx('swl'+i, c, 136+(i*46), 410, 10, TER));
});

// Date chip + title + description
mExp.appendChild(bx('chip', 20, 440, 89, 21, HI, 4));
mExp.appendChild(tx('chip-t', 'Jun 09, 2026', 30, 444, 13, TER));
mExp.appendChild(tx('title', "Thor's Helmet", 20, 474, 22, PRI, true));
mExp.appendChild(tx('desc',
  "Thor's Helmet is a hat-shaped cosmic cloud about 30 light-years across, in fact an interstellar bubble blown by the fast wind of a hot, massive Wolf-Rayet star near its center...",
  20, 514, 14, SEC));
mExp.appendChild(tx('credit', 'Image Credit & Copyright: Josep Drudis, Christian Sasse', 20, 638, 11, TER));

// Footer line
mExp.appendChild(bx('ftr-line', 0, 696, 390, 1, BRD));
mExp.appendChild(tx('ftr-txt', 'Created June 2026 · Built by Cinthia Vota · Imagery from NASA APOD', 20, 712, 11, TER));

// Bottom nav (Explore active)
addBottomNav(mExp, 'explore');

console.log('Done: Mobile – Explorer at x=8540');
```
