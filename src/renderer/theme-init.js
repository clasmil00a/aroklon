'use strict';

/* Set the theme/layout/language before the stylesheet loads (no flash). */
(function () {
  try {
    const mode = localStorage.getItem('aroklon.mode') || 'dark';
    const layout = localStorage.getItem('aroklon.layout') || 'modern';
    const lang = localStorage.getItem('aroklon.lang') || 'id';
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const effective = mode === 'system' ? (prefersDark ? 'dark' : 'light') : mode;
    document.documentElement.dataset.mode = effective === 'dark' ? 'dark' : 'light';
    document.documentElement.dataset.layout = layout === 'classic' ? 'classic' : 'modern';
    document.documentElement.lang = lang === 'en' ? 'en' : 'id';
  } catch (e) {
    /* ignore */
  }
})();