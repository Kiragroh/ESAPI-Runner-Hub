# Final verification checklist

Use this checklist for the public v0.1.0 tag and the synchronized internal deployment.

- [x] Requirements represented in source and README
- [x] Application logo embedded in executable, taskbar, window, and header
- [x] Settings editable in the GUI and stored in portable `settings.ini`
- [x] Fast detached patient search with no retained ESAPI objects
- [x] Patient context optional per application and transferable by argument or child environment
- [x] Patient-independent applications supported
- [x] Child applications isolated from the Hub process
- [x] Missing local and optional network paths handled per application
- [x] Privacy-safe technical log and crash report
- [x] Offline synthetic UI mode available
- [x] Public package contains no vendor assemblies
- [x] Automated release build and tests run from the final source tree
- [x] Shared UNC checkout and direct executable built from `main`
- [x] Dedicated STR-Hub InHouse entry visible under `Eclipse`
- [ ] Live Eclipse patient-directory load and local launch matrix completed

The unchecked item is intentionally a post-release clinical workstation gate, not an automated or synthetic test. Its protocol and evidence fields are defined in [CLINICAL_VALIDATION.md](CLINICAL_VALIDATION.md).
