import { describe, expect, it } from 'vitest';

const pairs = [
  ['#142130', '#f3f6f8'], ['#2457d6', '#ffffff'], ['#006f69', '#ffffff'], ['#8a4f00', '#ffffff'], ['#a62137', '#ffffff'],
  ['#f3f7fa', '#0e1722'], ['#7ea3ff', '#152231'], ['#42c8b8', '#152231'], ['#ffc166', '#152231'], ['#ff7185', '#152231'],
] as const;

function luminance(hex: string): number { const values = hex.slice(1).match(/.{2}/g)?.map((part) => Number.parseInt(part, 16) / 255) ?? []; const linear = values.map((value) => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4); return .2126 * linear[0] + .7152 * linear[1] + .0722 * linear[2]; }
function contrast(foreground: string, background: string): number { const [light, dark] = [luminance(foreground), luminance(background)].sort((a, b) => b - a); return (light + .05) / (dark + .05); }

describe('semantic token contrast', () => { it('keeps text/action semantic pairs at WCAG AA contrast', () => { for (const [foreground, background] of pairs) { expect(contrast(foreground, background), `${foreground} on ${background}`).toBeGreaterThanOrEqual(4.5); } }); });
