# OCB Electricity Bugfixes - 7 Days to Die (A20) Addon

This Mod fixes some Bugs I found in the original game during the
development of my Electricity Overhaul Mod. I've extracted these
into its own project, since they also fix some edge-cases with the
vanilla game implementation.

You need to disable EAC to use this mod!

[![GitHub CI Compile Status][4]][3]

[3]: https://github.com/OCB7D2D/ElectricityWorkarounds/actions/workflows/ci.yml
[4]: https://github.com/OCB7D2D/ElectricityWorkarounds/actions/workflows/ci.yml/badge.svg

## Fix 1 - Better power duration support with sensors/triggers

If you configure a Motion Sensor or a Trip Wire to have a power
runtime and trigger it permanently, the trigger will constantly
turn itself off after the duration and then on again. This fix
will reset the duration as long as the trigger is active,
therefore only counting down the duration once the trigger
is deactivated (e.g. target moved out of range/sight).

Note: I believe this is actually a bug in the original code,
since the correct implementation seems to be there, but simply
one level nested too deeply inside another (unrelated) condition.

## Fix 2 - Don't disconnect downstream triggers forcefully

This Bug can be seen if you connect three TripWire posts, resulting
in two actual wires that you can configure. Leave the first one on
`instant` triggers (default) and set the second to have a `duration`
of e.g. 1 minute (also connect some bulbs to see trigger states).

Now if you activate the second trip-wire, the bulb should light up
for one minute, but once you walk through the first wire, the second
is also instantly deactivated.

## Fix 3 - Fix triggers with delay and `triggered` duration

This Bug can be seen if you connect a pressure plate and set it to
have a start delay and power duration to `triggered`. If you quickly
step over the plate, the power will go on after the delay but never
turn off. Power should never be on in the first place in this case.
This applies also to trip wires and motion sensors.

## Fix 4 - Potential bugfix for vanilla power.dat reset

When the save game doesn't load correctly or is aborted too early,
the PowerManager will still write it's state to disk, even though
it might not have been loaded yet, thus overwriting existing save.

## Changes

### Version 0.3.3

- Fix visual glitch with power switch emission

### Version 0.3.2

- Add potential fix for [vanilla power.dat reset bug][1]
- Automated deployment and release packaging

[1] https://community.7daystodie.com/a20-bug-database/confirmed/powerdat-overwritten-with-empty-file-when-server-is-shutting-down-during-restart-r605/

### Version 0.3.1

- Fix `AudioSource_InteractFullVolume` not found issue

### Version 0.3.0

- Rework fix how trigger groups are disconnected

### Version 0.2.0

- Refactor for A20 compatibility

### Download and Install

Simply download here from GitHub and put into your A20 Mods folder:

- https://github.com/OCB7D2D/ElectricityWorkarounds/archive/master.zip

## Compatibility

I've developed and tested this Mod against version a20.b218.
