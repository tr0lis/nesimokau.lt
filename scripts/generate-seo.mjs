import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const origin = 'https://nesimokau.lt';
const groupedClasses = [
  { slug: '1-2-klases', label: '1–2 klasei', phrase: 'užduotys 1 klasei ir mokomieji žaidimai 2 klasei' },
  { slug: '3-4-klases', label: '3–4 klasei', phrase: 'mokomieji žaidimai 3 klasei ir mokomieji žaidimai 4 klasei' },
  { slug: '5-6-klases', label: '5–6 klasei', phrase: 'mokomieji žaidimai 5 klasei ir mokomieji žaidimai 6 klasei' },
  { slug: '7-8-klases', label: '7–8 klasei', phrase: 'mokomieji žaidimai 7 klasei ir mokomieji žaidimai 8 klasei' },
  { slug: '9-10-klases', label: '9–10 klasei', phrase: 'mokomieji žaidimai 9 klasei ir mokomieji žaidimai 10 klasei' },
  { slug: '11-12-klases', label: '11–12 klasei', phrase: 'užduotys vyresnių klasių mokiniams' },
];

const pages = [
  {
    path: '/',
    title: 'nesimokau.lt – Mokykis žaisdamas!',
    description: 'Mokomieji žaidimai 1–12 klasių mokiniams. Edukaciniai žaidimai vaikams pagal mokomuosius dalykus. Tobulėk lengviau!',
    sitemap: true,
    h1: 'Mokykis žaisdamas!',
    sections: [
      ['Edukaciniai žaidimai vaikams pagal mokomuosius dalykus', 'Rinkis lietuvių kalbos užduotis arba matematikos žaidimus. Aiškios taisyklės, trumpi iššūkiai ir draugiškas grįžtamasis ryšys padeda mokytis savo tempu.'],
      ['Mokymasis žaidžiant', 'Interaktyvūs mokomieji žaidimai leidžia iškart pritaikyti žinias: rašyti žodžius, spręsti uždavinius ir pastebėti, kas jau pavyksta. Pagrindiniai pratimai pasiekiami nemokamai.'],
    ],
    links: [['Lietuvių kalbos žaidimai', '/lietuviu-kalba'], ['Matematikos užduotys', '/matematika']],
  },
  {
    path: '/lietuviu-kalba',
    title: 'Lietuvių kalbos užduotys ir žaidimai vaikams | nesimokau.lt',
    description: 'Lietuvių kalbos užduotys vaikams: žodžių diktantas, praleistos raidės, kirčiavimas ir pamokos 1–2 klasei.',
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
    sitemap: true,
    h1: 'Matematikos žaidimai ir užduotys',
    sections: [
      ['Skaičiavimas ir loginis mąstymas', 'Atlik sudėties, atimties, daugybos ar dalybos užduotis skaičių arenoje. Užduotys padeda lavinti skaičiavimo tikslumą ir pasitikėjimą.'],
      ['Uždaviniai ir geometrija', 'Spręsk tekstinius uždavinius, pažink figūras ir matavimus, atrask skaičių bei formų sekas. Pasirink temą, kurią nori pasitreniruoti.'],
    ],
    links: [['Skaičių arena', '/matematika/zaidimai/skaiciu-arena'], ['Tekstiniai uždaviniai', '/matematika/zaidimai/tekstiniai-uzdaviniai'], ['Geometrija', '/matematika/zaidimai/geometrija'], ['Dėsningumai', '/matematika/zaidimai/desningumai']],
  },
  {
    path: '/lietuviu-kalba/1-2-klases/pamokos',
    title: 'Lietuvių kalbos pamokos ir užduotys 1–2 klasei | nesimokau.lt',
    description: 'Mokomoji medžiaga pirmokams ir antrokams: lietuvių kalbos žodynas, gramatika, A1–A2 pamokos ir interaktyvios užduotys.',
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
    description: 'Klausykis tariamo žodžio ir jį užrašyk. Diktanto žaidimas padeda lavinti klausymą, žodyną ir taisyklingą lietuviškų žodžių rašybą.',
    title: 'Žodžių diktanto žaidimas vaikams',
  },
  {
    slug: 'praleistos-raides', base: '/zaidimai/praleistos-raides', name: 'Praleistos raidės',
    category: 'Lietuvių kalba', keywords: 'lietuvių kalbos rašybos užduotys vaikams',
    description: 'Perskaityk žodį, pagalvok apie jo rašybą ir įrašyk trūkstamą raidę. Užduotys padeda pastebėti lietuvių kalbos rašybos dėsningumus.',
    title: 'Praleistų raidžių užduotys vaikams',
  },
  {
    slug: 'kirciavimas', base: '/zaidimai/kirciavimas', name: 'Kirčiavimas',
    category: 'Lietuvių kalba', keywords: 'lietuvių kalbos tarimo užduotys',
    description: 'Klausykis žodžių ir pasirink, kurio skiemens kirčiuoti balsį. Interaktyvus pratimas supažindina su lietuvių kalbos kirčiavimu.',
    title: 'Kirčiavimo žaidimas vaikams',
  },
  {
    slug: 'skaiciu-arena', base: '/matematika/zaidimai/skaiciu-arena', name: 'Skaičių arena',
    category: 'Matematika', keywords: 'matematikos žaidimai',
    description: 'Treniruok sudėtį, atimtį, daugybą ir dalybą skaičių arenoje. Pasirink veiksmą ir spręsk matematines užduotis savo tempu.',
    title: 'Skaičiavimo žaidimas vaikams',
  },
  {
    slug: 'tekstiniai-uzdaviniai', base: '/matematika/zaidimai/tekstiniai-uzdaviniai', name: 'Tekstiniai uždaviniai',
    category: 'Matematika', keywords: 'matematikos užduotys vaikams',
    description: 'Perskaityk sąlygą, atsirink svarbią informaciją ir pasirink tinkamą matematinį veiksmą. Lavink uždavinių sprendimo strategijas.',
    title: 'Matematikos tekstiniai uždaviniai',
  },
  {
    slug: 'geometrija', base: '/matematika/zaidimai/geometrija', name: 'Geometrija ir matavimai',
    category: 'Matematika', keywords: 'geometrijos žaidimai vaikams',
    description: 'Pažink geometrines figūras, matavimus ir erdvinius ryšius atlikdamas interaktyvias matematikos užduotis.',
    title: 'Geometrijos užduotys vaikams',
  },
  {
    slug: 'desningumai', base: '/matematika/zaidimai/desningumai', name: 'Dėsningumai',
    category: 'Matematika', keywords: 'loginiai matematikos žaidimai',
    description: 'Atrask pasikartojančias skaičių ir figūrų sekas, paaiškink jų taisyklę ir lavink loginį mąstymą.',
    title: 'Dėsningumų žaidimas vaikams',
  },
];

