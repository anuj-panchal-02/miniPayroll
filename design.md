# Design — miniPayroll

A locked design system for this app. Every page redesign reads this file before
emitting code. Do not regenerate per page — extend or amend this file when the
system needs to grow.

/* Hallmark · genre: modern-minimal · theme: Coral (accent retuned green) · design-system: design.md · designed-as-app
 * Hallmark · pre-emit critique: P5 H5 E4 S4 R5 V4
 */

## Genre
modern-minimal

## Macrostructure family
Pages within a family share the family's shape; they vary only in component archetypes.

- Marketing pages: none (this is an operations product, not a marketing site)
- App pages: Workbench — operational forms, ledgers, and workspace tools
- List pages: Index-First — companies and employees as scannable indexes
- Auth pages: Split Studio — sign-in and password change
- Wizard pages: Narrative Workflow — company setup and employee onboarding

## Theme
- `--color-paper`   oklch(97.2% 0.01 72)
- `--color-paper-2` oklch(94.6% 0.012 72)
- `--color-ink`     oklch(20% 0.016 48)
- `--color-ink-2`   oklch(32% 0.016 50)
- `--color-rule`    oklch(86% 0.012 70)
- `--color-accent`  oklch(58% 0.16 150)
- `--color-accent-soft` oklch(94% 0.04 150)
- `--color-focus`   oklch(48% 0.15 150)

## Typography
- Display: Geist, weight 600, style normal
- Body:    Geist, weight 400
- Mono:    Geist Mono, weight 500 (labels, chips, amounts)
- Display tracking: -0.03em
- Type scale anchor: `--text-xl` = 1.75rem

## Spacing
4-point named scale. The values are in `frontend/tokens.css`. Pages must use named
tokens (`var(--space-md)`), never raw values.

## Controls
- Button height: 44px (`--control-height`), 48px on coarse pointers
- Field well height: 44px (`--control-height-field`), 48px on coarse pointers — the same height as buttons, because the well holds only the value
- Input radius: 8px (`--radius-input`)
- Field wells: `--color-paper-2` fill, 1px border at a constant width in every state — only the border colour changes, so nothing reflows. Default, inactive, and success use `--color-rule`. Hover uses `--color-rule-2`. Focus and active use `--color-ink-2`. Never the green accent, never a halo. Error takes a single 1px `--color-error` border and always outranks hover and focus.
- Popovers (select listbox, date calendar): same outer width as the well, 4px below it, `--color-paper` fill, 1px `--color-rule` border, `--radius-input`, `--shadow-whisper`. Option text lines up with the well value. Selected option is `--color-paper-2` at weight 500. Keyboard/hover cursor is `--color-paper-3` fill — never an ink outline ring. Selected day is `--color-paper-3` with ink text. Never browser-blue or accent-green highlights.
- Success is never carried by the well border — the green strength meter or the green-toned helper slot below the well carries it, so the accent green and the success green never compete inside one control
- Labels sit above the well as real `<label htmlFor>` elements — never inside the well, never placeholders, never floating-away labels
- Helper, success, and error share one reserved slot below the well; error outranks success, which outranks hint
- Password fields render a strength meter between the well and the helper slot; it appears only once the field has a value
- Primary actions: ink fill, pill radius
- Secondary actions: paper fill, rule border, pill radius
- Focus rings: 2px `--color-focus`, instant, never animated
- Hover only inside `@media (hover: hover)`

## Motion
- Easings: `--ease-out`, `--ease-in`, `--ease-in-out`
- Reveal pattern: none on app pages
- Reduced-motion fallback: opacity-only, ≤ 150 ms

## Microinteractions stance
- Silent success when the result is visible; status text for drafts and async saves
- Hover delay 800 ms · focus delay 0 ms
- Validate on blur, then revalidate on change
- Never colour-only errors; pair with text and `aria-invalid`

## CTA voice
- Primary CTA: ink fill, pill, verb + object (`Save changes`, `Create company`)
- Secondary CTA: outlined pill (`Back`, `Save as draft`, `Cancel`)

## Per-page allowances
- Marketing pages MAY use enrichment (not applicable).
- App pages MUST NOT use enrichment — function carries the page.
- Content pages: typography only.

## What pages MUST share
- The wordmark / logotype.
- The accent colour used for focus, not as a flood fill.
- The display + body fonts.
- The CTA voice (button shape, border-radius, padding rhythm).
- Field well height, label-above-well placement, helper/error slot, and 44px touch targets.

## What pages MAY differ on
- Macrostructure within the page-type family.
- Grouping of fields to match the job (onboarding vs. payroll vs. admin).
- Search/filter toolbars on index pages.

## Exports

### tokens.css
See [`frontend/tokens.css`](frontend/tokens.css).
