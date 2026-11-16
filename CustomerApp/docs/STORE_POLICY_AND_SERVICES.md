# App Store / Google Play policy review + services needed

> **v2 note (2026-11-03):** the app is now distributed as an APK and is not published on the App Store or Google Play. Section A described the restrictions that applied to the store-ready v1 (kept in `backups/`); they were removed in v2 — see [V2_CHANGES.md](V2_CHANGES.md). Items 3, 4 and 11 of section B (Apple Developer, Google Play Console, reviewer test number) are no longer needed.

## A. Policy risks in the requested features (and what was done)

### 1. Multi-level referral commissions — HIGHEST RISK
- **Rules:** Neither store's written rules ban paying commissions on real product sales. But both review "earn by recruiting others" apps closely. Apple rejects apps it considers pyramid schemes or that "encourage illegal activity" (Guideline 1.4 / 5 – Legal). Google has comparable deceptive-behaviour rules.
- **Already built in to reduce risk:**
  - Commissions are paid **only on completed real sales**, confirmed by the company system. Nothing is paid for sign-ups and there is no joining fee.
  - The app opens as a **shopping app**. Products come first, as the owner requested.
  - Network members' personal data and purchases are hidden. Network commissions show only the level.
  - The terms say earnings are not guaranteed income. The company can reverse commissions for returns and cancellations.
- **Recommended:**
  - Have a lawyer confirm that multi-level commissions are legal in Iraq for your company.
  - Avoid wording such as "اربح دخلاً ثابتاً" in store listings and screenshots.
  - In the App Review notes, explain that commissions are a sales-referral reward on completed purchases of physical goods.
- **Fallback if a store rejects it:**
  - Ship the store app with shopping, orders, installments and a *single-level* "invite a friend" reward.
  - Keep the full multi-level earnings dashboard on the website.
  - Do **not** hide the feature only during review. That violates Apple 2.3.1 and gets the developer account banned.

### 2. Daily installments vs. "loan app" rules — MEDIUM RISK
- **The rule:**
  - Apple 3.2.2(ix): personal-loan apps must disclose APR, may not exceed 36% APR, and may not require repayment in 60 days or less.
  - Google: the Personal Loans policy and the Financial Features declaration.
- **Why it matters:** a 650,000 cash price against a 780,000 installment price over 120 days works out above 36% APR, if a reviewer treats it as a loan.
- **What was done:**
  - The app never lends money. It sells goods at an *installment price*, and the field sales rep decides on the sale.
  - Screens show the cash price, installment price, daily installment and duration.
  - No "قرض" or "فائدة" wording is used.
- **You must:**
  - Answer Google Play's **Financial features declaration** honestly: "installment purchase of goods sold by us", not "personal loans".
  - Keep loan wording out of the listing.
  - Keep durations above 60 days.

### 3. Account requirements — handled
- Guest browsing is available without an account (Apple 5.1.1).
- Accounts use phone OTP only. No social login is offered, so **Sign in with Apple is not required**.
- **Account deletion inside the app** (Apple 5.1.1(v), Google Data deletion):
  - It anonymises personal data and keeps the financial ledger.
  - You must also publish a **web page** for deletion requests; Google requires a URL.

### 4. Creating accounts for other people — handled
The OTP goes to the *new person's* phone, and the code they share is their consent. Without it no account is created.

### 5. Push notifications — handled
- Order and commission notifications are transactional.
- Promotional broadcasts go only to users who ticked the marketing opt-in (unchecked by default), which can be turned off in «حسابي» (Apple 4.5.4).

### 6. Payments
These are physical goods, paid in cash or installments outside the app. No In-App Purchase is needed (Apple 3.1.3(e)).

### 7. OTP auto-fill
The app uses SMS auto-fill hints only and does **not** request READ_SMS / RECEIVE_SMS; Google restricts those permissions.

### 8. Developer accounts
- Register both stores as an **Organization**, with a D-U-N-S number in the company's legal name:
  - Apple 5.1.1(ix) requires a legal entity for financial-services apps.
  - Google requires organization accounts for apps with financial features.
  - A new *personal* Google account would also need 12 testers for 14 days before production.
- The listing's developer name must match the company («قلعة الضمان»).

### 9. HTTPS
The existing company servers use `http://`. iOS ATS and Android 9+ block cleartext, so the **new API and store must be on HTTPS**. Gateway calls are server to server and can stay internal.

### 10. Privacy disclosures
Fill in Google **Data safety** and Apple **App Privacy** with what is collected:
- phone number, names and address;
- precise location, only when the user taps "use my location";
- purchase history;
- device push token.

Data is not sold and is not used for tracking, so ATT is not needed.

---

## B. What I need from you

| # | Item | Why / notes |
|---|---|---|
| 1 | **SMS/OTP provider for Iraq** + API credentials | Iraqi options: **OTPIQ** (SMS + WhatsApp OTP), Infobip, Twilio Verify (more expensive to Iraq). The backend's HTTP sender is configurable, so give me the provider's API doc and keys and I'll set the template. |
| 2 | **Firebase project** (free) | Push notifications. I need: the service-account JSON (server), `google-services.json` (Android), `GoogleService-Info.plist` (iOS). |
| 3 | **Apple Developer Program** (Organization, USD 99/yr) + **APNs key (.p8)** uploaded to Firebase | iOS publishing and iOS push. |
| 4 | **Google Play Console** (Organization, USD 25 once) | Android publishing. |
| 5 | **Domains + hosting** | e.g. `store.qalaa.iq` (web) and `api.qalaa-store.iq` (API), TLS certificates, and a SQL Server database for `CustomerAppDb`. |
| 6 | **Secrets from the current system** | Gateway `InternalApiKey` and gateway `Jwt:Key` (so the dashboard login works in the new API). |
| 7 | **Brand assets** | High-resolution logo, app icon (1024×1024), final app name for the stores, and store screenshot copy. |
| 8 | **Legal texts** | Privacy policy, terms of use, referral-program terms. Placeholders are in the web store at `/terms` and `/privacy`; a lawyer should review them. |
| 9 | **Business settings** (all editable later in the dashboard) | Defaults I chose:<br>• hold period before earnings become payable: **7 days**<br>• rounding unit: **250 IQD**<br>• minimum payout: **10,000 IQD**<br>• account-creation unlock: **5** completed sales<br>• referral ratio: **50%**<br>• auto-reverse commissions when a sale is cancelled in the system: **on**<br>• payout methods: **cash / ZainCash / QiCard** |
| 10 | **Support contacts** | Support phone and WhatsApp shown in the app. |
| 11 | **Reviewer test number** | A phone number reserved for App Review, set with a fixed OTP in `Otp:FixedCodes`. |
| 12 | **Product data** | Real products, photos/videos, prices, base commissions and the governorates for each product. These are entered from the dashboard. |
