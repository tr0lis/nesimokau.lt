import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const origin = 'https://nesimokau.lt';
const groupedClasses = [
  { slug: '1-2-klases', label: '1–2 klasei', audience: '1–2 klasių mokiniams', phrase: 'užduotys 1 klasei ir mokomieji žaidimai 2 klasei' },
  { slug: '3-4-klases', label: '3–4 klasei', audience: '3–4 klasių mokiniams', phrase: 'mokomieji žaidimai 3 klasei ir mokomieji žaidimai 4 klasei' },
  { slug: '5-6-klases', label: '5–6 klasei', audience: '5–6 klasių mokiniams', phrase: 'mokomieji žaidimai 5 klasei ir mokomieji žaidimai 6 klasei' },
  { slug: '7-8-klases', label: '7–8 klasei', audience: '7–8 klasių mokiniams', phrase: 'mokomieji žaidimai 7 klasei ir mokomieji žaidimai 8 klasei' },
  { slug: '9-10-klases', label: '9–10 klasei', audience: '9–10 klasių mokiniams', phrase: 'mokomieji žaidimai 9 klasei ir mokomieji žaidimai 10 klasei' },
  { slug: '11-12-klases', label: '11–12 klasei', audience: '11–12 klasių mokiniams', phrase: 'užduotys vyresnių klasių mokiniams' },
];

const pages = [
  {
    path: '/',
    title: 'nesimokau.lt – Mokykis žaisdamas!',
    description: 'Mokomieji žaidimai 1–12 klasių mokiniams. Edukaciniai žaidimai vaikams pagal mokomuosius dalykus. Tobulėk lengviau!',
    noindex: false,
    sitemap: true,
    h1: 'Mokykis žaisdamas!',
    sections: [
      ['Edukaciniai žaidimai vaikams pagal mokomuosius dalykus'],
      ['Mokymasis žaidžiant', 'Interaktyvūs mokomieji žaidimai leidžia iškart pritaikyti žinias: rašyti žodžius, spręsti uždavinius ir pastebėti, kas jau pavyksta.'],
    ],
    links: [['Lietuvių kalbos žaidimai', '/lietuviu-kalba'], ['Matematikos užduotys', '/matematika']],
  },
  {
    path: '/lietuviu-kalba',
    title: 'Lietuvių kalbos užduotys ir žaidimai vaikams | nesimokau.lt',
    description: 'Lietuvių kalbos užduotys vaikams: žodžių diktantas, praleistos raidės, kirčiavimas ir pamokos 1–2 klasei.',
    noindex: false,
    sitemap: true,
    h1: 'Lietuvių kalbos žaidimai ir užduotys',
    sections: [
      ['Rašyba ir žodynas praktikoje', 'Žaisk žodžių diktantą, įrašyk praleistas raides ir pasitikrink, kaip sekasi rašyti lietuviškus žodžius. Kiekvieną veiklą galima atlikti trumpais, aiškiais etapais.'],
      ['Pamokos pirmokams ir antrokams', 'A1 ir A2 lygio medžiagoje rasi žodyną, gramatikos paaiškinimus ir temines užduotis. Turinys pritaikytas 1 bei 2 klasės mokiniams.'],
    ],
    links: [['Pamokos 1–2 klasei', '/lietuviu-kalba/1-2-klases/pamokos'], ['Žodžių diktantas', '/zaidimai/zodziu-diktantas'], ['Praleistos raidės', '/zaidimai/praleistos-raides'], ['Kirčiavimas', '/zaidimai/kirciavimas']],
  },
  {
    path: '/matematika',
    title: 'Matematikos užduotys ir žaidimai vaikams | nesimokau.lt',
    description: 'Matematikos žaidimai vaikams: skaičiavimas, tekstiniai uždaviniai, geometrija, matavimai ir loginiai dėsningumai.',
    noindex: false,
    sitemap: true,
    h1: 'Matematikos žaidimai ir užduotys',
    sections: [
      ['Skaičiavimas ir loginis mąstymas', 'Atlik sudėties, atimties, daugybos ar dalybos užduotis skaičių arenoje. Užduotys padeda lavinti skaičiavimo tikslumą ir pasitikėjimą.'],
      ['Uždaviniai ir geometrija', 'Spręsk tekstinius uždavinius, pažink figūras ir matavimus, atrask skaičių bei formų sekas. Pasirink temą, kurią nori pasitreniruoti.'],
    ],
    links: [['Skaičių arena', '/matematika/zaidimai/skaiciu-arena'], ['Tekstiniai uždaviniai', '/matematika/zaidimai/tekstiniai-uzdaviniai'], ['Geometrija', '/matematika/zaidimai/geometrija'], ['Dėsningumai', '/matematika/zaidimai/desningumai']],
  },
  {
    path: '/irankiai',
    title: 'Matematikos įrankiai: grafikų braižyklė, lygčių sprendimas | nesimokau.lt',
    description: 'Nemokami matematikos įrankiai: funkcijų grafikų braižyklė, lygčių sprendimas žingsnis po žingsnio, trupmenų, procentų ir mokslinis skaičiuotuvas.',
    noindex: false,
    sitemap: true,
    h1: 'Matematikos įrankiai',
    sections: [
      ['Grafikai ir lygtys', 'Braižyk funkcijų grafikus, spręsk lygtis žingsnis po žingsnio ir tikrink atsakymus.'],
      ['Skaičiuotuvai', 'Mokslinis skaičiuotuvas, trupmenos, procentai, geometrija ir statistika vienoje vietoje.'],
    ],
    links: [['Matematikos užduotys', '/matematika'], ['Lietuvių kalbos žaidimai', '/lietuviu-kalba']],
  },
  {
    path: '/turnyrine-lentele',
    title: 'Turnyrinė lentelė – geriausi žaidėjai | nesimokau.lt',
    description: 'Geriausių nesimokau.lt žaidėjų turnyrinė lentelė: visų laikų, šio mėnesio ir šios savaitės rezultatai.',
    noindex: false,
    sitemap: true,
    h1: 'Turnyrinė lentelė',
    sections: [
      ['Geriausi žaidėjai', 'Stebėk, kas surinko daugiausia taškų per visus laikus, šį mėnesį ar šią savaitę.'],
    ],
    links: [['Matematikos užduotys', '/matematika'], ['Lietuvių kalbos žaidimai', '/lietuviu-kalba']],
  },
  {
    path: '/lietuviu-kalba/1-2-klases/pamokos',
    title: 'Lietuvių kalbos pamokos ir užduotys 1–2 klasei | nesimokau.lt',
    description: 'Mokomoji medžiaga pirmokams ir antrokams: lietuvių kalbos žodynas, gramatika, A1–A2 pamokos ir interaktyvios užduotys.',
    noindex: false,
    sitemap: true,
    h1: 'Lietuvių kalbos pamokos 1–2 klasei',
    sections: [
      ['Pasirink klasę ir temą', 'Pirmokai gali pradėti nuo kasdienių žodžių ir paprastų sakinių, o antrokai – plėsti žodyną ir taikyti gramatikos taisykles. Pamokose derinama mokomoji medžiaga ir praktika.'],
      ['Mokymasis savo tempu', 'Kiekvienos temos žodynas, paaiškinimai ir pratimai padeda įtvirtinti mokykloje išmoktas žinias.'],
    ],
    links: [['Lietuvių kalbos pratimai', '/lietuviu-kalba'], ['/ 1 klasės pamokos', '/lietuviu-kalba/1-2-klases/pamokos/1'], ['2 klasės pamokos', '/lietuviu-kalba/1-2-klases/pamokos/2']],
  },
];

