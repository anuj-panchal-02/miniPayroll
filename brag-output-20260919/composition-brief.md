# Hyperframes Composition Brief: miniPayroll

## Objective
Create a short launch-style brag video for miniPayroll.

## Output
- Composition directory: `brag-output-20260919/composition/`
- Rendered video: `brag-output-20260919/brag.mp4`
- Format: landscape — 1920x1080
- Duration: 18 seconds

## Source Material
- Project root: `c:\Users\Anuj\Desktop\miniPayroll`
- Primary files read: `frontend/app/page.tsx`, `frontend/app/globals.css`, `frontend/tokens.css`, `README.md`
- Product name: miniPayroll
- Tagline / strongest claim: Payroll suites got too large. This product only runs the month.
- Key UI or visual moment to recreate: The clean "Monthly Inputs" table or the 4-step process cards.
- Copy that must appear verbatim:
  - "Payroll suites got too large"
  - "HR, leave, tax, and modules nobody opens."
  - "Attendance in. Figures out. Payslips in hand."
  - "The month, in four steps"
  - "Payroll without the suite."

## Creative Direction
- Tone preset: app-store
- Creative direction: smooth, minimal SaaS presentation focusing on cutting the bloat.
- Interpretation: Pacing is clean and unhurried. Feature cards reveal smoothly. No chaotic motion, just professional restraint.
- Angle: The product's strength is its simplicity and narrow focus. The video will mock the complexity of modern HR suites and highlight how fast and clean it is to just enter attendance and get payslips.
- Hook: The phrase "Payroll suites got too large" centered on screen.
- Outro / punchline: "Payroll without the suite. miniPayroll."
- Avoid:
  - Generic SaaS language
  - Abstract filler visuals
  - Unrelated visual redesign

## Visual Identity
- Background: `oklch(99% 0.005 260)`
- Text: `oklch(18% 0.01 260)`
- Accent: `oklch(62% 0.2 150)`
- Display font: Geist Sans
- Body font: Geist Sans
- Visual references from the project: 4-step showcase tab layout.

## Storyboard
Use the storyboard in `brag-output-20260919/brag-plan.md` as the creative contract.

Scene summary:
1. The Hook — 4s — Text "Payroll suites got too large" centered. "too large" highlights in green accent color.
2. The Bloat — 3s — Text "HR, leave, tax, and modules nobody opens." gets crossed out. "Attendance in. Figures out. Payslips in hand." appears.
3. The Process — 7s — "The month, in four steps" at the top. 4 feature cards slide in sequentially.
4. The Outro — 4s — "Payroll without the suite." with miniPayroll brand logo.

## Audio
- Audio role: warm bed with sparse professional accents
- Audio arc: clean, professional audio bed with satisfying, sparse interaction sounds for text and card reveals.
- Music: `happy-beats-business-moves-vol-1-by-ende-dot-app.mp3`
- Music treatment: start at 0s, fade out smoothly at 18s
- Music cue guidance: 1-3 strong cues for the feature card reveals; beat-grid window for the 4 steps appearing. Detect at composition via `hyperframes beats` or bundle.
- Audio-reactive treatment: subtle; music RMS gently pulses the UI cards' shadow.
- Audio-coupled moments:
  - Scene 1 — typing / beat reveal
  - Scene 3 — card sequence, soft click for each
- SFX selection guidance: use clean UI pops, ticks, and soft paper sliding sounds. 
- SFX analysis guidance: use lower high-frequency-risk sounds for repeated or polished moments
- Exact SFX choice: Hyperframes should choose filenames, timestamps, density, and volume based on the implemented animation.
- Audio files: copy the chosen music and any Hyperframes-selected SFX into `brag-output-20260919/composition/assets/`

## Hyperframes Instructions
Load the composition-building Hyperframes domain skills — `hyperframes-core` (composition contract + `data-*` timing), `hyperframes-animation` (motion), `hyperframes-creative` (design spec, beats, audio-reactive), `hyperframes-keyframes` (seek-safe keyframes), and `hyperframes-cli` (lint/check/render). /brag is its own workflow: do not enter the `hyperframes` entry-point intent interview and do not route into its generic promo / launch-video workflow. Prefer native Hyperframes conventions over anything in `/brag`.

Requirements:
- Show at least one real UI, copy, or visual element from the source project.
- Keep all text readable in the final render.
- Keep the video within 15-25 seconds.
- Include the planned music/SFX layer unless audio was explicitly disabled or documented as intentionally silent.
- Treat `/brag` audio notes as guidance, not a fixed cue sheet. Choose SFX after the visual animation exists.
- Treat music cue metadata as optional timing hints. Hyperframes decides exact animation timing and should ignore cues that hurt readability, scene pacing, or the product story.
- Major reveals may move toward nearby strong cues within about 0.15s. Smaller entrances may align to nearby beat points within about 0.10s. Use only 1-3 strong cue locks in a 15-25s video unless the edit clearly benefits from more.
- Use SFX to support motion and interaction: card sounds for card-like reveals, short announcement cues for major payoffs, key/click sounds for text or user actions, and restraint when the edit is already busy.
- Honor planned music treatment such as fade-outs, ducking, beat-aligned reveals, or letting a final SFX ring over the music, using the best Hyperframes-supported implementation.
- When music is present and the treatment is not `none`, consider Hyperframes audio-reactive workflow: extract audio data and use RMS/frequency bands for subtle, brand-specific motion. Good targets are glow, depth, background warmth, card presence, title emphasis, or other existing visual elements. Avoid waveform/equalizer visuals, musical-note graphics, generic particle systems, strobing, or heavy pulsing.
- Use local assets for audio and any required runtime/media dependencies when possible.
- Run `hyperframes check` before render — it is brag's single gate.
