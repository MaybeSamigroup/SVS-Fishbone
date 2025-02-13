# SVS-Fishbone

plugin api to serialize and deserialize character or coordinate bound extension data, for SamabakeScramble

# Prerequests

 * BepinEx v6

# Installation

Extract the release to game root.

# How it works

Fishbone extensions are stick in to game character/coordinate poatrait, as a PNG private extension chunk of "fsBN".

For coordinate, where you see at file explorer.

For character, where you see at in game character selection view.

# How to use

Plugin authors who want to use fishbone, should subscribe all or some of these event.

Extension storages are provided as ZipArchive, shared by all plugin.

So strongly advised to use each plugin Guid named directories and it's sub entries.

## OnCharacterCreationSerialize

Notified when character is serialized in Character Creation.

## OnCharacterCreationDeserialize

Notified when character is deserialized in Character Creation.

## OnCoordinateSerialize

Notified when coordinate is serialized in Character Creation.

## OnCoordinateInitialize

Notified When character coordinates is reset to default in H Scene.

## OnCoordinateDeserialize

Notified when coordinate is deserialized in Character Creation or H Scene.

## OnActorSerialize

Notified when actor serialized in simulation mode. aka, when auto or manually saved.

Actor bound to the serialized data is indicated by index from ```Game.saveData.Chars``.

## OnActorDeserialize 

Notified when actors are deserialized in just before entering simulation mode.

Actor bound to the deserialized data is indicated by index from ```Game.saveData.Chars``.
