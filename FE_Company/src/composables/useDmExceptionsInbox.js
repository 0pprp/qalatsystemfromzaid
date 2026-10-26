/**
 * Shared inbox logic for طلبات الاستثناء / المبيعات الخاطئة
 * (client-side WrongSale filter as in Flutter ExceptionsScreen).
 */
import { computed, ref } from 'vue'
import {
  dmExceptionDecide,
  dmExceptionDetail,
  dmExceptionMarkRead,
  dmExceptions,
  isWrongSaleReason,
} from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

export function useDmExceptionsInbox({ wrongSaleMode = false } = {}) {
  const toast = useToast()
  const busy = ref(false)
  const items = ref([])
  const status = ref(wrongSaleMode ? '' : 'Pending')
  const wrongFilter = ref('unread') // unread | read | reviewed
  const detailOpen = ref(false)
  const detail = ref(null)
  const decideNote = ref('')
  const decideBusy = ref(false)

  function pick(obj, ...keys) {
    if (!obj) return undefined
    for (const k of keys) {
      if (obj[k] != null && obj[k] !== '')
        return obj[k]
    }
    return undefined
  }

  function isWrong(row) {
    return isWrongSaleReason(pick(row, 'reason', 'Reason'))
      || Boolean(pick(row, 'isWrongSale', 'IsWrongSale'))
  }

  function isUnread(row) {
    return !pick(row, 'readAtUtc', 'ReadAtUtc')
  }

  function isBmReviewed(row) {
    return Boolean(pick(row, 'branchManagerReviewedAtUtc', 'BranchManagerReviewedAtUtc')
      || pick(row, 'isBranchManagerReviewed', 'IsBranchManagerReviewed'))
  }

  const visible = computed(() => {
    let rows = items.value.filter(r => (wrongSaleMode ? isWrong(r) : !isWrong(r)))
    if (wrongSaleMode) {
      if (wrongFilter.value === 'unread')
        rows = rows.filter(isUnread)
      else if (wrongFilter.value === 'read')
        rows = rows.filter(r => !isUnread(r) && !isBmReviewed(r))
      else if (wrongFilter.value === 'reviewed')
        rows = rows.filter(isBmReviewed)
    }
    return rows
  })

  async function load() {
    busy.value = true
    try {
      const data = await dmExceptions({
        page: 1,
        pageSize: wrongSaleMode ? 100 : 50,
        ...(wrongSaleMode || !status.value ? {} : { status: status.value }),
      })
      items.value = pick(data, 'items', 'Items') || []
    }
    catch (e) {
      items.value = []
      toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل القائمة')
    }
    finally {
      busy.value = false
    }
  }

  async function openRow(row) {
    const id = pick(row, 'id', 'Id')
    if (!id) return
    try {
      detail.value = await dmExceptionDetail(id)
      detailOpen.value = true
      decideNote.value = ''
      if (isUnread(row)) {
        await dmExceptionMarkRead(id)
        await load()
      }
    }
    catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'تعذر فتح التفاصيل')
    }
  }

  async function decide(decision) {
    const req = pick(detail.value, 'request', 'Request') || detail.value
    const id = pick(req, 'id', 'Id')
    if (!id) return
    decideBusy.value = true
    try {
      await dmExceptionDecide(id, { decision, note: decideNote.value })
      toast.success(decision === 'Approved' ? 'تمت الموافقة' : 'تم الرفض')
      detailOpen.value = false
      await load()
    }
    catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'تعذر حفظ القرار')
    }
    finally {
      decideBusy.value = false
    }
  }

  return {
    busy,
    items,
    status,
    wrongFilter,
    visible,
    detailOpen,
    detail,
    decideNote,
    decideBusy,
    pick,
    load,
    openRow,
    decide,
  }
}
