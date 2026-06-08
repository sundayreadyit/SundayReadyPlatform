const CACHE_NAME = 'cwc-portal-shell-v1.1.0';
const SHELL_ASSETS = [
  '/manifest.webmanifest',
  '/images/branding/cwc-app-icon-192.png',
  '/images/branding/cwc-church-logo.png'
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL_ASSETS)).catch(() => undefined));
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Keep application pages, forms, JSON endpoints, and sensitive vault pages network-only.
  if (request.method !== 'GET' || request.mode === 'navigate' || url.pathname.toLowerCase().includes('/passwordvault')) {
    return;
  }

  event.respondWith(
    caches.match(request).then(cached => cached || fetch(request).then(response => {
      if (!response || response.status !== 200 || response.type !== 'basic') return response;
      const clone = response.clone();
      caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
      return response;
    }).catch(() => cached))
  );
});
