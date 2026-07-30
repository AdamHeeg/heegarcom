/* Unconquered by Debt — page behavior (vanilla JS). */
(function () {

  /* ============================================================
     THEME ENGINE
     Palettes are raw hex sets below. For every colored background
     role we compute the WCAG contrast against black and white and
     pick the winner for the matching --on-* token, so text never
     lands low-contrast when a palette changes.
     To add/replace a palette: edit PALETTES. Keep sectionAlt a light
     tint (the section subhead can't auto-flip per section).
     ============================================================ */
  var PALETTES = [
    { name: 'CherryBlossom',
      surface:'#f7e1d7', sectionAlt:'#dedbd2', card:'#b0c4b1',
      heading:'#4a5759', pop:'#edafb8',
      text:'#4a5759', muted:'#4a5759', onDark:'#edafb8' },

    { name: 'Periwinkle',
      surface:'#eaeaea', sectionAlt:'#cbc5ea', card:'#73628a',
      heading:'#313d5a', headingDeep:'#183642', pop:'#73628a',
      text:'#183642', muted:'#313d5a', onDark:'#cbc5ea' },

    { name: 'KhakiSage',
      surface:'#d8ffdd', sectionAlt:'#dedbd8', card:'#c3c49e',
      heading:'#524632', pop:'#8f7e4f',
      text:'#524632', muted:'#524632', onDark:'#c3c49e' },

    { name: 'LavaCoffee',
      surface:'#f7f3e3', sectionAlt:'#b3b6b7', card:'#af9164',
      heading:'#2b2118', pop:'#6f1a07',
      text:'#2b2118', muted:'#2b2118', onDark:'#af9164' },

    { name: 'SoftBlues',
      surface:'#f9ebe0', sectionAlt:'#89aae6', card:'#334e58',
      heading:'#0d2149', pop:'#1ea896',
      text:'#0d2149', muted:'#334e58', onDark:'#89aae6' },

    { name: 'GoldenBlues',
      surface:'#e6ecef', sectionAlt:'#d5c67a', card:'#005377',
      heading:'#052f5f', pop:'#f1a208',
      text:'#052f5f', muted:'#005377', onDark:'#d5c67a' }
  ];

  var INK = '#16202b';
  var WHITE = '#ffffff';

  function toRgb(hex) {
    var h = hex.replace('#', '');
    if (h.length === 3) { h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2]; }
    return [parseInt(h.substr(0, 2), 16), parseInt(h.substr(2, 2), 16), parseInt(h.substr(4, 2), 16)];
  }

  function luminance(hex) {
    var rgb = toRgb(hex);
    var lin = rgb.map(function (v) {
      v = v / 255;
      return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2];
  }

  function contrast(a, b) {
    var la = luminance(a), lb = luminance(b);
    var hi = Math.max(la, lb), lo = Math.min(la, lb);
    return (hi + 0.05) / (lo + 0.05);
  }

  // Black or white text — whichever reads better on the given background.
  function onColor(bg) {
    return contrast(bg, WHITE) >= contrast(bg, INK) ? WHITE : INK;
  }

  // Darken a hex toward black by a fraction (0..1). Used to derive the
  // "deep"/edge shades so each palette only needs its base colors.
  function darken(hex, amount) {
    var rgb = toRgb(hex).map(function (v) { return Math.max(0, Math.round(v * (1 - amount))); });
    return '#' + rgb.map(function (v) { var s = v.toString(16); return s.length === 1 ? '0' + s : s; }).join('');
  }

  var root = document.documentElement;
  function setVar(name, value) { root.style.setProperty(name, value); }

  function applyPalette(p) {
    // Resolve the derived shades once, so the auto-contrast step below always
    // gets a real color (never undefined) to measure.
    var cardEdge    = p.cardEdge    || darken(p.card, 0.14);
    var border      = p.border      || darken(p.surface, 0.09);
    var headingDeep = p.headingDeep || darken(p.heading, 0.30);
    var popDeep     = p.popDeep     || darken(p.pop, 0.16);

    setVar('--surface', p.surface);
    setVar('--section-alt', p.sectionAlt);
    setVar('--card', p.card);
    setVar('--card-edge', cardEdge);
    setVar('--border', border);
    setVar('--heading', p.heading);
    setVar('--heading-deep', headingDeep);
    setVar('--pop', p.pop);
    setVar('--pop-deep', popDeep);
    setVar('--text', p.text);
    setVar('--muted', p.muted);
    setVar('--on-dark', p.onDark);

    // Auto-contrast foregrounds.
    setVar('--on-surface', onColor(p.surface));
    setVar('--on-section-alt', onColor(p.sectionAlt));
    setVar('--on-card', onColor(p.card));
    setVar('--on-heading', onColor(p.heading));
    setVar('--on-heading-deep', onColor(headingDeep));
    setVar('--on-pop', onColor(p.pop));
  }

  // Default palette. The wheel is hidden for now, so this is effectively fixed.
  // (When the wheel returns, restore the localStorage read below to persist choices.)
  var themeIndex = 0;
  for (var pi = 0; pi < PALETTES.length; pi++) {
    if (PALETTES[pi].name === 'GoldenBlues') { themeIndex = pi; break; }
  }
  /* try {
    var saved = localStorage.getItem('ubd-theme');
    if (saved !== null) {
      var n = parseInt(saved, 10);
      if (!isNaN(n) && n >= 0 && n < PALETTES.length) { themeIndex = n; }
    }
  } catch (e) { } */

  applyPalette(PALETTES[themeIndex]);

  function labelThemeBtn() {
    var b = document.getElementById('themeBtn');
    if (b) b.title = 'Theme: ' + PALETTES[themeIndex].name + ' (click to change)';
    var nm = document.getElementById('themeName');
    if (nm) nm.textContent = PALETTES[themeIndex].name;
  }

  // Event delegation on document so the click is wired no matter when the
  // button parses in (avoids any script/element ordering race).
  document.addEventListener('click', function (e) {
    if (!e.target.closest || !e.target.closest('#themeBtn')) return;
    themeIndex = (themeIndex + 1) % PALETTES.length;
    applyPalette(PALETTES[themeIndex]);
    labelThemeBtn();
    try { localStorage.setItem('ubd-theme', String(themeIndex)); } catch (e2) { /* ignore */ }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', labelThemeBtn);
  } else {
    labelThemeBtn();
  }

  // Footer year.
  var y = document.getElementById('year');
  if (y) y.textContent = new Date().getFullYear();

  // Contact form: client-side validation only for now.
  // NOTE (developer): wire the submit to a backend/CRM (e.g. /api/ubd-contact) before going live.
  var form = document.getElementById('ubdForm');
  if (!form) return;
  var success = document.getElementById('success');

  function fieldOf(el) { return el.closest('.field'); }
  function isEmail(v) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v); }

  form.addEventListener('submit', function (e) {
    e.preventDefault();

    // Honeypot: a filled hidden field means bot — pretend success, do nothing.
    var hp = document.getElementById('website');
    if (hp && hp.value) { form.style.display = 'none'; success.classList.add('show'); return; }

    var ok = true;

    ['name', 'role'].forEach(function (id) {
      var el = document.getElementById(id);
      var bad = !el.value.trim();
      fieldOf(el).classList.toggle('invalid', bad);
      if (bad) ok = false;
    });

    var email = document.getElementById('email');
    var emailBad = !isEmail(email.value.trim());
    fieldOf(email).classList.toggle('invalid', emailBad);
    if (emailBad) ok = false;

    if (!ok) {
      var firstBad = form.querySelector('.field.invalid');
      if (firstBad) firstBad.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }

    // TODO (developer): POST the collected fields to your intake API here.
    form.style.display = 'none';
    success.classList.add('show');
    success.scrollIntoView({ behavior: 'smooth', block: 'center' });
  });

  // Clear the invalid state as the visitor fixes a field.
  form.addEventListener('input', function (e) {
    var f = e.target.closest('.field');
    if (f) f.classList.remove('invalid');
  });
})();
