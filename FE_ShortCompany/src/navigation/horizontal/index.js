export default [
  {
    title: 'الرئيسية',
    to: { name: 'root' },
    icon: { icon: 'tabler-home', size: '25' },
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
    ],
  },
  {
    title: 'المتابعة اليومية',
    to: { name: 'daily-followup' },
    icon: { icon: 'tabler-calendar-stats', size: '25' },
  },
  {
    title: 'القرارات',
    to: { name: 'decision-board' },
    icon: { icon: 'tabler-gavel', size: '25' },
  },
  {
    title: 'المبيعات',
    to: { name: 'sales-list' },
    icon: { icon: 'tabler-cash', size: '25' },
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
]