const games = [
  {
    slug: 'zodziu-diktantas', base: '/zaidimai/zodziu-diktantas', name: 'Žodžių diktantas',
    category: 'Lietuvių kalba', keywords: 'lietuvių kalbos žaidimai ir rašybos užduotys',
    rule: 'Išklausyk tariamą žodį ir įrašyk jį taip, kaip girdi.',
    benefit: 'Pasirink sudėtingumą ir lavink klausymą, žodyną bei taisyklingą rašybą.',
    title: 'Žodžių diktanto žaidimas vaikams',
  },
  {
    slug: 'praleistos-raides', base: '/zaidimai/praleistos-raides', name: 'Praleistos raidės',
    category: 'Lietuvių kalba', keywords: 'lietuvių kalbos rašybos užduotys vaikams',
    rule: 'Perskaityk pateiktą žodį ir jo vietoje įrašyk trūkstamą raidę.',
    benefit: 'Pasirink sudėtingumą ir įtvirtink lietuvių kalbos rašybos dėsningumus.',
    title: 'Praleistų raidžių užduotys vaikams',
  },
  {
    slug: 'kirciavimas', base: '/zaidimai/kirciavimas', name: 'Kirčiavimas',
    category: 'Lietuvių kalba', keywords: 'lietuvių kalbos tarimo užduotys',
    rule: 'Išklausyk tariamą žodį ir pažymėk, kurio skiemens balsis kirčiuojamas.',
    benefit: 'Pasirink sudėtingumą ir mokykis atpažinti lietuvių kalbos kirčio vietą.',
    title: 'Kirčiavimo žaidimas vaikams',
  },
  {
    slug: 'skaiciu-arena', base: '/matematika/zaidimai/skaiciu-arena', name: 'Skaičių arena',
    category: 'Matematika', keywords: 'matematikos žaidimai',
    rule: 'Pasirink aritmetinį veiksmą ir išspręsk pateiktus skaičiavimo pavyzdžius.',
    benefit: 'Pasirink sudėtingumą ir treniruok sudėtį, atimtį, daugybą bei dalybą.',
    title: 'Skaičiavimo žaidimas vaikams',
  },
  {
    slug: 'tekstiniai-uzdaviniai', base: '/matematika/zaidimai/tekstiniai-uzdaviniai', name: 'Tekstiniai uždaviniai',
    category: 'Matematika', keywords: 'matematikos užduotys vaikams',
    rule: 'Perskaityk uždavinio sąlygą, atsirink duomenis ir pasirink sprendimo veiksmą.',
    benefit: 'Pasirink sudėtingumą ir lavink matematinių uždavinių sprendimo strategijas.',
    title: 'Matematikos tekstiniai uždaviniai',
  },
  {
    slug: 'geometrija', base: '/matematika/zaidimai/geometrija', name: 'Geometrija ir matavimai',
    category: 'Matematika', keywords: 'geometrijos žaidimai vaikams',
    rule: 'Atlik užduotis apie geometrines figūras, jų savybes ir matavimus.',
    benefit: 'Pasirink sudėtingumą ir lavink erdvinį mąstymą bei matavimo įgūdžius.',
    title: 'Geometrijos užduotys vaikams',
  },
  {
    slug: 'desningumai', base: '/matematika/zaidimai/desningumai', name: 'Dėsningumai',
    category: 'Matematika', keywords: 'loginiai matematikos žaidimai',
    rule: 'Surask skaičių ar figūrų sekos taisyklę ir pagal ją pasirink trūkstamą elementą.',
    benefit: 'Pasirink sudėtingumą ir lavink loginį bei matematinį mąstymą.',
    title: 'Dėsningumų žaidimas vaikams',
  },
];

