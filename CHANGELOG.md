# Changelog

## 1.2.5

Changes since the public 1.2.0 repository state:

- Replaced the former status HUD with the minimal REC, operator, raid time, magazine check, and SENZE logo layout.
- Extended adjustable horizontal FOV to 145 degrees, with the same angle while aiming.
- Kept the F9 Low Light and Night Vision cycle and H flashlight, while fixing their effect strengths in code.
- Changed the lens distortion to a barrel shaped effect and exposed edge crop to reduce stretched edge blur.
- Softened the vignette edge and made the current tested configuration values the defaults.
- Removed corner peeking, rain on lens, camera exposure, highlight bloom, signal flicker, HUD presets, and the ADS camera preset selector. The former Vanilla aiming position remains active.
- Rebased Body Offset X so zero represents the previously tuned camera position; negative values shift the weapon image left and positive values right.
- Removed the former battery and wireless HUD assets and unused weapon motion patch.
- Kept the Bodycam interference API used by SENZE Extreme Weather 1.3.12 for lightning effects.

### Verification

- Client and server projects build for SPT 4.1.6 without warnings or errors.
- The Bodycam and Extreme Weather API signatures were checked against local source.
- Lightning interference and exact weapon centering have not yet been confirmed in a live raid.
