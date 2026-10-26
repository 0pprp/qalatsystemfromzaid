export default [
  {
    title: 'الرئيسية',
    to: { name: 'root' },
    icon: { icon: 'tabler-home', size: '25' },
  },
  {
    title: 'التقييمات',
    // Explicit empty query → hub cards (not last ?city=)
    to: { name: 'director-ratings', query: {} },
    icon: { icon: 'tabler-chart-histogram', size: '25' },
  },
  {
    title: 'الملخص',
    to: { name: 'director-summary' },
    icon: { icon: 'tabler-table', size: '25' },
  },
  {
    title: 'متابعة المندوبين',
    to: () => {
      const city = String(localStorage.getItem('director-delegate-followup-city') || '').trim()

      return { name: 'director-delegate-followup', query: city ? { city } : {} }
    },
    icon: { icon: 'tabler-users', size: '25' },
  },
  {
    title: 'متابعة المبيعات',
    to: () => {
      const city = String(localStorage.getItem('director-sales-followup-city') || '').trim()

      return { name: 'director-sales-followup', query: city ? { city } : {} }
    },
    icon: { icon: 'tabler-shopping-cart', size: '25' },
  },
  {
    title: 'عمل المتابعين',
    icon: { icon: 'tabler-user-scan', size: '25' },
    children: [
      {
        title: 'مصادقة البيع',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'sales', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-certificate', size: '22' },
      },
      {
        title: 'المتدنية',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'declining', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-trending-down', size: '22' },
      },
      {
        title: 'الملاحظات',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'notes', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-message', size: '22' },
      },
      {
        title: 'الزبائن القديمة',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'oldCustomers', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-users-group', size: '22' },
      },
      {
        title: 'ملاحظات المندوبين',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'delegateNotes', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-notes', size: '22' },
      },
      {
        title: 'الجباية',
        to: () => {
          const city = String(localStorage.getItem('director-follower-work-city') || '').trim()

          return { name: 'director-follower-work', query: { tab: 'collection', ...(city ? { city } : {}) } }
        },
        icon: { icon: 'tabler-cash', size: '22' },
      },
      // FollowerRoutesInMonitorWork_V1 — moved from accountant «متابعة المتابعين»
      {
        title: 'الموقع المباشر',
        to: { name: 'sales-manager-followers-map' },
        icon: { icon: 'tabler-map', size: '22' },
      },
      {
        title: 'المسارات',
        to: { name: 'sales-manager-followers-routes' },
        icon: { icon: 'tabler-route', size: '22' },
      },
    ],
  },
  {
    title: 'طلبات البيع',
    to: { name: 'sales-manager-requests' },
    icon: { icon: 'tabler-send', size: '25' },
  },
  {
    title: 'الفيديوهات التعليمية',
    to: { name: 'director-tutorial-videos' },
    icon: { icon: 'tabler-video', size: '25' },
  },
  {
    title: 'الإعلانات',
    to: { name: 'director-announcements' },
    icon: { icon: 'tabler-ad-2', size: '25' },
  },
  {
    title: 'المهام',
    to: { name: 'director-assigned-tasks' },
    icon: { icon: 'tabler-list-check', size: '25' },
  },
  {
    title: 'أعمال إدارة الفرع',
    to: { name: 'director-branch-visits' },
    icon: { icon: 'tabler-building-community', size: '25' },
  },
  {
    title: 'مهام اللجنة',
    to: { name: 'director-committee-tasks' },
    icon: { icon: 'tabler-checklist', size: '25' },
  },
  {
    title: 'ملاحظات التطبيقات',
    to: { name: 'director-notes' },
    icon: { icon: 'tabler-notes', size: '25' },
  },
  {
    title: 'التنازلات',
    to: { name: 'director-waivers' },
    icon: { icon: 'tabler-receipt-refund', size: '25' },
  },
  {
    title: 'المستخدمين',
    icon: { icon: 'tabler-users', size: '25' },
    children: [
      {
        title: 'جميع المستخدمين',
        to: { name: 'users-list' },
        icon: { icon: 'tabler-user', size: '25' },
      },
      {
        title: 'نشاطات المستخدمين',
        to: { name: 'user-active' },
        icon: { icon: 'tabler-activity', size: '25' },
      },
    ],
  },
  {
    title: 'المخازن',
    to: { name: 'warehouses-list' },
    icon: { icon: 'tabler-building-warehouse', size: '25' },
  },
  {
    title: 'العناصر',
    to: { name: 'items-list' },
    icon: { icon: 'tabler-box', size: '25' },
  },
  {
    title: 'الموردين',
    to: { name: 'suppliers-list' },
    icon: { icon: 'tabler-truck-delivery', size: '25' },
  },
  {
    title: 'المشتريات',
    to: { name: 'purchases-list' },
    icon: { icon: 'tabler-shopping-cart', size: '25' },
  },
  {
    title: 'العملاء',
    icon: { icon: 'tabler-users', size: '25' },
    children: [
      {
        title: 'جميع العملاء',
        to: { name: 'customers-list' },
        icon: { icon: 'tabler-user-circle', size: '25' },
      },
      {
        title: 'أرشيف المصفرين',
        to: { name: 'customers-archived' },
        icon: { icon: 'tabler-archive', size: '25' },
      },
      {
        title: 'المتوقفين حسب التاريخ',
        to: { name: 'customers-stopped-date' },
        icon: { icon: 'tabler-calendar-time', size: '25' },
      },
      {
        title: 'الزبائن المتدنية',
        to: { name: 'customers-declining' },
        icon: { icon: 'tabler-trending-down', size: '25' },
      },
      {
        title: 'بحث القانونية',
        to: { name: 'accountant-legal-search' },
        icon: { icon: 'tabler-file-search', size: '25' },
      },
    ],
  },
  {
    title: 'المتابعة اليومية',
    to: { name: 'daily-followup' },
    icon: { icon: 'tabler-calendar-stats', size: '25' },
  },
  {
    title: 'لوحة الأسعار',
    to: { name: 'price-board' },
    icon: { icon: 'tabler-tag', size: '25' },
  },
  {
    title: 'أسماء قانونية',
    icon: { icon: 'tabler-gavel', size: '25' },
    children: [
      {
        title: 'أسماء قانونية',
        to: { name: 'decision-board' },
        icon: { icon: 'tabler-gavel', size: '22' },
      },
      {
        title: 'طلبات التنازل',
        to: { name: 'waiver-accountant-requests' },
        icon: { icon: 'tabler-cash', size: '22' },
      },
      {
        title: 'سجل القرارات',
        to: { name: 'decisions-list' },
        icon: { icon: 'tabler-list-check', size: '22' },
      },
    ],
  },
  {
    title: 'المبيعات',
    to: { name: 'sales-list' },
    icon: { icon: 'tabler-cash', size: '25' },
  },
  {
    title: 'إدارة المبيعات',
    icon: { icon: 'tabler-map-pin', size: '25' },
    children: [
      { title: 'نظرة عامة', to: { name: 'sales-manager-dashboard' }, icon: { icon: 'tabler-layout-dashboard', size: '22' } },
      { title: 'موظفين المبيعات', to: { name: 'sales-manager-employees' }, icon: { icon: 'tabler-users', size: '22' } },
      { title: 'الموقع المباشر', to: { name: 'sales-manager-map' }, icon: { icon: 'tabler-map', size: '22' } },
      { title: 'المسارات', to: { name: 'sales-manager-routes' }, icon: { icon: 'tabler-route', size: '22' } },
      { title: 'المبيعات', to: { name: 'sales-manager-sales' }, icon: { icon: 'tabler-receipt', size: '22' } },
      { title: 'ملخص المبيعات', to: { name: 'sales-manager-sales-summary' }, icon: { icon: 'tabler-chart-bar', size: '22' } },
      { title: 'القانونية', to: { name: 'sales-manager-legal-followups' }, icon: { icon: 'tabler-gavel', size: '22' } },
      { title: 'طلبات البيع', to: { name: 'sales-manager-requests' }, icon: { icon: 'tabler-send', size: '22' } },
    ],
  },
  {
    title: 'التسديدات',
    icon: { icon: 'tabler-credit-card', size: '25' },
    children: [
      {
        title: 'التسديدات الكلية',
        to: { name: 'payments-total' },
        icon: { icon: 'tabler-currency-dollar', size: '25' },
      },
      {
        title: 'التسديدات قبل أسبوع',
        to: { name: 'payments-week' },
        icon: { icon: 'tabler-calendar-week', size: '25' },
      },
      {
        title: 'التسديدات قبل شهر',
        to: { name: 'payments-month' },
        icon: { icon: 'tabler-calendar-month', size: '25' },
      },
      {
        title: 'متابعة حسب التاريخ',
        to: { name: 'payments-date-tracking' },
        icon: { icon: 'tabler-calendar-search', size: '25' },
      },
      {
        title: 'طلبات التسديدات',
        to: { name: 'request-receipt' },
        icon: { icon: 'tabler-currency-dollar', size: '25' },
      },
    ],
  },
  {
    title: 'المندوبين',
    icon: { icon: 'tabler-user', size: '25' },
    children: [
      {
        title: 'جميع المندوبين',
        to: { name: 'agents-list' },
        icon: { icon: 'tabler-users-group', size: '25' },
      },
      {
        title: 'جباية من قبل المتابعين',
        to: { name: 'follower-collection-lists' },
        icon: { icon: 'tabler-cash', size: '25' },
      },
      {
        title: 'إحصائيات  مع المصفرين',
        to: { name: 'agents-statistics' },
        icon: { icon: 'tabler-chart-bar', size: '25' },
      },
      {
        title: 'إحصائيات  بدون المصفرين',
        to: { name: 'no-statistics' },
        icon: { icon: 'tabler-chart-bar', size: '25' },
      },
    ],
  },
  {
    title: 'الحسابات',
    icon: { icon: 'tabler-wallet', size: '25' },
    children: [
      {
        title: 'الخزائن النقدية',
        to: { name: 'cash-registers' },
        icon: { icon: 'tabler-cash', size: '25' },
      },
      {
        title: 'الإضافات إلى الخزائن',
        to: { name: 'cash-deposits' },
        icon: { icon: 'tabler-cash-banknote', size: '25' },
      },
      {
        title: 'السحوبات من الخزائن',
        to: { name: 'cash-withdrawals' },
        icon: { icon: 'tabler-wallet-off', size: '25' },
      },
      {
        title: 'النقل بين الخزائن',
        to: { name: 'cash-transfers' },
        icon: { icon: 'tabler-arrows-exchange', size: '25' },
      },
    ],
  },
  {
    title: 'النسخ الاحتياطي',
    to: { name: 'backup-database' },
    icon: { icon: 'tabler-database', size: '25' },
  },
]
