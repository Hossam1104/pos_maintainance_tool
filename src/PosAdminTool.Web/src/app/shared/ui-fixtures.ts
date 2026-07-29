export type SignalStatus = 'loading' | 'ready' | 'degraded' | 'unreachable' | 'stale' | 'unknown';

export interface SignalNode { label: string; status: SignalStatus; evidence: string; checkedAt: string; route: string; }

export const branchSignalFixture: readonly SignalNode[] = [
  { label: 'This device', status: 'ready', evidence: 'P087 / POS 03 identity confirmed', checkedAt: '14:32:08 UTC', route: '/device' },
  { label: 'RMS services', status: 'degraded', evidence: '2 of 3 required services are running', checkedAt: '14:31:56 UTC', route: '/services' },
  { label: 'Local SQL', status: 'ready', evidence: 'Local SQL health check completed', checkedAt: '14:31:49 UTC', route: '/device' },
  { label: 'Main server', status: 'unreachable', evidence: 'Agent cannot reach the main-server diagnostic endpoint', checkedAt: '14:32:08 UTC', route: '/downloads' },
];

export const navigationItems = [
  { path: '/', label: 'Overview', shortLabel: 'Home', glyph: '⌂' }, { path: '/device', label: 'Device', shortLabel: 'Device', glyph: '◇' },
  { path: '/services', label: 'Services', shortLabel: 'Services', glyph: '≡' }, { path: '/backups', label: 'Backups', shortLabel: 'Ops', glyph: '↥' },
  { path: '/restore', label: 'Restore', shortLabel: 'More', glyph: '↧' }, { path: '/maintenance', label: 'Maintenance', shortLabel: 'More', glyph: '□' },
  { path: '/downloads', label: 'Downloads', shortLabel: 'More', glyph: '⇣' }, { path: '/activity', label: 'Activity', shortLabel: 'More', glyph: '◫' }, { path: '/settings', label: 'Settings', shortLabel: 'More', glyph: '≐' },
] as const;