for (const game of games) {
  const description = `Žaidimas skirtas 1–12 klasių mokiniams. ${game.rule} ${game.benefit}`;
  pages.push({
    path: game.base,
    title: `${game.title} | nesimokau.lt`,
    description,
    noindex: false,
    sitemap: true,
    gamePage: true,
    h1: game.name,
    category: game.category,
    sections: [[game.keywords, description], ['Mokykis žaisdamas', 'Pasirink užduoties sudėtingumą, spręsk savo tempu ir iškart pasitikrink atsakymą.']],
    links: [['Dalykai ir kiti žaidimai', game.category === 'Matematika' ? '/matematika' : '/lietuviu-kalba']],
  });

  if (game.category === 'Lietuvių kalba') {
    for (const grade of groupedClasses) {
      pages.push({
        path: `/lietuviu-kalba/${grade.slug}/${game.slug}`,
        title: `${game.name} – mokomieji žaidimai ${grade.label} | nesimokau.lt`,
        description: `Lietuvių kalbos žaidimas pritaikytas ${grade.audience}. ${game.rule} ${game.benefit}`,
        noindex: false,
        sitemap: true,
        gamePage: true,
        h1: `${game.name}: užduotys ${grade.label}`,
        category: 'Lietuvių kalba',
        sections: [
          [`${grade.phrase} lietuvių kalbai`, `Šis pratimas pritaikytas ${grade.audience}. ${game.rule} ${game.benefit}`],
          ['Kartok ir tobulėk', 'Spręsk užduotis savo tempu, iškart matyk atsakymo rezultatą ir nuosekliai stiprink lietuvių kalbos įgūdžius.'],
        ],
        links: [['Visi lietuvių kalbos pratimai', '/lietuviu-kalba'], ['Pamokos 1–2 klasei', '/lietuviu-kalba/1-2-klases/pamokos']],
      });
    }
  }
}

