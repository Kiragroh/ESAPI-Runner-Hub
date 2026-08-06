# Final verification checklist

Verified for public release v0.3.1 and the synchronized internal Citrix deployment on 2026-08-06.

- [x] Responsive catalogue uses one centered search/category/format filter bar
- [x] Four 284-DIP application cards fit on a 1586-pixel-wide synthetic smoke window
- [x] Horizontal catalogue scrolling is disabled
- [x] Recent activity uses proportional columns and exposes replay availability reasons
- [x] `Run again` is enabled for a terminal activity with an available application and protected context
- [x] Privacy mode obscures patient identifiers, treatment context, paths, tooltips, and activity context
- [x] Synthetic screenshot contains no clinical data
- [x] Visible Hub, Script Host, Citrix launcher, example settings, and live catalogue copy is English
- [x] One Citrix Published Application supports the UI and user-scoped exact-context requests
- [x] Direct context scripts remain isolated in the adjacent Eclipse 18 Script Host
- [x] STR Hub README actions copy an IP-reachable URL without launching a browser
- [x] Direct `GetDicomCollectionUKL.cs` source compilation succeeds with Windows Forms support
- [x] Direct `ExportPlansQuicker.esapi.dll` is configured as a read-only patient-context binary
- [x] Script Host configuration permits explicitly configured UNC assemblies
- [x] Write-enabled scripts retain their explicit save boundary
- [x] Child failures remain isolated from the Hub process
- [x] Existing live application paths and launch arguments were preserved while the two requested export entries and Hub IP were added
- [x] Public release package contains no vendor assemblies
- [x] 142 automated tests pass
- [x] CMD launcher contract passes with 0 failures
- [x] EXE launcher contract passes with 0 failures
- [x] Deterministic v0.3.1 ZIP and immutable Citrix binary were created
- [x] `citrix/current.txt` selects `ESAPI-Runner-Hub.v0.3.1.exe`
- [ ] Live clinical Eclipse script matrix completed

The unchecked item is intentionally a clinical workstation gate. Automated and synthetic evidence does not replace validation of each configured clinical application. The protocol is defined in [CLINICAL_VALIDATION.md](CLINICAL_VALIDATION.md).
