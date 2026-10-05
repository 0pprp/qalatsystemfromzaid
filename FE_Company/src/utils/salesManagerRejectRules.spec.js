import { describe, expect, it } from 'vitest'
import { canManagerRejectRequest } from '../src/utils/salesManagerRejectRules.js'

describe('canManagerRejectRequest', () => {
  it('allows reject for sent-tab follower New', () => {
    expect(canManagerRejectRequest({}, {
      status: 'New',
      isEmployeeSubmitted: false,
      isFollowerSubmitted: true,
      isDelegateSubmitted: false,
    })).toBe(true)
  })

  it('allows reject for sent-tab delegate New', () => {
    expect(canManagerRejectRequest({}, {
      status: 'New',
      isEmployeeSubmitted: false,
      isFollowerSubmitted: false,
      isDelegateSubmitted: true,
    })).toBe(true)
  })

  it('allows reject for employee submitted New', () => {
    expect(canManagerRejectRequest({}, {
      status: 'New',
      isEmployeeSubmitted: true,
      isFollowerSubmitted: false,
      isDelegateSubmitted: false,
    })).toBe(true)
  })

  it('blocks Completed and Rejected', () => {
    expect(canManagerRejectRequest({}, {
      status: 'Completed',
      isEmployeeSubmitted: true,
      isFollowerSubmitted: false,
      isDelegateSubmitted: false,
    })).toBe(false)
    expect(canManagerRejectRequest({}, {
      status: 'Rejected',
      isEmployeeSubmitted: true,
      isFollowerSubmitted: false,
      isDelegateSubmitted: false,
    })).toBe(false)
  })
})
