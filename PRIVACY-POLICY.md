# Privacy Policy

> The canonical, hosted version of this policy lives at <https://jihedkdiss.github.io/AutoCenter/privacy>. This file is the source for that page.

**Last updated:** May 9, 2026

Auto Center ("the app") is a desktop utility for Windows that automatically centers and resizes windows on your monitors. This document describes what data the app collects, stores, and transmits.

## Summary

**Auto Center does not collect, transmit, or share any personal data.** The app runs entirely on your device and makes no network requests.

## Data stored locally

Auto Center stores the following information on your computer only. None of it leaves your device.

| What | Where | Purpose |
|------|-------|---------|
| Settings (toggles, hotkey, alignment, sizing, theme) | `%LOCALAPPDATA%\AutoCenter\settings.json` | Persisting your preferences between launches |
| Crash log | `%LOCALAPPDATA%\AutoCenter\crash.log` | Local diagnostics if the app crashes |
| Startup registration | Windows Registry (`HKCU\...\Run`) or Microsoft Store StartupTask | Optional "Launch at startup" feature |

You can delete `%LOCALAPPDATA%\AutoCenter\` at any time to remove all stored data.

## Data transmitted

**None.** Auto Center does not call out to the internet, does not include any analytics, telemetry, or crash reporting service, and does not contact any server controlled by the developer or any third party.

## Microsoft Store

If you obtained Auto Center via the Microsoft Store, Microsoft may collect installation and usage telemetry under their own [privacy statement](https://privacy.microsoft.com/privacystatement). That telemetry is collected by the Microsoft Store platform itself, not by Auto Center.

## Children

Auto Center is not directed at children and does not knowingly collect any data from anyone of any age.

## Changes

If this policy changes, the updated version will be published in this repository with a new "Last updated" date.

## Contact

Questions? Open an issue at <https://github.com/jihedkdiss/AutoCenter/issues>.
