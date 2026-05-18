// CabBook Service Worker - Offline support & caching strategy
const CACHE_VERSION = 'v1';
const CACHE_NAME = `cabbook-${CACHE_VERSION}`;

// Assets to cache on install
const STATIC_ASSETS = [
    '/',
    '/index.html',
    '/app.js',
    '/api.js',
    '/app.css',
    '/manifest.json'
];

// Install event - cache static assets
self.addEventListener('install', event => {
    console.log('Service Worker installing...');
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            console.log('Caching static assets');
            return cache.addAll(STATIC_ASSETS);
        })
    );
    self.skipWaiting();
});

// Activate event - clean up old caches
self.addEventListener('activate', event => {
    console.log('Service Worker activating...');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        console.log('Deleting old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        })
    );
    self.clients.claim();
});

// Fetch event - Network first, fall back to cache
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET requests and external requests
    if (request.method !== 'GET' || !url.origin.includes(self.location.origin)) {
        return;
    }

    // API requests: Network first with cache fallback
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(
            fetch(request)
                .then(response => {
                    // Clone the response
                    const clonedResponse = response.clone();

                    // Cache successful responses
                    if (response.ok) {
                        caches.open(CACHE_NAME).then(cache => {
                            cache.put(request, clonedResponse);
                        });
                    }

                    return response;
                })
                .catch(() => {
                    // Network failed, try cache
                    return caches.match(request).then(cachedResponse => {
                        return cachedResponse || createOfflineResponse(url.pathname);
                    });
                })
        );
        return;
    }

    // Static assets: Cache first, network fallback
    event.respondWith(
        caches.match(request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }

                return fetch(request).then(response => {
                    if (!response || response.status !== 200 || response.type === 'error') {
                        return response;
                    }

                    // Cache successful responses
                    const clonedResponse = response.clone();
                    caches.open(CACHE_NAME).then(cache => {
                        cache.put(request, clonedResponse);
                    });

                    return response;
                });
            })
            .catch(() => {
                // Network failed, try index.html (for SPA routing)
                return caches.match('/index.html');
            })
    );
});

// Create offline response for API calls
function createOfflineResponse(pathname) {
    if (pathname.includes('/booking')) {
        return new Response(JSON.stringify({
            error: 'Offline - Data from cache may be stale',
            offline: true
        }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
        });
    }

    if (pathname.includes('/tenant/config')) {
        // Return cached config for offline
        return caches.match('/api/tenant/config').then(response => {
            return response || new Response(JSON.stringify({
                error: 'Offline - Using cached config',
                offline: true
            }), {
                status: 200,
                headers: { 'Content-Type': 'application/json' }
            });
        });
    }

    return new Response(JSON.stringify({
        error: 'Offline - Cannot reach server'
    }), {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
    });
}

// Background sync for offline bookings (future enhancement)
self.addEventListener('sync', event => {
    if (event.tag === 'sync-bookings') {
        event.waitUntil(syncPendingBookings());
    }
});

async function syncPendingBookings() {
    // This would sync any pending bookings when connection is restored
    // Implementation depends on storing pending bookings in IndexedDB
    console.log('Syncing pending bookings...');
}

// Handle messages from client
self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }

    if (event.data && event.data.type === 'CACHE_CLEAR') {
        caches.delete(CACHE_NAME).then(() => {
            event.ports[0].postMessage({ cleared: true });
        });
    }
});
