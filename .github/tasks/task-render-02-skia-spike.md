# Task 02: Pin Skia 2.88 layer behaviour with tests

Source: summary §5.1, §5.2; proposal §8 experiments. Depends on: none. Unblocks: 03.

## Goal

Settle the Skia facts task 03 depends on, as tests that stay in the suite. If an update of VL.Core.Skia (and with it SkiaSharp) changes the behaviour, they fail.

## Questions, one test each

1. **Bounded layer with a blur.** `SaveLayer(nodeBox, blurPaint)` around content filling the box: does the blur spill outside the box, or is it cut at the box edge?
2. **Unbounded layer under a clip.** `SaveLayer(blurPaint)` inside a `ClipRect`: is the blur cut by the clip, as the clip of an ancestor with Overflow Hidden would cut it?
3. **Scaled CTM.** Under `Scale(2)`, is the blur sigma doubled in pixels, and is the drop shadow offset doubled?
4. **Rotated CTM.** Under a rotation, does a drop shadow offset rotate with the node, or stay in device space?
5. **Group opacity.** Two overlapping opaque children inside `SaveLayer` with paint alpha 50%: is the overlap the same as the rest, unlike drawing each child at 50%?
6. **Opacity and clip together.** `SaveLayer(alphaPaint)` followed by `ClipRect`: is the content clipped and faded, and restored by one `RestoreToCount`?
7. **Paint order on one layer.** A layer paint with both a color filter and an image filter: which applies first? This decides whether task 03 can use one layer for both.

## Where

`VL.Fu.Tests\Core\Rendering\SkiaLayerBehaviourTests.cs`. These tests use plain SkiaSharp on raster `SKBitmap`s, with no Fu nodes. Each test documents the behaviour it asserts, so a failure points at the changed fact.

## Output

* Results written into the summary §5 and the proposal §4.3, replacing the open claims.
* A bounds policy for task 03. The expected outcome is unbounded `SaveLayer(paint)` when an image filter is set, and the clip area or node box otherwise, but the tests decide.

## Out of scope

GPU performance. A layer allocates an offscreen target per frame; measure that by hand in vvvv with many faded nodes once task 03 exists.
