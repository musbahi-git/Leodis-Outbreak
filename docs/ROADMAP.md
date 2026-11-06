# Roadmap

Ordered checkpoints taken from the project brief. Each one should end with something that can
be demonstrated. Do not let UI polish or architecture planning delay the next working build.

## 0. Foundations — done

- Unity 6000.6.8f1 project (URP, portrait, `com.musbahi.leodisoutbreak`), Git + GitHub.
- Unity Cloud project linked, Build Automation connected to GitHub (SSH deploy key).
- Android build target configured (debug keystore, APK). iOS target created but blocked on
  Apple credentials.

## 1. Minimal app on a phone — in progress

- [x] Title "Leodis: Outbreak", status "Ready", one button → "Started".
- [x] Rebuilt on UI Toolkit with a generated scene.
- [ ] First Android cloud build downloaded and launched on a device.
- [ ] Apple Developer Program enrolment (US$99/year; the student fee waiver does not cover
      individuals). Then: distribution certificate (.p12) + ad-hoc provisioning profile with the
      iPhone 17 Pro UDID, uploaded to Build Automation (never into Git or chat).
- [ ] First iOS cloud build installed on the iPhone.

## 2. Demo mode — implemented, needs device verification

- [x] Synthetic runners and hunters, periodic reveals, captures that convert runners to hunters,
      timer, end states.
- [x] Stylised 2D map with stale-reveal rings.
- [ ] Verify on Android and iPhone: frame rate, readability in sunlight, tap targets.

## 3. Map experiment

- Evaluate Cesium for Unity on a small area around the University of Leeds (Parkinson Building
  area as origin). Check actual coverage and quality before promising anything; a simplified
  stylised map is acceptable and may be preferable.
- Compare: Cesium 3D Tiles vs stylised extruded footprints (OpenStreetMap data) vs the current
  2D radar. Pick based on phone rendering performance and readability.
- Add Cesium only after milestone 1 is complete on the iPhone.

## 4. Location on iOS and Android — investigate early

- Foreground location with `Input.location` or a native plugin; measure update freshness and
  battery.
- Investigate what is possible when the screen is locked on iOS (background location
  permission, "Always" authorisation, limits imposed by the OS). Do not promise uninterrupted
  background tracking or exact update intervals.
- Measure reconnect behaviour after the app is suspended.

## 5. Backend and one complete small-group loop

- Choose backend technology (undecided). Requirements: owns hidden runner coordinates, applies
  reveal rules, supports 10–20 players first with a path to 250.
- `RemoteBackend : IGameBackend`. Lobby, roles, start, reveals, captures, end.
- Field test with a small group in a bounded campus area.

## 6. Scale and AR

- Load-test the backend with simulated clients toward 250 players.
- Optional AR mode (AR Foundation with ARKit/ARCore providers): hunters "blink through walls"
  using only data the backend is allowed to reveal. Baseline builds keep "Requires ARKit
  support" off.
