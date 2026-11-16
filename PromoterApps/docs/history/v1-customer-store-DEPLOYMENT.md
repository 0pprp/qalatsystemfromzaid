# Deployment guide

## Components

| Component | Folder | Runs as |
|---|---|---|
| BE_CustomerApp (new API) | `backend/BE_CustomerApp` | .NET 10, port 5380, behind nginx/IIS with **HTTPS** |
| CustomerAppDb (new DB) | created automatically on first start | SQL Server (same server as the gateway is fine) |
| Web store | `web/store` | static files (nginx), SPA fallback |
| Mobile app | `mobile/customer_app` | Google Play + App Store |
| Gateway changes | `qalatsystemfromzaid-main/BE_SalesEmployee` | redeploy the SalesGateway |
| Branch changes | `qalatsystemfromzaid-main/BE_Company` | redeploy **every Qalaa `Admin*` branch** (1–6, 8–14) |
| Dashboard pages | `qalatsystemfromzaid-main/FE_Company` | rebuild the FE_Company Docker image |

Every changed file in the company repo is listed in [CHANGES_IN_COMPANY_REPO.md](CHANGES_IN_COMPANY_REPO.md). The originals are in `company_repo_backup/`.

## Order of rollout
1. Deploy the BE_Company branch changes to all Qalaa branches. The changes are additive; the existing CRM intake is unchanged when `source` is absent.
2. Deploy the gateway (BE_SalesEmployee).
3. Deploy BE_CustomerApp with `Gateway:UseFake=false`.
4. Rebuild and deploy FE_Company. In nginx, `/customer-app/` proxies to `127.0.0.1:5380/`.
5. Deploy the web store.
6. Publish the mobile apps after the store listings are approved.

## BE_CustomerApp configuration (`appsettings.Production.json` or environment variables)

| Key | Value |
|---|---|
| `ConnectionStrings:CustomerApp` | SQL Server connection. The database is created and migrated at startup. In production give the login `db_owner` on CustomerAppDb only. |
| `Jwt:Key` | New random secret, at least 32 characters. Used for customer tokens. |
| `GatewayJwt:Key` | **Same value as the gateway's `Jwt:Key`**, so dashboard logins work here. |
| `Otp:Secret` | New random secret. |
| `Otp:FixedCodes:07XXXXXXXXX` | Only the App Store / Play reviewer test number, with a fixed code. |
| `Gateway:BaseUrl` | `http://127.0.0.1:5280/api/` (the gateway, server to server). |
| `Gateway:Key` | The gateway's `InternalApiKey`. |
| `PublicUrls:Store` / `PublicUrls:Api` | e.g. `https://store.qalaa.iq` and `https://api.qalaa-store.iq`. |
| `Sms:Provider` = `Http`, `Sms:Http:*` | Your SMS/OTP provider. Templates support `{phone}` `{phoneIntl}` `{phoneIntlNoPlus}` `{code}` `{message}`. |
| `Push:Fcm:ProjectId`, `Push:Fcm:ServiceAccountFile` | Firebase project id and the path to the service-account JSON. |
| `Media:Root` | Folder for uploaded product images/videos. Back it up. |
| `Cors:AllowedOrigins` | `["https://store.qalaa.iq", "https://<FE_Company host>"]` |

Publish with:

```bash
dotnet publish backend/BE_CustomerApp -c Release -o /opt/CustomerApp
```

Then run it as a service (systemd or IIS, the same way the gateway runs).

Background jobs run inside the API:
- order-status sync with the company system, every 2 minutes;
- commission maturation, every 10 minutes;
- cleanup, every 6 hours.

Run **one** instance. Scaling out later needs a distributed lock for the jobs and a distributed cache.

### nginx (API)
```nginx
server {
  server_name api.qalaa-store.iq;
  listen 443 ssl http2;            # certificates via certbot / your provider
  client_max_body_size 110m;       # product video uploads
  location / { proxy_pass http://127.0.0.1:5380; proxy_set_header Host $host;
               proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; proxy_set_header X-Forwarded-Proto $scheme; }
}
```
`/p/{id}` (product share previews) and `/r/{code}` (invite links) are served by this API. Point the store domain's `/p/*` and `/r/*` to it too if you want those links on the store domain.

## Backups
- CustomerAppDb holds the financial ledger: commission entries, payouts and the audit log. Include it in the nightly SQL backups and keep point-in-time log backups.
- Back up the `Media:Root` folder as well.

## Smoke test after deploy
- `GET /health` → `{status:"ok"}`
- `GET /api/v1/branches` → 13 branches, no Kut, Maysan or Aqeel.
- Log in to FE_Company as المدير and open «تطبيق الزبون → فلترة طلبات التطبيق».
- Place a test order from the app, approve it, then check that it appears in «طلبات البيع» with source «تطبيق الزبون».
