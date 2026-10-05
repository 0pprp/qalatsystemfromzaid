<template>
  <div class="director-shell">
    <header class="director-hero">
      <div>
        <p class="eyebrow">
          لوحة المدير
        </p>
        <h1 :class="selected ? 'province-title' : 'hub-title'">
          {{ selected ? selected.name : 'التقييمات' }}
        </h1>
        <p class="sub">
          {{ selected
            ? 'الفترة = تاريخ البيع فقط — التقييم والتسديدات تُحسب حتى اليوم (الرسم من أول بيع في الفترة إلى اليوم). اضغط ممتاز/جيد/… للأسماء ثم البروفايل'
            : 'كل المحافظات في نظرة واحدة. اضغط كارت للمحافظة.' }}
        </p>
      </div>
      <div
        v-if="selected"
        class="hero-actions"
      >
        <VBtn
          variant="tonal"
          color="surface"
          @click="backToHub"
        >
          كل المحافظات
        </VBtn>
      </div>
    </header>

    <section
      v-if="!selected"
      class="hub"
    >
      <div class="hub-bar">
        <VTextField
          v-model="search"
          density="comfortable"
          variant="solo-filled"
          flat
          hide-details
          prepend-inner-icon="tabler-search"
          placeholder="بحث محافظة…"
          class="search"
        />
        <span class="count">
          {{ cityCards.length }} محافظة
          <template v-if="warmBusy"> · تجهيز الكاش…</template>
        </span>
      </div>

      <div
        v-if="branchesLoading && !cityCards.length"
        class="muted"
      >
        جاري تحميل المحافظات…
      </div>

      <div class="cards">
        <button
          v-for="city in cityCards"
          :key="city.value"
          type="button"
          class="province-card"
          @click="openCity(city)"
        >
          <span class="province-name">{{ city.name }}</span>
          <span class="province-cta">افتح التقييمات</span>
        </button>
      </div>
    </section>

    <section
      v-else
      class="detail"
    >
      <div class="filters">
        <VTextField
          v-model="fromDate"
          type="date"
          label="بيع من"
          density="comfortable"
          variant="outlined"
          hide-details
        />
        <VTextField
          v-model="toDate"
          type="date"
          label="بيع إلى"
          density="comfortable"
          variant="outlined"
          hide-details
        />
        <VBtn
          color="primary"
          size="large"
          :loading="trendBusy"
          @click="loadTrend"
        >
          تطبيق فترة البيع
        </VBtn>
      </div>

      <div class="scope-row">
        <button
          v-for="s in scopes"
          :key="s.value"
          type="button"
          class="scope-chip"
          :class="{ active: scope === s.value }"
          @click="scope = s.value"
        >
          <strong>{{ s.title }}</strong>
          <small>{{ s.hint }}</small>
        </button>
      </div>

      <div
        v-if="needsEntity"
        class="entity-row"
      >
        <VSelect
          v-model="entityId"
          :items="entityOptions"
          item-title="name"
          item-value="id"
          :label="scope === 'list' ? 'المندوب / القائمة' : scope === 'follower' ? 'المتابع' : 'موظف المبيعات'"
          :loading="catalogBusy"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          :disabled="!entityOptions.length"
        />
      </div>

      <div class="result-panel">
        <div class="score-block">
          <p class="metric">
            {{ metricLabel }}
          </p>
          <p class="score">
            {{ scoreText }}
          </p>
          <p
            class="label-pill"
            :data-dir="direction"
          >
            {{ ratingLabel }}
          </p>
          <p class="cust-count">
            كل الزبائن ضمن الفترة:
            <strong>{{ customerCount == null ? '—' : customerCount }}</strong>
          </p>
          <div
            v-if="detailCurrent"
            class="month-inline"
          >
            <p>
              زبائن جدد ({{ formatYm(detailCurrent.yearMonth) }}):
              <strong>{{ detailCurrent.newCount }}</strong>
            </p>
            <p>
              مصفرون:
              <strong>{{ detailCurrent.zeroCount }}</strong>
            </p>
            <p>
              الفرق:
              <strong :class="signedClass(detailCurrent.netCount)">
                {{ signedText(detailCurrent.netCount) }}
              </strong>
            </p>
            <p class="month-hint">
              الجدد = أول بيع هذا الشهر · المصفرون = سددوا المتبقي هذا الشهر — مجموعتان مختلفتان فلا يتساويان
            </p>
          </div>
          <p
            v-if="chartGeom"
            class="day-count"
          >
            {{ chartGeom.count }} نقطة يومية بالرسم (متوسط التقييم فقط) — الجدد/المصفرون في الكروت تحت الرسم
          </p>
        </div>
        <div class="spark">
          <div
            v-if="selectedPoint"
            class="point-pop"
          >
            <strong>{{ selectedPoint.label }}</strong>
            <span>التقييم: {{ Number(selectedPoint.value).toFixed(2) }}</span>
            <button
              type="button"
              class="point-close"
              aria-label="إغلاق"
              @click="clearPoint"
            >
              ×
            </button>
          </div>
          <svg
            v-if="chartGeom"
            :viewBox="`0 0 ${chartGeom.w} ${chartGeom.h}`"
            preserveAspectRatio="none"
            class="trend-svg"
            role="img"
            aria-label="رسم التقييم اليومي"
          >
            <path
              :d="chartGeom.path"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
            <g
              v-for="c in chartGeom.coords"
              :key="c.index"
              class="pt"
              :class="{ active: selectedPoint && selectedPoint.date === c.date }"
              @click.stop="pickPoint(c)"
            >
              <circle
                :cx="c.x"
                :cy="c.y"
                r="14"
                class="hit"
              />
              <circle
                :cx="c.x"
                :cy="c.y"
                :r="selectedPoint && selectedPoint.date === c.date ? 6 : 4"
                class="dot"
              />
            </g>
          </svg>
          <div
            v-if="chartGeom"
            class="axis-row"
          >
            <span>{{ formatDay(chartGeom.first) }}</span>
            <span>{{ formatDay(chartGeom.last) }}</span>
          </div>
          <p
            v-if="!points.length && !trendBusy"
            class="muted"
          >
            لا نقاط في هذه الفترة — غيّر التاريخ أو النطاق.
          </p>
        </div>
      </div>

      <div
        v-if="detailMonthBusy && !detailCurrent"
        class="muted month-loading"
      >
        جاري تحميل إحصائيات الشهر…
      </div>

      <div
        v-if="detailPrevious || detailCurrent"
        class="month-stats"
      >
        <article
          v-if="detailPrevious"
          class="month-card month-card--prev"
        >
          <header>
            <h3>الشهر السابق</h3>
            <span>{{ formatYm(detailPrevious.yearMonth) }}</span>
          </header>
          <div class="month-section">
            <p class="month-label">زبائن جدد</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailPrevious.newCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailPrevious.newSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailPrevious.newDay) }} د.ع</strong></div>
          </div>
          <div class="month-section">
            <p class="month-label">مصفرون</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailPrevious.zeroCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailPrevious.zeroSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailPrevious.zeroDay) }} د.ع</strong></div>
          </div>
          <div class="month-section month-section--net">
            <p class="month-label">الفرق (جدد − مصفرون)</p>
            <div class="metric-line">
              <span>فرق العدد</span>
              <strong :class="signedClass(detailPrevious.netCount)">{{ signedText(detailPrevious.netCount) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق سعر البيع</span>
              <strong :class="signedClass(detailPrevious.netSale)">{{ signedText(detailPrevious.netSale, true) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق القسط</span>
              <strong :class="signedClass(detailPrevious.netDay)">{{ signedText(detailPrevious.netDay, true) }}</strong>
            </div>
          </div>
        </article>

        <article
          v-if="detailCurrent"
          class="month-card month-card--cur"
        >
          <header>
            <h3>الشهر الحالي</h3>
            <span>{{ formatYm(detailCurrent.yearMonth) }}</span>
          </header>
          <div class="month-section">
            <p class="month-label">زبائن جدد</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailCurrent.newCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailCurrent.newSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailCurrent.newDay) }} د.ع</strong></div>
          </div>
          <div class="month-section">
            <p class="month-label">مصفرون</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailCurrent.zeroCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailCurrent.zeroSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailCurrent.zeroDay) }} د.ع</strong></div>
          </div>
          <div class="month-section month-section--net">
            <p class="month-label">الفرق (جدد − مصفرون)</p>
            <div class="metric-line">
              <span>فرق العدد</span>
              <strong :class="signedClass(detailCurrent.netCount)">{{ signedText(detailCurrent.netCount) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق سعر البيع</span>
              <strong :class="signedClass(detailCurrent.netSale)">{{ signedText(detailCurrent.netSale, true) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق القسط</span>
              <strong :class="signedClass(detailCurrent.netDay)">{{ signedText(detailCurrent.netDay, true) }}</strong>
            </div>
          </div>
        </article>
      </div>

      <div
        v-if="showBuckets && bucketCounts"
        class="buckets"
      >
        <button
          v-for="b in ratingBuckets"
          :key="b.key"
          type="button"
          class="bucket-card"
          :class="{ active: activeBucket?.key === b.key && bucketDialog }"
          @click="openBucket(b)"
        >
          <span>{{ b.label }}</span>
          <strong>{{ bucketCounts[b.countKey] }}</strong>
        </button>
      </div>
    </section>

    <VDialog
      v-model="bucketDialog"
      max-width="920"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between flex-wrap ga-2">
          <span>
            زبائن {{ activeBucket?.label || '' }}
            <template v-if="selected">
              — {{ selected.name }}
            </template>
          </span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="bucketDialog = false"
          />
        </VCardTitle>
        <VCardText>
          <div class="bucket-toolbar mb-4">
            <VTextField
              v-model="bucketSearch"
              density="comfortable"
              variant="outlined"
              hide-details
              prepend-inner-icon="tabler-search"
              placeholder="بحث بالاسم أو الهاتف…"
              @keyup.enter="searchBucketServer"
            />
            <VBtn
              color="primary"
              variant="tonal"
              :loading="bucketBusy"
              @click="searchBucketServer"
            >
              بحث في السيرفر
            </VBtn>
          </div>
          <p
            v-if="bucketTruncated"
            class="muted mb-3"
          >
            عُرض أول {{ bucketCustomers.length }} اسماً من أصل {{ bucketCounts?.[activeBucket.countKey] }} —
            استخدم البحث للوصول لزبون محدد.
          </p>
          <div
            v-if="bucketBusy"
            class="muted"
          >
            جاري تحميل الأسماء…
          </div>
          <VAlert
            v-else-if="bucketError"
            type="error"
            variant="tonal"
          >
            {{ bucketError }}
          </VAlert>
          <p
            v-else-if="filteredBucketCustomers.length === 0"
            class="muted"
          >
            لا زبائن في هذه الحالة.
          </p>
          <div
            v-else
            class="customer-grid"
          >
            <button
              v-for="c in filteredBucketCustomers"
              :key="c.customerId"
              type="button"
              class="customer-card"
              @click="openCustomerProfile(c)"
            >
              <strong class="customer-name">{{ c.customerName }}</strong>
              <div class="customer-meta">
                <span v-if="c.phone">{{ c.phone }}</span>
                <span>{{ c.ratingLabel }}</span>
                <span v-if="c.amountRemaining != null">متبقي: {{ money(c.amountRemaining) }}</span>
              </div>
            </button>
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.director-shell {
  --ink: #14201b;
  --muted: #5c6b64;
  --sand: #eef3ef;
  --card: #f7faf8;
  --accent: #1f6f5b;
  --accent-deep: #0f3d32;
  --warn: #b45309;
  min-height: calc(100vh - 6rem);
  padding: 0.5rem 0.25rem 2rem;
  color: var(--ink);
}

.director-hero {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
  padding: 1.25rem 1.5rem;
  border-radius: 1.25rem;
  background:
    radial-gradient(1200px 280px at 10% -40%, rgba(31, 111, 91, 0.28), transparent 60%),
    linear-gradient(135deg, #0f3d32 0%, #1a5648 55%, #244a3f 100%);
  color: #f4faf7;
  margin-bottom: 1.25rem;
}

.eyebrow {
  margin: 0;
  opacity: 0.75;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  text-transform: uppercase;
}

.director-hero h1 {
  margin: 0.2rem 0;
  font-size: clamp(1.75rem, 3vw, 2.4rem);
  font-weight: 700;
  line-height: 1.15;
}

.director-hero h1.province-title,
.director-hero h1.hub-title {
  color: #ffffff !important;
  font-weight: 800 !important;
  -webkit-text-fill-color: #ffffff;
  opacity: 1;
}

.sub {
  margin: 0;
  opacity: 0.85;
  max-width: 40rem;
}

.hub-bar {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1rem;
}

.search {
  flex: 1;
  max-width: 28rem;
}

.count {
  color: var(--muted);
  font-size: 0.95rem;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.95rem;
}

.province-card {
  text-align: start;
  border: 1px solid rgba(20, 32, 27, 0.08);
  background:
    linear-gradient(165deg, #ffffff 0%, #f4f8f6 100%);
  border-radius: 1.15rem;
  padding: 1.15rem 1.2rem 1.05rem;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
  box-shadow: 0 1px 0 rgba(15, 61, 50, 0.04);
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-height: 5.5rem;
}

.province-card:hover,
.province-card:focus-visible {
  transform: translateY(-2px);
  border-color: rgba(31, 111, 91, 0.35);
  box-shadow: 0 12px 28px rgba(15, 61, 50, 0.1);
  outline: none;
}

.province-name {
  display: block;
  font-size: 1.2rem;
  font-weight: 800;
  margin: 0;
  color: var(--accent-deep);
}

.province-cta {
  color: var(--accent);
  font-size: 0.9rem;
  margin-top: auto;
}

.province-deltas {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin-top: 0.15rem;
}

.delta-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.75rem;
  font-size: 0.86rem;
}

.delta-row span {
  color: var(--muted);
}

.delta-row strong {
  font-variant-numeric: tabular-nums;
  font-weight: 800;
  white-space: nowrap;
}

.delta-pos { color: #15803d !important; }
.delta-neg { color: #b91c1c !important; }
.delta-flat { color: #4b5563 !important; }

.month-inline {
  margin-top: 0.65rem;
  padding-top: 0.55rem;
  border-top: 1px dashed rgba(20, 32, 27, 0.12);
  display: grid;
  gap: 0.2rem;
  font-size: 0.88rem;
  color: var(--muted);
}

.month-inline p {
  margin: 0;
}

.month-inline strong {
  color: var(--ink);
}

.month-hint {
  margin-top: 0.35rem !important;
  font-size: 0.78rem !important;
  line-height: 1.35;
  color: var(--muted);
}

.month-loading {
  margin: 0.75rem 0;
}

.month-stats {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
  margin: 1rem 0 1.15rem;
}

.month-card {
  background: #fff;
  border: 1px solid rgba(20, 32, 27, 0.08);
  border-radius: 1.1rem;
  padding: 1rem 1.1rem 1.05rem;
  box-shadow: 0 8px 22px rgba(15, 61, 50, 0.05);
}

.month-card--prev {
  background: linear-gradient(180deg, #f8faf9 0%, #eef3ef 100%);
}

.month-card--cur {
  border-color: rgba(31, 111, 91, 0.28);
  background: linear-gradient(180deg, #ffffff 0%, #eef7f3 100%);
}

.month-card header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 0.7rem;
  gap: 0.5rem;
}

.month-card header h3 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: var(--accent-deep);
}

.month-card header span {
  color: var(--muted);
  font-size: 0.85rem;
  font-variant-numeric: tabular-nums;
}

.month-section {
  padding: 0.45rem 0;
  border-top: 1px solid rgba(20, 32, 27, 0.06);
}

.month-section--net {
  background: rgba(31, 111, 91, 0.05);
  margin: 0.35rem -0.35rem 0;
  padding: 0.55rem 0.55rem 0.4rem;
  border-radius: 0.75rem;
  border-top: none;
}

.month-label {
  margin: 0 0 0.35rem;
  font-weight: 700;
  color: var(--accent);
  font-size: 0.9rem;
}

.metric-line {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  font-size: 0.9rem;
  margin-bottom: 0.28rem;
}

.metric-line span {
  color: var(--muted);
}

.metric-line strong {
  font-variant-numeric: tabular-nums;
}

@media (max-width: 900px) {
  .month-stats {
    grid-template-columns: 1fr;
  }
}

.filters {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 0.75rem;
  margin-bottom: 1rem;
  align-items: end;
}

.scope-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.65rem;
  margin-bottom: 1rem;
}

.scope-chip {
  border: 1px solid rgba(20, 32, 27, 0.1);
  background: #fff;
  border-radius: 0.9rem;
  padding: 0.85rem 0.9rem;
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.scope-chip strong {
  display: block;
  font-size: 1rem;
}

.scope-chip small {
  color: var(--muted);
}

.scope-chip.active {
  border-color: var(--accent);
  background: rgba(31, 111, 91, 0.08);
}

.entity-row {
  margin-bottom: 1rem;
  max-width: 28rem;
}

.result-panel {
  display: grid;
  grid-template-columns: minmax(180px, 240px) 1fr;
  gap: 1rem;
  background: var(--sand);
  border-radius: 1.15rem;
  padding: 1.25rem;
  min-height: 10rem;
}

.metric {
  margin: 0;
  color: var(--muted);
  font-size: 0.85rem;
}

.score {
  margin: 0.25rem 0;
  font-size: 2.75rem;
  font-weight: 800;
  letter-spacing: -0.03em;
  color: var(--accent-deep);
  line-height: 1;
}

.label-pill {
  display: inline-block;
  margin: 0;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: rgba(31, 111, 91, 0.12);
  color: var(--accent-deep);
  font-weight: 600;
}

.label-pill[data-dir='down'] {
  background: rgba(180, 83, 9, 0.15);
  color: var(--warn);
}

.cust-count,
.day-count {
  margin: 0.35rem 0 0;
  color: var(--muted);
  font-size: 0.8rem;
}

.cust-count strong {
  color: var(--accent-deep);
  font-size: 1rem;
}

.spark {
  color: var(--accent);
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: 10rem;
  position: relative;
}

.spark .trend-svg {
  width: 100%;
  height: 9rem;
  cursor: crosshair;
}

.spark .hit {
  fill: transparent;
  cursor: pointer;
}

.spark .dot {
  fill: currentColor;
  pointer-events: none;
}

.spark .pt.active .dot {
  fill: #0f3d32;
  stroke: #fff;
  stroke-width: 2;
}

.point-pop {
  position: absolute;
  top: 0.25rem;
  inset-inline-start: 0.5rem;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.45rem 0.7rem;
  border-radius: 0.65rem;
  background: #0f3d32;
  color: #f4faf7;
  font-size: 0.9rem;
  box-shadow: 0 8px 20px rgba(15, 61, 50, 0.25);
}

.point-pop strong {
  font-weight: 800;
}

.point-close {
  border: 0;
  background: transparent;
  color: inherit;
  font-size: 1.25rem;
  line-height: 1;
  cursor: pointer;
  padding: 0 0.15rem;
}

.axis-row {
  display: flex;
  justify-content: space-between;
  margin-top: 0.35rem;
  color: var(--muted);
  font-size: 0.75rem;
}

.buckets {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.5rem;
}

.bucket-card {
  background: #fff;
  border-radius: 0.75rem;
  padding: 0.75rem;
  border: 1px solid rgba(20, 32, 27, 0.06);
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease, background 0.15s ease;
}

.bucket-card:hover,
.bucket-card.active {
  border-color: rgba(31, 111, 91, 0.45);
  background: rgba(31, 111, 91, 0.06);
  box-shadow: 0 8px 18px rgba(15, 61, 50, 0.08);
}

.bucket-card span {
  display: block;
  color: var(--muted);
  font-size: 0.8rem;
}

.bucket-card strong {
  font-size: 1.25rem;
}

.bucket-toolbar {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.75rem;
  align-items: center;
}

.customer-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.65rem;
}

.customer-card {
  text-align: start;
  border: 1px solid rgba(20, 32, 27, 0.08);
  background: var(--card);
  border-radius: 0.85rem;
  padding: 0.85rem 1rem;
  cursor: pointer;
  transition: border-color 0.15s ease, transform 0.12s ease;
}

.customer-card:hover {
  border-color: rgba(31, 111, 91, 0.4);
  transform: translateY(-1px);
}

.customer-name {
  display: block;
  font-size: 1.05rem;
  margin-bottom: 0.35rem;
}

.customer-meta {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  color: var(--muted);
  font-size: 0.8rem;
}

.muted {
  color: var(--muted);
}

@media (max-width: 960px) {
  .scope-row,
  .filters,
  .result-panel,
  .buckets {
    grid-template-columns: 1fr 1fr;
  }

  .filters,
  .bucket-toolbar {
    grid-template-columns: 1fr;
  }

  .result-panel {
    grid-template-columns: 1fr;
  }
}
</style>
