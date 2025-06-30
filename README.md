# Rope Swinging Mod v0.6.2

**RopeSwing** is a mod for PEAK that allows swinging on ropes by adding dynamic momentum, player-controlled swing forces to rope segments. This mod lets you push yourself around while climbing ropes.

This mod is still janky and a work in progress. See this as a beta mod until it eventually reaches version 1.0.0. Feel free to give feedback in the PEAK Modding Discord.

---

## Features

- Adds player-controlled swinging while climbing a rope  
- Configurable swing strength and keys  
- Configurable rope segment mass  
- Configurable rope joint limits to fine-tune how freely the rope bends  
- Multiplier for swing power scaling with rope length  
- Compatible with ragdoll parts to make rope interactions look more physical

---

## Controls

| Action         | Default Key |
|----------------|-------------|
| Swing forward  | Left Shift  |
| Swing backward | Left Control|
| Swing left     | A           |
| Swing right    | D           |

All of these keys can be changed in the BepInEx config file after first launch.

---

## Configuration

After running the game once with the mod installed, a config file will be created under:  

BepInEx/config/com.lamia.ropeswing.cfg


Here you can adjust:  

- **BaseForce** — how much force is applied when swinging  
- **JointAngularZLimit** — how far each rope segment can twist  
- **RopeMass** — mass of each segment  
- **SegmentYouAreOnRopeMass** — mass of the segment you are currently grabbing  
- **RopeLengthSpeedMultiplierPerSegment** — how much swing power scales per segment down the rope  
- Key bindings for swinging in each direction

---

## Installation

1. Install BepInEx for your PEAK version  
2. Place `Rope Swing Mod.dll` into your `BepInEx/plugins` folder  
3. Launch the game once to generate the config  
4. Adjust the config to taste  
5. Enjoy swinging!

---

## Known Issues

- Heavy joint limits may cause the rope to feel stiff  
- High masses or limits might break physics if too extreme  
- The whole system is still extremely janky and not fine tuned. If you have any ideas or you found good configurations- please reach out to me on the Peak Modding Discord!
- Because of how the ropes and climbing normally works in PEAK, you always seem to be 'dragged' after the rope. I want to fix it by making it smoother but I don't know how yet

---
## Ideas
- I thought about adding a small boost after jumping off a rope, simulating momentum

---

## Changelog

### 0.6.2
- Added full configurability
- This is still very much a work in progress