for (const grade of ['1', '2']) {
  pages.push({
    path: `/lietuviu-kalba/1-2-klases/pamokos/${grade}`,
    title: `Lietuvių kalbos pamokos ir užduotys ${grade} klasei | nesimokau.lt`,
    description: `Lietuvių kalbos pamokos ${grade} klasei: teminis žodynas, gramatikos paaiškinimai ir praktinės užduotys mokymuisi žaidžiant.`,
    noindex: false,
    sitemap: true,
    h1: `Lietuvių kalbos užduotys ${grade} klasei`,
    category: 'Lietuvių kalba',
    sections: [
      [`Mokomoji medžiaga ${grade} klasei`, `Pasirink temą, susipažink su jos žodynu ir taisyklėmis, o tada pritaikyk žinias interaktyvioje užduotyje.`],
      ['Temos ir praktika', 'A1–A2 lygio turinys padeda plėsti žodyną ir pamažu įtvirtinti kalbos vartojimo įgūdžius.'],
    ],
    links: [['Visos 1–2 klasės pamokos', '/lietuviu-kalba/1-2-klases/pamokos'], ['Lietuvių kalbos žaidimai', '/lietuviu-kalba']],
  });
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
}

function pageContent(page) {
  const sections = page.sections.map(([heading, text]) => `<section><h2>${escapeHtml(heading)}</h2>${text ? `<p>${escapeHtml(text)}</p>` : ''}</section>`).join('');
  const links = (page.links ?? []).map(([label, href]) => `<a href="${escapeHtml(href)}">${escapeHtml(label)} <span aria-hidden="true">→</span></a>`).join('');
  return `<main class="seo-prerender"><nav aria-label="Pagrindinis puslapis"><a class="seo-brand" href="/"><img src="/images/nesimokau-logo.png" alt="" />nesimokau<span>.lt</span></a>${page.category ? ` <span class="seo-breadcrumb" aria-hidden="true">/</span> <span>${escapeHtml(page.category)}</span>` : ''}</nav><header><p class="eyebrow">${escapeHtml(page.category ?? 'MOKYKIMĖS ŽAIDŽIANT')}</p><h1>${escapeHtml(page.h1)}</h1><p class="subtitle">${escapeHtml(page.description)}</p></header>${sections}<nav class="seo-related" aria-label="Susiję puslapiai">${links}</nav><p class="seo-cta"><span>Išsaugok savo mokymosi pažangą</span><a href="/registracija">Sukurti paskyrą <span aria-hidden="true">→</span></a><a href="/prisijungimas">Prisijungti</a></p></main>`;
}

function structuredData(page) {
  return JSON.stringify({
    '@context': 'https://schema.org',
    '@type': page.category ? 'LearningResource' : 'WebPage',
    name: page.title,
    description: page.description,
    url: `${origin}${page.path}`,
    inLanguage: 'lt-LT',
    isAccessibleForFree: true,
    learningResourceType: page.category ? 'Interactive educational game' : 'Educational website',
    educationalUse: 'practice',
    publisher: { '@type': 'Organization', name: 'nesimokau.lt', url: `${origin}/` },
    isPartOf: { '@type': 'WebSite', name: 'nesimokau.lt', url: `${origin}/` },
  }).replace(/</g, '\\u003C');
}

function injectMeta(html, name, content, property = false) {
  const attribute = property ? 'property' : 'name';
  const escapedName = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const regex = new RegExp(`<meta\\s+${attribute}="${escapedName}"[^>]*>`, 'i');
  const tag = `<meta ${attribute}="${name}" content="${escapeHtml(content)}" />`;
  return regex.test(html) ? html.replace(regex, tag) : html.replace('</head>', `    ${tag}\n</head>`);
}

function renderDocument(indexHtml, page) {
  const canonical = `${origin}${page.path}`;
  let html = indexHtml.replace(/<title>[\s\S]*?<\/title>/i, `<title>${escapeHtml(page.title)}</title>`);
  html = injectMeta(html, 'description', page.description);
  html = injectMeta(html, 'robots', page.noindex ? 'noindex,follow' : 'index,follow,max-image-preview:none');
  html = injectMeta(html, 'og:title', page.title, true);
  html = injectMeta(html, 'og:description', page.description, true);
  html = injectMeta(html, 'og:url', canonical, true);
  html = injectMeta(html, 'twitter:title', page.title);
  html = injectMeta(html, 'twitter:description', page.description);
  html = html.replace(/<link\s+rel="canonical"[^>]*>/i, `<link rel="canonical" href="${canonical}" />`);
  html = html.replace(/<script type="application\/ld\+json">[\s\S]*?<\/script>/i, `<script type="application/ld+json">${structuredData(page)}</script>`);
  // Keep the branded loader visible; the crawlable content stays in the DOM but is visually hidden until Blazor boots.
  const loader = (html.match(/<div class="app-loading">[\s\S]*?<\/div>\s*<\/div>/) ?? [''])[0];
  html = html.replace(/<div id="app">[\s\S]*?(?=<div id="blazor-error-ui")/, `<div id="app">${loader}${pageContent(page)}</div>\n\n    `);
  return html;
}

