import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { resolveLastStatusAtUtc } from './salesRequestLastStatus.js'

describe('resolveLastStatusAtUtc', () => {
  it('prefers LastStatusAtUtc from API', () => {
    const at = '2026-10-25T10:00:00Z'
    assert.equal(
      resolveLastStatusAtUtc({
        status: 'Pending',
        LastStatusAtUtc: at,
        createdAtUtc: '2026-10-20T08:00:00Z',
      }, 'Pending'),
      at,
    )
  })

  it('uses CompletedAtUtc for Completed', () => {
    assert.equal(
      resolveLastStatusAtUtc({
        Status: 'Completed',
        CreatedAtUtc: '2026-10-20T08:00:00Z',
        CompletedAtUtc: '2026-10-24T14:00:00Z',
      }, 'Completed'),
      '2026-10-24T14:00:00Z',
    )
  })

  it('uses RejectedAtUtc for Rejected', () => {
    assert.equal(
      resolveLastStatusAtUtc({
        status: 'Rejected',
        createdAtUtc: '2026-10-20T08:00:00Z',
        rejectedAtUtc: '2026-10-22T09:00:00Z',
      }, 'Rejected'),
      '2026-10-22T09:00:00Z',
    )
  })
})
