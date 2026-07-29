import { describe, expect, it } from 'vitest';
import { signalStatus } from './overview-page.component';

describe('signalStatus', () => {
  it('derives healthy, degraded, unreachable, and unknown evidence states', () => {
    expect(signalStatus({ freshness: 'fresh', detail: 'SQL query completed' })).toBe('ready');
    expect(signalStatus({ freshness: 'stale', detail: 'Branch verification failed' })).toBe('degraded');
    expect(signalStatus({ freshness: 'stale', detail: 'TCP endpoint unreachable' })).toBe('unreachable');
    expect(signalStatus({ freshness: 'unknown', detail: 'Not configured' })).toBe('unknown');
  });
});