const outputRoot = path.resolve(process.argv[2] ?? 'public');
const indexHtml = await fs.readFile(path.join(outputRoot, 'index.html'), 'utf8');
const uniquePaths = new Set();
for (const page of pages) {
  if (uniquePaths.has(page.path)) throw new Error(`Duplicate SEO route: ${page.path}`);
  uniquePaths.add(page.path);
  const target = page.path === '/' ? path.join(outputRoot, 'index.html') : path.join(outputRoot, page.path.slice(1), 'index.html');
  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.writeFile(target, renderDocument(indexHtml, page), 'utf8');
}

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'wwwroot');
const pagesRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'Pages');
const sourcePages = await fs.readdir(pagesRoot, { withFileTypes: true });
const declaredRoutes = new Set();
for (const entry of sourcePages) {
  if (!entry.isFile() || !entry.name.endsWith('.razor')) continue;
  const source = await fs.readFile(path.join(pagesRoot, entry.name), 'utf8');
  for (const match of source.matchAll(/^\s*@page\s+"([^"]+)"/gm)) {
    declaredRoutes.add(match[1]);
  }
}

function routeMatchesTemplate(route, template) {
  const pattern = template.split('/').map((segment) => {
    if (segment.startsWith('{') && segment.endsWith('}')) return '[^/]+';
    return segment.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }).join('/');
  return new RegExp(`^${pattern}/?$`).test(route);
}

const sitemapPages = pages.filter((page) => page.sitemap);
const sitemapPaths = new Set();
for (const page of sitemapPages) {
  if (sitemapPaths.has(page.path)) throw new Error(`Duplicate sitemap route: ${page.path}`);
  if (![...declaredRoutes].some((route) => routeMatchesTemplate(page.path, route))) {
    throw new Error(`Sitemap route is not declared by a Razor page: ${page.path}`);
  }
  if (page.noindex) throw new Error(`Noindex route cannot be listed in sitemap: ${page.path}`);
  sitemapPaths.add(page.path);
}
for (const page of pages) {
  if (typeof page.noindex !== 'boolean' || typeof page.sitemap !== 'boolean') {
    throw new Error(`SEO route must explicitly define noindex and sitemap: ${page.path}`);
  }
  if (page.noindex === page.sitemap) {
    throw new Error(`Public routes must be in the sitemap and noindex routes must be excluded: ${page.path}`);
  }
}
const gamePages = pages.filter((page) => page.gamePage);
for (const field of ['title', 'description', 'h1', 'path']) {
  if (new Set(gamePages.map((page) => page[field])).size !== gamePages.length) {
    throw new Error(`Game pages must have unique ${field} values.`);
  }
}
const today = new Date().toISOString().slice(0, 10);
const urls = sitemapPages.map((page) => `  <url><loc>${origin}${page.path}</loc><lastmod>${today}</lastmod></url>`).join('\n');
const sitemap = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
const aiBots = ['GPTBot', 'OAI-SearchBot', 'ChatGPT-User', 'ClaudeBot', 'Claude-Web', 'anthropic-ai', 'PerplexityBot', 'Perplexity-User', 'Google-Extended', 'Applebot-Extended', 'CCBot', 'Bingbot'];
const aiRules = aiBots.map((bot) => `User-agent: ${bot}\n`).join('') + `Allow: /\nDisallow: /api/\nDisallow: /profilis\nDisallow: /prisijungimas\nDisallow: /registracija\n\n`;
const robots = aiRules + `User-agent: *\nAllow: /\nDisallow: /api/\nDisallow: /profilis\nDisallow: /pasiekimai\nDisallow: /rezultatai\nDisallow: /mediateka\nDisallow: /pasitaisyti\nDisallow: /diagnostika\nDisallow: /skaiciuokle\nDisallow: /prisijungimas\nDisallow: /registracija\n\nSitemap: ${origin}/sitemap.xml\n`;
await fs.writeFile(path.join(outputRoot, 'sitemap.xml'), sitemap);
await fs.writeFile(path.join(outputRoot, 'robots.txt'), robots);
await fs.writeFile(path.join(sourceRoot, 'sitemap.xml'), sitemap);
await fs.writeFile(path.join(sourceRoot, 'robots.txt'), robots);
console.log(`Generated ${pages.length} SEO-rendered routes and a sitemap with ${sitemapPages.length} verified public routes.`);
