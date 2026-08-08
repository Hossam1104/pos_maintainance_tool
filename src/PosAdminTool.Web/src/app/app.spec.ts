import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { App } from './app';
import { routes } from './app.routes';

describe('App shell', () => {
  it('renders named navigation and non-colour agent status', async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter(routes)] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    const shell = fixture.nativeElement as HTMLElement;
    expect(shell.querySelector('[aria-label="Primary navigation"]')?.textContent).toContain('Overview');
    expect(shell.querySelector('.unreachable-banner')?.textContent).toContain('Agent unreachable');
  });

  it('keeps shell controls in a keyboard focusable order', async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideRouter(routes)] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const controls = [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('a, button')];
    expect(controls.length).toBeGreaterThan(4);
    expect(controls.every((control) => !control.hasAttribute('tabindex') || control.tabIndex >= 0)).toBe(true);
  });
});
