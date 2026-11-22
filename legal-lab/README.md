# Legal system lab

An isolated copy of the system for trying the legal work on this PC. Nothing here touches the
live server or your normal local databases: the lab reads `DatabaseCompanyNajaf` and
`DatabaseCompanyBabil` once to copy them, then works only on the copies.

| Piece | Where |
| --- | --- |
| Branch copies | `LegalLab_Najaf`, `LegalLab_Babil` on `.\SQLEXPRESS` |
| Legal tables | `LegalLab_Legal` (created empty, tables appear on first use) |
| Branch APIs | http://127.0.0.1:5611 (Najaf), http://127.0.0.1:5612 (Babil) |
| Gateway | http://127.0.0.1:5680 |
| Company dashboard | http://localhost:5183 |
| Legal app | http://localhost:5190 |
| Logins, logs | `legal-lab\data\` (`lab-secrets.json` has the three lab accounts) |

## Run it

```powershell
.\legal-lab\setup-lab.ps1      # once: copies the databases, writes lab config and random lab passwords
.\legal-lab\start-lab.ps1      # builds and starts the three backends, creates the legal logins
npm --prefix FE_Company run dev -- --port 5183 --strictPort --mode legallab
npm --prefix FE_Legal run dev -- --port 5190 --strictPort
```

Stop the backends with `.\legal-lab\stop-lab.ps1`. Start again from clean copies with
`.\legal-lab\setup-lab.ps1 -Fresh` (this also issues new lab passwords).

On the dashboard login page, clear the province box (the × inside it) before signing in with a
central account: `lab-legal-accountant` or `lab-director`. The legal app takes `lab-legal-team`
(role «مدير القانونية») or `lab-director`.

## Automatic check

```powershell
python legal-lab\smoke-test.py
```

Runs the whole story through the real APIs and checks every rule (about 60 checks): legal customers
by list, grouping across two branches, sending, each stage move, the 200,000 / 150,000 / 35,000
amounts (35,000 only before a judicial case), both registers (the cash box may go negative), lawyer
wallets and fees, ابطال, payments staying in القانونية with nothing posted to the branch, and who can
and cannot see legal customers. Each run adds a few debtors to the lab.

## To test against today's data instead of the September copies

Restore a fresh backup of a branch under any name on `.\SQLEXPRESS`, then:

```powershell
.\legal-lab\setup-lab.ps1 -Fresh -NajafSource <restored name> -BabilSource <another restored name>
```

## What to try by hand

1. **Hiding.** Log into the normal dashboard as a branch accountant of the copied branch (point a
   normal dev run at port 5611): customers marked قانونية are gone from every list. Set
   `Legal:HideFromNonLegalUsers` to `false` in `BE_Company\appsettings.LegalLab.json` to compare.
2. **Grouping.** قوائم القانونية → pick a list (each shows how many legal customers it holds) → tick accounts → «تجميع في مدين واحد». Use the two
   icons on a row to find the same person's other sales (same branch, or all branches).
3. **Flow.** In the legal app move the debtor station by station; add the same step twice; assign a
   lawyer; mark ابطال; then try ابطال again after التنفيذ (refused).
4. **Money.** السجلات → receive money from the company → transfer to a lawyer's wallet → pay a fee
   from the lawyer's page, once for his own debtor and once for someone else's.
5. **Payments.** Back on the dashboard open the debtor → تسديد. The payment appears under
   «مقبوضات القانونية» on the overview and on the debtor's file; the branch copy is not touched.
6. **Full settlement.** From a debtor still in the archive press «المدين يريد التسديد الكامل», then
   approve it on the dashboard (طلبات التسديد الكامل) with a changed penalty and pay everything:
   the file closes by itself. Repeat for a debtor already in a judicial case: no penalty is offered.

The Aqeel branches (الرصافة عقيل، الكرخ عقيل، ميسان، الكوت) are left out of the legal system: the
gateway skips branches whose name or database matches (`Legal:ExcludedBranches`), and a branch API
whose database name contains Aqeel / Maysan / Kot hides nothing (`Legal:ExcludedDatabases`).
