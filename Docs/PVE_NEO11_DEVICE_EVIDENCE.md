# iQOO Neo11 High-quality validation — 2026-09-08

User direction: improve the actual 3D game as far as possible, then validate the
ordinary iQOO Neo11 at best quality. High is the primary visual/device gate;
the earlier one-tier-lower target is later compatibility work. Do not silently
reduce resolution/tier to claim the High gate passed.

## Connected hardware

Targeted read-only ADB checks identified one authorized USB device, current user
0, model `V2520A`, Android `16`, SoC `SM8750`. Its device serial is intentionally
not copied into this report. Official Neo11 specifications identify this model
and Snapdragon 8 Elite / Adreno 830.
[Official Neo11 specifications](https://www.vivo.com.cn/vivo/param/iqooneo11).

`wm size` reports **1080×2376**, despite the advertised 1440×3168 panel. This is
the phone's currently reported Android display configuration, not a measured
Unity render resolution. No display setting was changed. Actual screen/render
dimensions and High settings still need verification in the running app.

Pre-install battery: 87%, reported battery temperature 31.2°C, charging with
`AC powered: true` (ADB connection is USB). This is not ambient temperature,
SoC temperature, an unplugged run or a controlled thermal baseline.

## Build and attempted installation

Fresh artifact: `../outputs/SessionRpg-dev.apk`, 232,103,140 bytes, SHA256
`c6bb22fce90b68791d0f75db2f422270f3b6e3e8b22d0319fa1452c78ea3d700`.
The APK is ARM64, debuggable, package `com.sessionrpg.prototype`, version
0.1.0/code 1, launcher `com.unity3d.player.UnityPlayerGameActivity`.
See [desktop/build/source evidence](PVE_WOODLAND_POLISH_EVIDENCE.md).

An existing game was found, last updated 2026-09-03 15:12:17, version 0.1.0/code 1.
The scoped `adb -s DEVICE install --user 0 -r ../outputs/SessionRpg-dev.apk`
attempt preserves existing data; it **failed** with exit 1:
`INSTALL_FAILED_ABORTED: User rejected permissions`.
No uninstall, package-data clear, device reset, resolution change, security-setting
change or bypass was attempted. The phone-side installation approval must be
resolved by the user before retrying. Do not present the old installed game as
the current tested revision.

## Initial diagnostic readiness and limits (before corrections below)

Read-only inspection confirms the current Unity GameActivity accepts its `unity`
string intent extra and passes it to the player; the APK contains the opt-in
profile classes and no Android execution rejection. That is source evidence,
not proof that the managed arguments/report work on this phone. Prefer a fresh
app-private report path retrievable with the development package's `run-as`;
never overwrite existing profile data.

The initial profile report had a desktop-only limitations sentence inherited
from its first use. It must not be used to label Android evidence as desktop or
to claim a phone pass. Record actual `platform`, GPU/API, resolution, tier and
counter availability; correct that diagnostic labeling before treating it as a
general mobile reporting contract. Its current bounded stationary observation
is not sustained combat, a thermal soak or a physical-touch test.

## Retry and Android diagnostic corrections

The user authorized a retry. The same data-preserving installation subsequently
returned **Success**. Existing data was not cleared. A fresh activity launch
accepted the `unity` extra, streamed PvE and reached the authored capture camera.

The first PNG failed: Android appended the absolute requested internal-storage
path to `Application.persistentDataPath`, yielding a nonexistent duplicated path.
The runner timed out and closed itself. `EXIT_SELF 0` is not a successful capture;
the completion marker and actual artifact are required. The app-PID-filtered log
is `build/review/neo11-high-20260908/initial-capture-failed.log`.

Source changes are confined to opt-in development diagnostics and their tests:

- `SessionRpgReferenceCapture.cs`: Android validates that the absolute destination
  lies inside its persistent directory, submits a relative screenshot name, and
  polls the original absolute destination. Other platforms retain absolute paths.
  Invalid Android destinations fail before a scene capture starts. Seven new
  `SessionRpgReferenceCapturePathTests` cases cover exact path reconstruction,
  canonicalization, prefix lookalikes, outside/relative paths and desktop behavior.
  [Unity screenshot-path contract](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html).
- `SessionRpgProfileCapture.cs` / options / tests: schema3 records actual platform
  and backend; `ENABLE_IL2CPP` bypasses unsupported
  `GC.GetAllocatedBytesForCurrentThread` and returns **unavailable (-1)**.
  The pinned native runtime asserts/returns zero rather than throwing the managed
  exception the previous observer caught. The unsafe profile was not launched
  on the phone. Five additional cases verify unavailable summaries and labeling.

No Core, Application, input, camera, quality/shadow/resolution, generated asset,
package, progression or network source changed. No authoring rebuild is required
for these inert runtime diagnostic corrections.
Final SHA256 comparison against the 1,788-file entry inventory confirms **zero
generated/ProjectSettings files changed, zero added and zero removed**. Source
and documentation whitespace checks passed.

Fresh commands:

- `Tools/Agent/unity.sh doctor`: passed, pinned 6000.5.6f1, Editor closed.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all`: passed,
  **1,016/1,016 EditMode and 251/251 PlayMode**, zero failed/inconclusive/skipped.
  XML under `../outputs/agent/20260908-142439-test-edit-4459/results.xml` and
  `20260908-142531-test-play-4459/results.xml`.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-android`:
  passed (`../outputs/agent/20260908-142730-build-android-5687/unity.log`).
  Corrected APK: **232,126,236 bytes**, SHA256
  `0574480280a57e95bf8e18022deb121e82b4766910bb8f1572eef40d1e91cbd7`.
  Inspecting freshly generated `SessionRpg.Unity__4.cpp` confirmed the IL2CPP
  allocation observer returns -1 directly; it makes no unsupported GC call.
- Final `validate`: passed (`20260908-142927-validate-6778/unity.log`).
- Corrected APK installation attempt could not start: after the build restarted
  ADB, the device reported **unauthorized**. The user was asked to approve the
  phone's USB-debugging prompt. No key changes, root, security bypass or data
  clear was attempted. Corrected capture/profile remains pending authorization.

The user then confirmed authorization. `get-state` returned `device`; installing
the corrected **057448…** APK returned **Success**. A new capture activity launch
returned `Status: ok`, `LaunchState: COLD`, and a game PID. Its fresh destination
was the app-owned external persistent path
`/storage/emulated/0/Android/data/com.sessionrpg.prototype/files/codex-review/iqoo-high-pve-discipline-02.png`.

Before retrieval, the phone disconnected: targeted APK checksum and PNG pull
commands returned `device not found`, and a fresh `adb devices -l` listed no
devices. The waiting app-log command was interrupted. Neither PNG nor completion
log was retrieved; do not infer capture success from activity launch alone. No
corrected live profile has been launched. Installation succeeded, but independent
on-device APK hashing remains unverified because of the disconnect.

### Meaning of High in this build

Source/asset review confirms High is the game's highest **URP preset**: requested
scale1.0, 4×MSAA, HDR lighting buffer, 4096 shadow atlas, 40m shadow distance,
two cascades, soft main-light shadows, four per-pixel additional lights and 60fps.
No app-level resolution/automatic tier downgrade was found; camera dynamic
resolution and adaptive performance are disabled.

This does not mean every Unity/phone setting is maximized. Android's separate
global Unity quality remains index2/Medium (LOD bias0.7), versus desktop
index5/Ultra (LOD bias2). Authored characters explicitly request four skin weights.
URP can reduce MSAA according to GPU sample/store-and-resolve support. Actual
render-target sample count/format is not recorded by the current profiler; HDR
lighting is not HDR display output. `qualityTier=High` is the selected tier, not
alone proof of applied pipeline state/every effective target property. Verify
pixels, catalog/application errors, resolution and telemetry before acceptance.
These are findings, not settings changed by the diagnostic fix.

### Startup log classification

The first log includes a caught `AssetPackManager` class-not-found probe. Pinned
Unity player bytecode catches this reflection failure and exposes unavailable
Play Asset Delivery. The current standalone APK embeds assets, has no PAD modules
or game-source PAD calls and continues through scene loading. This is not evidence
that a required game dependency caused the capture failure. Do not add a package
merely to silence the probe; actual PAD operations would require that capability.
[Unity PAD scope](https://docs.unity3d.com/6000.0/Documentation/Manual/play-asset-delivery.html).

Shutdown `MemoryLeaks` reported 1,652,075 B after quit, plus a debugger listen
message. Swappy's loader also logged missing `libgame.so`, although GameActivity
had loaded it earlier. Their causes and impact are not established by aggregate
logs. They remain separate from the proven screenshot destination defect; no
clean-shutdown or complete performance/stability pass is claimed.

## Current status

- Device connection and targeted identity checks: **passed**.
- Initial APK install: **passed on user-authorized retry**.
- Corrected diagnostic APK install: **passed after reauthorization**.
- Corrected activity launch: **passed**; corrected PNG retrieval/completion:
  **blocked by phone disconnect**. Initial failed export is historical above.
- Actual pixels / exact effective High configuration: **not yet verified**.
- Physical CPU/GPU/frame pacing, allocation/memory, thermal soak: **not run**.
- Human mobile movement/flicks/readability: **not run**.
- Network/co-op: **not run**.
- Production/art/device acceptance: **not passed**.

Next safe action: reconnect and keep the phone attached/unlocked throughout the
validation. Verify the installed APK and retrieve the attempted capture/log (or
make a fresh capture if necessary), then run the safe bounded profile. Do not
reinstall again unless the installed binary differs or a new fix is needed.
