# SENZE Bodycam

My mods are no longer available on The Forge. For support, please use my [Discord](https://discord.gg/PD5TPQFKpa). The Forge may not provide support for mods released elsewhere.

SENZE Bodycam adds a body mounted first person camera and a recording overlay to SPT 4.1.6.

## Installation

Extract the release archive into the SPT installation folder. It contains the client plugin under `BepInEx/plugins` and the server mod under `SPT_Runtime/user/mods`.

- **SPT version:** 4.1.6
- **Required mods:** none
- **Optional integration:** SENZE Extreme Weather 1.3.12. Nearby or strong lightning can briefly interfere with the bodycam image when Extreme Weather's Bodycam interference setting is enabled.
- **FIKA compatibility:** not verified

## Features

- Body mounted camera with configurable position, camera weight, movement response, and visual shot reaction. The shot reaction does not change weapon recoil.
- Adjustable horizontal FOV from 120 to 145 degrees, including while aiming.
- Barrel shaped lens distortion, edge crop, chromatic aberration, motion blur, vignette, lens smears, sensor noise, and damage marks.
- Minimal recording HUD with operator name, raid date and time, SENZE logo, and magazine check label.
- Low Light and Night Vision modes cycled with F9.
- Weak, wide bodycam flashlight toggled with H.
- Optional face and eye mosaic.
- F10 toggles the bodycam. Most visual and camera settings can be changed in the BepInEx F12 menu.

## Notes

The bodycam flashlight and Night Vision are supporting tools. They are not intended to replace tactical lights or dedicated night vision equipment.

Extreme Weather is optional. Without it, Bodycam works normally; lightning interference is simply unavailable. The integration needs a matching installed Extreme Weather client plugin and has not yet been verified in a live raid for this release.

## Bug reports

Please include a description of the problem, BepInEx and server logs, and screenshots or video if available. You can report issues on [Discord](https://discord.gg/PD5TPQFKpa).
