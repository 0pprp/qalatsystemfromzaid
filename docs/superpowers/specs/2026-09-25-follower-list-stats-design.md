# Follower list stats — rating + active + monthly flow

Approved: option A (list score = avg CustomerRatingCalculator points). Approach: expand `Followers/ListStats` in BE_DelegateWebApplication + Flutter `list_stats_page`.

## Card KPIs (per linked list)
- Top-start: AverageScore + FinalRatingLabel
- ActiveCount: WeekPaid/AmountTotalSales **> 3.8%** (WeakWeek window inverted; remaining>0)
- Month (Iraq 1→today): NewCustomers (count, sale sum, day-installment sum)
- Zeroed this month: AmountRemaining=0 and LastPaymentDate in month (count, sale, installment)
- Diff: new − zeroed for count / sale / installment

## Navigation
List cards → rating buckets → names → existing CustomerProfile → back one step.

## APIs
- `GET Followers/ListStats` — enriched rows
- `GET Followers/ListStats/{listId}/rating-customers?level=&asyncId=`
- `GET Followers/ListStats/{listId}/active-customers?asyncId=`
