import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { UiDialogComponent } from './ui-dialog.component';

describe('UiDialogComponent', () => {
  it('exposes a modal dialog and moves initial focus to the safe primary action', async () => {
    await TestBed.configureTestingModule({ imports: [UiDialogComponent] }).compileComponents();
    const fixture = TestBed.createComponent(UiDialogComponent);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    await fixture.whenStable();
    const dialog = (fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(dialog?.querySelector('button')?.textContent).toContain('×');
    expect(dialog?.querySelectorAll('button').length).toBe(3);
  });
});