for (const game of games) {
  pages.push({
    path: game.base,
    title: `${game.title} | nesimokau.lt`,
    description: game.description,
    sitemap: true,
    h1: game.name,
    category: game.category,
    sections: [[game.keywords, game.description], ['Mokykis žaisdamas', 'Užduotis suskirstyta į trumpus žingsnius, todėl gali iškart pasitikrinti atsakymą, pastebėti klaidą ir bandyti dar kartą.']],
    links: [['Dalykai ir kiti žaidimai', game.category === 'Matematika' ? '/matematika' : '/lietuviu-kalba']],
  });

  if (game.category === 'Lietuvių kalba') {
    for (const grade of groupedClasses) {
      pages.push({
        path: `/lietuviu-kalba/${grade.slug}/${game.slug}`,
        title: `${game.name} – mokomieji žaidimai ${grade.label} | nesimokau.lt`,
        description: `${grade.phrase}: ${game.description}`,
        h1: `${game.name}: užduotys ${grade.label}`,
        category: 'Lietuvių kalba',
        sections: [
          [`${grade.phrase} lietuvių kalbai`, `Pasirink savo mokymosi etapui pritaikytą ${game.name.toLowerCase()} pratimą. ${game.description}`],
          ['Kartok ir tobulėk', 'Atlik užduotį savo tempu, iškart matyk atsakymo rezultatą ir nuosekliai stiprink rašybos bei kalbos įgūdžius.'],
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
  const sections = page.sections.map(([heading, text]) => `<section><h2>${escapeHtml(heading)}</h2><p>${escapeHtml(text)}</p></section>`).join('');
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
  html = injectMeta(html, 'robots', 'index,follow,max-image-preview:none');
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
    if (!match[1].includes('{')) declaredRoutes.add(match[1]);
  }
}

const sitemapPages = pages.filter((page) => page.sitemap);
const sitemapPaths = new Set();
for (const page of sitemapPages) {
  if (sitemapPaths.has(page.path)) throw new Error(`Duplicate sitemap route: ${page.path}`);
  if (!declaredRoutes.has(page.path)) throw new Error(`Sitemap route is not declared by a Razor page: ${page.path}`);
  sitemapPaths.add(page.path);
}
const urls = sitemapPages.map((page) => `  <url><loc>${origin}${page.path}</loc></url>`).join('\n');
const sitemap = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`;
const robots = `User-agent: *\nAllow: /\nDisallow: /api/\nDisallow: /profilis\nDisallow: /pasiekimai\nDisallow: /rezultatai\nDisallow: /turnyrine-lentele\nDisallow: /mediateka\nDisallow: /pasitaisyti\nDisallow: /diagnostika\nDisallow: /skaiciuokle\nDisallow: /prisijungimas\nDisallow: /registracija\nSitemap: ${origin}/sitemap.xml\n`;
await fs.writeFile(path.join(outputRoot, 'sitemap.xml'), sitemap);
await fs.writeFile(path.join(outputRoot, 'robots.txt'), robots);
await fs.writeFile(path.join(sourceRoot, 'sitemap.xml'), sitemap);
await fs.writeFile(path.join(sourceRoot, 'robots.txt'), robots);
console.log(`Generated ${pages.length} SEO-rendered routes and a sitemap with ${sitemapPages.length} verified public routes.`);
