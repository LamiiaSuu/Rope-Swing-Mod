# Rope Swinging Mod v1.0.0

**RopeSwing** is a mod for PEAK that lets you swing on ropes by adding player-controlled forces and momentum. Push yourself around while climbing, build up speed and jump off with some of the rope’s momentum.

It finally reached 1.0.0, but it can still be janky. Feel free to give feedback in the PEAK Modding Discord!

## Features

* Player-controlled swinging while climbing a rope
* Configurable swing strength and keys
* Lower starting force that builds up while you keep swinging
* More swing power on longer ropes
* Momentum when jumping off, depending on how fast the rope is moving
* Gradual return to normal PEAK air movement after jumping off
* Multiple players can swing the same rope together
* Reduced resistance from other players on the rope

## Controls

| Action | Default Key |
|----------------|-------------|
| Swing forward | Left Shift |
| Swing backward | Left Control |
| Swing left | A |
| Swing right | D |

All of these keys can be changed in the BepInEx config file after first launch.

## Configuration

After running the game once with the mod installed, a config file will be created under:

`BepInEx/config/com.lamia.ropeswing.cfg`

Here you can adjust:

* **Acceleration:** Full swing strength. Starts at half strength and builds up over about 6 seconds of active swinging
* **DriveSpeed:** How fast you can push the rope before the swing force starts fading. Scales with rope length
* **PumpBoost:** Extra force when pushing in the direction you are already moving
* **CoastDamping:** How quickly the rope loses momentum
* **SteeringDamping:** How much sideways drifting gets slowed down while steering
* **ClimberLoad:** Extra downward pull where you are climbing. Does not change segment mass
* **JumpAirDrag:** Air resistance after jumping off. Returns to normal PEAK air movement over about 1.8 seconds
* Key bindings for swinging in each direction

The old **BaseForce**, mass and joint settings are no longer used. They might still appear in your config if you used an older version.

## Installation

1. Install BepInEx for your PEAK version
2. Place `Rope Swing Mod.dll` into your `BepInEx/plugins` folder
3. Remove older RopeSwing DLLs so you only have one installed
4. Launch the game once to generate the config
5. Adjust the config to taste
6. Enjoy swinging!

Everyone in multiplayer needs the same version installed, including whoever created the rope.

## Known Issues

* You can still feel dragged behind the rope, especially at high speeds or with latency
* Extreme config values might make the physics act weird
* Multiplayer and different rope lengths might still need some fine tuning. If you have any ideas or found good configurations, please reach out to me on the PEAK Modding Discord!

## Changelog

### 1.0.0

* Halved the starting swing force. Builds up while swinging, with the same maximum speed allowance
* Jump momentum now depends on the rope’s actual speed
* Added a gradual return to normal PEAK air movement over about 1.8 seconds
* Reduced resistance from other climbers
* Additional mod weight from players on the same segment no longer stacks

### 0.7.2

* Improved player attachment and reduced dragging behind the rope
* Adjusted the follow force to feel less stiff
* Improved momentum when jumping off
* Limited jump boosts so they do not stack
* Adjusted swing force and movement along the rope

### 0.7.1

* Improved swing strength on long ropes
* Increased the speed allowance on long ropes for wider swings
* Improved pumping responsiveness

### 0.7.0

* Reworked rope physics and networking to address freezing
* Removed repeated Photon ownership requests
* Added simultaneous swing control for multiple players
* Improved swing responsiveness and reduced unwanted drift
* Updated compatibility with current PEAK and BepInEx
* Replaced the old mass and joint settings with new swing settings

### 0.6.3

* Fixed a bug where you would fall off the rope if climbing down while swinging

### 0.6.2

* Added full configurability
* This is still very much a work in progress
