# قلعة الضمان — Web Store (`web/store`)

Arabic RTL web store for **قلعة الضمان** — the web twin of the Flutter customer app. It talks only to
`BE_CustomerApp` using the contract in [`docs/API_CONTRACT.md`](../../docs/API_CONTRACT.md) (sections 1–8).

Stack: Vue 3 + Vite 6, vue-router 4, Pinia, Vuetify 3 (RTL, Arabic locale), axios, Cairo font, vitest.

## Quick start

```bash
cd web/store
npm install

# Demo without a backend (built-in fake API with Arabic sample data)
npm run dev:fake          # http://localhost:5173

# Against a real BE_CustomerApp on http://localhost:5380
npm run dev               # /api/* is proxied to VITE_DEV_PROXY_TARGET

npm test                  # vitest unit tests
npm run build             # production build -> dist/
npm run build:fake        # static demo build that uses the fake API
npm run preview           # serve dist/ locally
```

### Fake API mode

Enabled by `VITE_USE_FAKE_API=true` (the `fake` mode in `.env.fake` sets it). An axios adapter
(`src/api/fake/adapter.js`) implements every customer endpoint in sections 1–8 in memory, saving its state to
`localStorage` (`qa.fakeDb.v1`). Real builds do not include it.

- Log in with **any valid Iraqi mobile number**. The OTP code is always **`123456`**.
- **`07701234567`** is a pre-seeded demo account. It has orders in every status, three installment accounts
  across two branches (with a "partial results" warning until you press تحديث), earnings, payouts, a network,
  unread notifications, and account creation for others unlocked.
- Any other number creates a new account. Try the referral link `/r/QA7K2M`.
- Adding a product that already has an open order for the same phone shows the `409 duplicate_open_order` dialog.
  For example, as the demo user, order product 1.
- To reset the demo data, run `__resetFakeDb()` in the browser console.

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `VITE_API_BASE_URL` | `/api/v1/` | Base URL of BE_CustomerApp. It must end with `/`. It can be absolute, e.g. `https://api.qalaa-store.iq/api/v1/` (CORS must allow the store origin). |
| `VITE_DEV_PROXY_TARGET` | `http://localhost:5380` | Dev server only. `/api` is proxied here. |
| `VITE_USE_FAKE_API` | `false` | `true` uses the in-browser fake backend. |
| `VITE_SITE_URL` | `https://store.qalaa.iq` | Informational fallback for links. |
| `VITE_APP_STORE_URL`, `VITE_PLAY_STORE_URL` | empty | Store badge links. When empty, the badges show «قريباً». |

Vite reads these at **build time**. Put overrides in `.env.local` or `.env.production.local` (both git-ignored).

## Project layout

```
src/
  api/          client.js (axios, auth header, refresh-on-401 single-flight), tokens.js, index.js (endpoint modules), fake/
  stores/       auth (session, OTP, referral code capture, login dialog, unread badge polling 60s), cart (localStorage), catalog (reference data + product cache)
  pages/        Home, Catalog, Product (/p/:id), Cart, Checkout wizard, OrderSuccess, Login, Terms, Privacy, Download, NotFound
  pages/account AccountLayout (sidebar / tabs) + Orders, OrderDetail, Purchases, PurchaseDetail, Earnings, Network, Notifications, Profile
  components/   header/footer/bottom nav, ProductCard/Grid, MediaGallery (image, mp4, YouTube), ShareButtons, LoginForm/Dialog, PhoneField, OtpField, ...
  utils/        phone (Iraqi validation/normalisation), money (Latin digits, "1,250,000 د.ع"), orderPayload (POST /orders builder + validation), dates, enums
  router/       routes + auth guard (requiresAuth → /login?redirect=…)
tests/          vitest: phone, money, order payload
```

### Behaviour notes

- **Auth.** Tokens are stored in `localStorage` (`qa.accessToken`, `qa.refreshToken`). On a 401, the client calls
  `POST auth/refresh` once, even when several requests fail together, and then retries the request. If the refresh
  fails, the session is cleared and the user is sent to login from protected pages.
- **Referral.** `/r/:code` saves the code in `localStorage` (`qa.referralCode`) and opens login/registration.
  The registration form is prefilled with the code. An `invalid_referral_code` error lets the user clear the code
  and retry.
- **Checkout.** Every submit attempt sends a new `Idempotency-Key` (`crypto.randomUUID()`). The key is reused only
  when the previous attempt got no HTTP response (a network failure), so a retry cannot create a duplicate order.
  The branch list shows only branches where every cart item is available.
- **Notifications.** While logged in, the unread badge is refreshed every 60 s via `GET notifications?page=1`
  (`unreadCount`). Polling skips hidden tabs.

## Deploy (static hosting behind nginx)

`npm run build` produces a static SPA in `dist/`. Serve it with an SPA fallback, and proxy `/api` to BE_CustomerApp
(or set an absolute `VITE_API_BASE_URL`).

```nginx
server {
    listen 443 ssl http2;
    server_name store.qalaa.iq;
    root /var/www/qalaa-store/dist;
    index index.html;

    # API -> BE_CustomerApp
    location /api/ {
        proxy_pass http://127.0.0.1:5380;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Social previews for product links: let the backend serve server-rendered OG tags to crawlers (see below)
    # location ~ ^/p/\d+$ { if ($http_user_agent ~* "(facebookexternalhit|WhatsApp|TelegramBot|Twitterbot|Slackbot|LinkedInBot)") { proxy_pass http://127.0.0.1:5380; } try_files $uri /index.html; }

    # Hashed assets: cache for a long time
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri =404;
    }

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
        add_header Cache-Control "no-cache";
    }
}
```

### Social previews (`/p/:id`)

The SPA renders `/p/:id` on the client and updates `document.title` and the meta tags in the browser. Crawlers
(WhatsApp, Facebook, Telegram) do not run JavaScript, so **per-product previews come from the backend route
`GET /p/{id}`**, which returns server-rendered OG tags (`og:title`, `og:image`, `og:description`) and then
redirects or serves the SPA. Route crawler user-agents (or all `/p/*` requests) for that path to the backend at
the reverse proxy, as in the commented nginx block above. Every other page uses the defaults in `index.html`.

## Legal pages

`/terms` and `/privacy` hold Arabic **placeholder** text. Both pages show a banner saying they await review by the
company's lawyer. The text covers the data collected, purposes, retention, account deletion, and the referral
program terms.
