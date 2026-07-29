import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { SignalPathComponent } from './signal-path.component';
import { SignalNode } from './ui-fixtures';

const states: SignalNode[] = (['loading', 'ready', 'degraded', 'unreachable', 'stale', 'unknown'] as const).map((status) => ({ label: status, status, evidence: `${status} evidence`, checkedAt: '14:32:08 UTC', route: '/device' }));

describe('SignalPathComponent', () => {
  it('makes every status node a named keyboard route with evidence and time', async () => {
    await TestBed.configureTestingModule({ imports: [SignalPathComponent], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(SignalPathComponent);
    fixture.componentRef.setInput('nodes', states);
    fixture.detectChanges();
    const nodes = [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.signal-node')];
    expect(nodes).toHaveLength(6);
    expect(nodes.every((node) => node.hasAttribute('href') && node.getAttribute('aria-label')?.includes('evidence'))).toBe(true);
    expect(nodes.map((node) => node.textContent).join(' ')).toContain('14:32:08 UTC');
  });
});
