# GUI Review - R1.5.5

This review compares the real R1.5.4 Windows screenshot with `APPROVED-GUI-REFERENCE.png`.

## Visible issues corrected

1. **The window was too tall and vertically loose.**
   - Reduced default height from 820 to 760.
   - Reduced minimum height from 720 to 680.
   - Reduced the empty queue height from 190 to 165.
   - Reduced the converter drop-zone height so the main content is denser and closer to the approved proportions.

2. **The MP3 bitrate row clipped the 128 kbps option.**
   - Segmented controls now use a one-row `UniformGrid`, so all MP4/MKV/MP3 and bitrate choices share the available width without clipping.

3. **The URL link/paste icons looked like broken placeholder glyphs.**
   - Replaced them with vector geometry icons.

4. **The open-folder action looked like a generic square glyph.**
   - Replaced it with a vector folder icon.
   - Added a folder icon to the Save Location strip.

5. **The empty Quality control looked broken because it was just blank.**
   - Added an explicit `Analyze first` placeholder.
   - Quality selection stays disabled until analysis succeeds.

6. **Availability badges and duration were visible before analysis.**
   - They now appear only after real media metadata has been successfully analyzed.

7. **The Analyze button looked inactive before a URL was pasted.**
   - Analyze remains visually available while idle.
   - Clicking with an invalid/empty URL now gives a clear in-app validation message instead of presenting a dead-looking button.

8. **The right Convert card was slightly too wide compared with the approved composition.**
   - Reduced the converter column width to give the main media/download area more room.

9. **The queue header showed a permanent `Ready` status that was not in the approved design.**
   - The idle status is now empty; meaningful progress/errors still appear when needed.

10. **Primary disabled actions became too visually dead.**
    - Increased disabled primary-button visibility while still keeping the state obviously unavailable.

## Still intentionally deferred

- Pause/resume and per-item cancel buttons are not added decoratively until the queue engine supports them correctly.
- Audio source selection remains informational until there is a real selectable audio-track workflow.
- Thumbnail content remains empty before URL analysis rather than showing fake sample media.
