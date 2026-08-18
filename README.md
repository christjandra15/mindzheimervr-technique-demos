# VR Cognitive-Assessment Environment — Technique Demos

Standalone, decontextualized demos of interaction, visualization, and setup
techniques built for **MindzheimerVR**, a VR-based cognitive assessment
research project (Meta Quest 3, Unity 6, XR Interaction Toolkit / OpenXR).

**About the source project:** MindzheimerVR itself was built under a
university research grant, and its full source is not publicly available —
the underlying assessment logic and data pipeline are IP-restricted under
the grant's terms. The scripts in this repo are clean, standalone
reimplementations of specific *techniques* from that project, rewritten to
remove anything specific to the clinical assessment design. They demonstrate
how the systems work architecturally, not the assessment itself.


---

## What's here

### `Scripts/Visualization/`
A live 3D skeleton viewer and a procedurally-built calibration UI, both
driven by real Quest 3 head/controller tracking data at runtime — no
imported art assets. Includes a scripted demo-playback mode for when no
headset is connected, so the same scene can present itself.

- `Portfolio3DTrackingViewer.cs` — world-space head/hand tracking
  visualization with a gaze ray, dual-camera (3rd-person + POV) setup, and
  analog trigger/grip pressure feedback on each hand.
- `PortfolioCalibrationUIBuilder.cs` + `PortfolioTrackerPoint.cs` +
  `PortfolioUITheme.cs` + `UIShapeFactory.cs` — a fully code-generated
  tracker-calibration screen (no Photoshop, no imported sprites).
- `PortfolioLiveXRPointer.cs` — drives the calibration screen's cursors
  from live headset/controller input instead of the scripted demo.

### `Scripts/Interaction/`
A generic snap-to-socket placement system: grab an object, carry it near a
compatible socket, release to snap into place — or watch it smoothly
return to its origin if no valid socket is nearby. Built on XR Interaction
Toolkit's `XRGrabInteractable`.

- `SnapGrabbableObject.cs` — grab/release/return-to-origin lifecycle.
- `SnapPlacementSocket.cs` — proximity highlighting, acceptance rules,
  correct/incorrect placement events.
- `ShapeMatchPlacementSocket.cs` — a subclass demonstrating a different
  detection strategy (box overlap instead of sphere overlap) without
  touching the base class.

### `Scripts/Utilities/`
Small, self-contained fixes for problems that come up often in seated VR
development.

- `XROriginHeightController.cs` — manual seated-height override with
  runtime keyboard adjustment, for testing without repeatedly re-donning a
  headset.
- `VRSmoothingFix.cs` — a one-shot URP quality/anti-aliasing configuration
  pass (MSAA, anisotropic filtering, per-camera SMAA, shadow settings).

---

## Setup

Each script is a standalone `MonoBehaviour` (or a small set of them) — drop
the relevant folder into a Unity 6 project with URP and XR Interaction
Toolkit installed, add the component(s) to a GameObject as described in
each file's header comment, and press Play. No scene files are included;
these are meant to be dropped into your own test scene.

## License

MIT — see [LICENSE](LICENSE). Use, adapt, and learn from any of this freely.
