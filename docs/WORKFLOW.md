# Workflow

## Local

- Project path: `C:\Repositories\Leodis-Outbreak` (the only copy to use).
- Editor: Unity 6000.6.8f1 with iOS and Android Build Support.
- After pulling changes that touch scripts or `Assets/_Project/UI`, let Unity finish compiling,
  then run **Leodis > Setup > Rebuild Bootstrap Scene** if the scene or panel settings changed.

## Git

Stage explicit paths. Never `git add .` (a stray untracked file lives in the working copy) and
never force-push as a routine fix.

```powershell
git status --short
git add -A -- Assets ProjectSettings Packages docs README.md .gitignore LICENSES
git commit -m "Describe the change"
git push
```

`.gitignore` is the GitHub Unity template: `Library/`, `Temp/`, `Logs/`, `UserSettings/`,
IDE files and `/.vsconfig` are not tracked.

## Cloud builds (Unity Build Automation)

- Source: GitHub via manual Git SSH (`git@github.com:musbahi-git/Leodis-Outbreak.git`) with a
  read-only deploy key.
- Editor version in every target: 6000.6.8f1 (same as local).
- **Android-Development**: Windows builder, auto-generated debug keystore, APK. Trigger a build
  manually from the dashboard, download the APK, install on an Android device for a sanity check.
- **iOS-Development**: macOS builder, Xcode 26.5. Blocked until:
  1. Apple Developer Program membership (US$99/year; the fee waiver does not apply to individuals).
  2. A distribution certificate exported as `.p12` with password and an **ad-hoc** provisioning
     profile that includes the iPhone's UDID. Both are uploaded in the Build Automation
     credentials screen only.
- Build minutes are metered. Check the current free allowance and per-minute price in the Unity
  Cloud dashboard before enabling scheduled or auto builds.

## Testing checklist for a new build

1. App launches to the home screen: title, `Ready`, Start button.
2. Tap Start: status becomes `Started`, the HUD appears within a second.
3. Timer counts down, "Next reveal" counts to zero and resets, runner rings grow then snap back.
4. Captures appear in the event log and the runner count drops.
5. End session returns a result screen; Back to home resets to `Ready`.
6. Rotate the phone: the app stays portrait.
