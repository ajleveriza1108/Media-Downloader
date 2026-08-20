# GUI Review R1.5.9.2 - Viewport Root Repair

## Reported failure

At a short 935x485 viewport, the Media card showed Format, Quality, and Audio, but the Save/Browse/Open-folder/Download footer was below the visible area. The screenshot demonstrated that width reflow alone was insufficient; height pressure remained inside the Media card.

## Root cause

The Media card retained star/minimum-height rows and a flexible spacer. Compact mode also imposed a large minimum Media height. This allowed the download footer to be pushed below the initial viewport even though the outer workspace had a ScrollViewer.

## Repair

1. Give the outer panel ScrollViewer an explicit name and make it the authoritative viewport.
2. Remove forced compact `PanelsGrid` height.
3. Change Media content rows to Auto / gap / Auto / gap / Auto.
4. Compress thumbnail and summary at short and very-short heights.
5. Keep Queue and Convert stacked below Media with vertical scrolling.
6. Add a Windows GUI smoke at 935x485 that checks footer visibility, panel width, and scrollbar visibility.

## Acceptance rule

Compress first, reflow second, scroll when necessary, never clip.
