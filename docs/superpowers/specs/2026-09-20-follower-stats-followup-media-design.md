# Follower UX fixes: stats deltas, field follow-up, sales auth, media

Date: 2026-09-20  
Status: approved (user: أ موافق)

## Stats (option أ)
- Keep portfolio delta: live totals − previous month snapshot.
- UI: show signed difference clearly as زيادة / نقصان / صفر with colors.
- Labels clarify these are monthly differences from new activity on the list portfolio.

## Field follow-up
- Section intro explaining purpose.
- Home cards (paid + unpaid): text button «إرسال للمتابعة الميدانية» (not icon-only).
- Home cards: text button «تمت المتابعة» + required note; persist note to customer profile notes; show last recorded note on card.
- Field follow-ups page keeps complete flow; completion also writes customer note when possible.

## Sales auth
- From PendingSales / pendingAuth profile: only «تمت المصادقة».
- Remove «إرسال طلب مبيع» from that path (keep on normal profile open from home).

## Media
- Fix shop image + KYC docs load in follower app and admin panel (URL resolve / file serve).

## Release
- BE deploy if needed + mandatory follower APK.
