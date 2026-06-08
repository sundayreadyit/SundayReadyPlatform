const CACHE_NAME = 'cwc-portal-shell-v1.1.1';
const SHELL_ASSETS = [
  '/manifest.webmanifest',
  '/images/branding/cwc-app-icon-192.png',
  '/images/branding/cwc-app-icon-512.png',
  '/images/branding/cwc-church-logo.png'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(SHELL_ASSETS))
      .catch(() => undefined)
  );
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(
      keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))
    ))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  const path = url.pathname.toLowerCase();

  // Keep app pages, authenticated pages, forms, JSON endpoints, and sensitive areas network-only.
  if (
    request.method !== 'GET' ||
    request.mode === 'navigate' ||
    path.includes('/passwordvault') ||
    path.includes('/account') ||
    path.includes('/profile') ||
    path.includes('/admin') ||
    path.includes('/api') ||
    path.endsWith('.json')
  ) {
    return;
  }

  event.respondWith(
    caches.match(request).then(cached => {
      if (cached) return cached;
      return fetch(request).then(response => {
        if (!response || response.status !== 200 || response.type !== 'basic') return response;
        const clone = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
        return response;
      }).catch(() => cached);
    })
  );
});
