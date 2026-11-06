# Decisions

Short log of decisions, with reasons, so they are not re-argued later. "Requirement" marks
something fixed by the project brief; "Decision" marks a choice that could be revisited.

| # | Item | Type | Detail |
| --- | --- | --- | --- |
| 1 | Unity 6000.6.8f1 (6.6) | Decision (user) | Chosen over the LTS stream by the project owner. Same version is pinned in Build Automation. |
| 2 | URP, portrait, `com.musbahi.leodisoutbreak`, product name `Leodis: Outbreak` | Requirement | From the brief. Checked by `Leodis > Setup > Validate Project Settings`. |
| 3 | iOS and Android both targeted | Decision | iOS is the primary test device (iPhone 17 Pro); Android is used to get a first cloud build running while Apple enrolment is pending. |
| 4 | Build Automation, no Mac | Requirement | Cloud-compiled and signed. Signing material is uploaded in the Unity Cloud dashboard only, never in Git or chat. |
| 5 | Input System only (`activeInputHandler: 1`) | Decision | Template default; UI Toolkit works with it through `InputSystemUIInputModule`. |
| 6 | UI Toolkit instead of uGUI | Decision | Layout and styling are plain text (UXML/USS), reviewable in Git and authorable without Inspector work. Replaced the first uGUI/TextMeshPro screen. |
| 7 | Source Serif 4 as the display font | Decision | University of Leeds uses Freight Text/Sans Pro, which is commercial. Source Serif 4 (SIL OFL) is a close free serif. Licence in `LICENSES/`. |
| 8 | Scene generated from code | Decision | `BootstrapSceneBuilder` builds the scene so set-up is reproducible and nothing depends on manual wiring. |
| 9 | Backend technology undecided; `IGameBackend` seam | Requirement + Decision | Brief forbids phone-as-server, Netcode, Relay, Unity multiplayer services or a dedicated Unity server "just because". The interface lets the demo run now and the real client slot in later. |
| 10 | Runner positions leave the backend only as reveals | Requirement | Hidden coordinates are protected server-side. The demo backend models the same boundary. |
| 11 | Stylised 2D radar before any 3D map | Decision | Cesium evaluation is postponed until the app runs on the iPhone, as the brief requires. |
| 12 | AR as an optional later mode | Decision (user) | AR Foundation (ARKit/ARCore) after the core loop works; "Requires ARKit support" stays off for baseline builds so the app installs on all devices. |
| 13 | Android: debug keystore, APK | Decision | Fastest route to a first on-device build. A release keystore is needed before any store upload. |
| 14 | Never `git add .` in this repo | Decision | A stray untracked file (`trap screen`) exists in the working copy; stage explicit paths. |
