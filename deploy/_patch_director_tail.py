from pathlib import Path

p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\FE_Company\src\pages\director-ratings.vue")
text = p.read_text(encoding="utf-8")
idx = text.index("<template>")
tail = text[idx:]

old_hub = """        <span class=\"count\">
          {{ cityCards.length }} محافظة
          <template v-if=\"hubMonthBusy\"> · جاري الفرق…</template>
        </span>
      </div>

      <div
        v-if=\"branchesLoading && !cityCards.length\"
        class=\"muted\"
      >
        جاري تحميل المحافظات…
      </div>

      <div class=\"cards\">
        <button
          v-for=\"city in cityCards\"
          :key=\"city.value\"
          type=\"button\"
          class=\"province-card\"
          @click=\"openCity(city)\"
        >
          <span class=\"province-name\">{{ city.name }}</span>
          <div
            v-if=\"hubDeltas(city.value)\"
            class=\"province-deltas\"
          >
            <div class=\"delta-row\">
              <span>فرق الزبائن</span>
              <strong :class=\"signedClass(hubDeltas(city.value).netCount)\">
                {{ signedText(hubDeltas(city.value).netCount) }}
              </strong>
            </div>
            <div class=\"delta-row\">
              <span>فرق سعر البيع</span>
              <strong :class=\"signedClass(hubDeltas(city.value).netSale)\">
                {{ signedText(hubDeltas(city.value).netSale, true) }}
              </strong>
            </div>
            <div class=\"delta-row\">
              <span>فرق القسط</span>
              <strong :class=\"signedClass(hubDeltas(city.value).netDay)\">
                {{ signedText(hubDeltas(city.value).netDay, true) }}
              </strong>
            </div>
          </div>
          <span
            v-else
            class=\"province-cta\"
          >{{ hubMonthBusy ? 'يحسب الفرق…' : 'افتح التقييمات' }}</span>
        </button>
      </div>"""

new_hub = """        <span class=\"count\">
          {{ cityCards.length }} محافظة
          <template v-if=\"warmBusy\"> · تجهيز الكاش…</template>
        </span>
      </div>

      <div
        v-if=\"branchesLoading && !cityCards.length\"
        class=\"muted\"
      >
        جاري تحميل المحافظات…
      </div>

      <div class=\"cards\">
        <button
          v-for=\"city in cityCards\"
          :key=\"city.value\"
          type=\"button\"
          class=\"province-card\"
          @click=\"openCity(city)\"
        >
          <span class=\"province-name\">{{ city.name }}</span>
          <span class=\"province-cta\">افتح التقييمات</span>
        </button>
      </div>"""

if old_hub not in tail:
    raise SystemExit("hub block missing")
tail = tail.replace(old_hub, new_hub, 1)

old_inline = """          <div
            v-if=\"detailCurrent\"
            class=\"month-inline\"
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
              <strong :class=\"signedClass(detailCurrent.netCount)\">
                {{ signedText(detailCurrent.netCount) }}
              </strong>
            </p>
          </div>
          <p
            v-if=\"chartGeom\"
            class=\"day-count\"
          >
            {{ chartGeom.count }} نقطة يومية بالرسم — اضغط نقطة لعرض يومها
          </p>"""

new_inline = """          <div
            v-if=\"detailCurrent\"
            class=\"month-inline\"
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
              <strong :class=\"signedClass(detailCurrent.netCount)\">
                {{ signedText(detailCurrent.netCount) }}
              </strong>
            </p>
            <p class=\"month-hint\">
              الجدد = أول بيع هذا الشهر · المصفرون = سددوا المتبقي هذا الشهر — مجموعتان مختلفتان فلا يتساويان
            </p>
          </div>
          <p
            v-if=\"chartGeom\"
            class=\"day-count\"
          >
            {{ chartGeom.count }} نقطة يومية بالرسم (متوسط التقييم فقط) — الجدد/المصفرون في الكروت تحت الرسم
          </p>"""

if old_inline not in tail:
    raise SystemExit("inline block missing")
tail = tail.replace(old_inline, new_inline, 1)

tail = tail.replace("minmax(260px, 1fr)", "minmax(200px, 1fr)", 1)
tail = tail.replace("min-height: 9.5rem;", "min-height: 5.5rem;", 1)

needle = """.month-inline strong {
  color: var(--ink);
}"""
insert = """.month-inline strong {
  color: var(--ink);
}

.month-hint {
  margin-top: 0.35rem !important;
  font-size: 0.78rem !important;
  line-height: 1.35;
  color: var(--muted);
}"""
if needle in tail:
    tail = tail.replace(needle, insert, 1)

Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\deploy\_director_ratings_tail.vue").write_text(tail, encoding="utf-8")
print("tail ok", len(tail))
