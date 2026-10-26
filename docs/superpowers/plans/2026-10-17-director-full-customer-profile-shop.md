# Director Full Customer Profile + Shop 3-Part Implementation Plan

> **For agentic workers:** Implement task-by-task. Steps use checkbox syntax.

**Goal:** Button next to مصادق opens sales-manager customer profile; Back restores same follower + customer; بيانات المحل shows employee → follower → delegate.

**Architecture:** FE query-param return (`returnTo`, `followerId`, `tab`, `resumeCustomerId`). Shop evals already on profile DTO (`FollowerShopEval` / `DelegateShopEval`).

**Tech Stack:** Vue 3, Vuetify, existing director/SM APIs.

## Global Constraints

- Target page: `/sales-manager-customer-profile` with `cityValue` + `customerId`
- Button label: عرض بروفايل الزبون الكامل
- Shop subsections order: موظف مبيعات → متابع → مندوب
- No Flutter / DB changes unless director profile omits evals

---

### Task 1: Director outbound + resume

**Files:**
- Modify: `FE_Company/src/pages/director-follower-work.vue`

- [ ] Add button next to badge; navigate with return query
- [ ] On mount/activated: if `resumeCustomerId` + `followerId`, open detail and set profileRow
- [ ] Clear resume query after apply

### Task 2: Profile Back + shop 3 sections

**Files:**
- Modify: `FE_Company/src/pages/sales-manager-customer-profile.vue`

- [ ] `goBack` prefers `returnTo=director-follower-work` restore URL
- [ ] Restructure بيانات المحل into 3 subsections binding FollowerShopEval / DelegateShopEval

### Task 3: Deploy FE and verify

- [ ] Build + deploy AdminFrontend
- [ ] Smoke: button → profile → back restores customer
