# Tuesday (Tiled Unity Editor, Serializer, Deserializer, and You)

Tuesday is a generic C# Tiled (.tmx) serializer and deserializer and a set of Unity editor scripts that allow you to drag and drop TMX files into your scene, make edits, and save back out as TMX files.

The goals of this project are full support of the TMX file format, feature parity with the standalone Tiled map editor, and minimal dependencies.


## Installation

[To add as a Unitypackage, download it from the itch.io page.](https://318arcade.itch.io/tuesday)

To add as a Git submodule:
`git submodule add https://github.com/ShreveportArcade/Tuesday.git Assets/Tuesday`

## Prefab Replacement

Template Group files can be remapped as prefabs. Components can be added and their fields/properties modified using the format "ClassName.fieldName". For example, you can set the gravity scale of a prefab's Rigidbody2D by adding a float property called "Rigidbody2D.gravityScale".

## Features

 * Drag and Drop TMX files into the Scene View or Hierarchy
 * Converts TMX/TSX files to Unity Tilemaps/Tiles
 * Supports both external and embedded Tile Sets 
 * CSV, Base64, GZIP, and zLib encoding/decoding
 * Collision geometry support
 * Tile layer tint support
 * Tiled Object support
 * Prefab replacement
 * Edit your tilemaps in Unity
 * Export your changes back out as TMX/TSX files
 * Infinite map support
 * Template Group (.tx) support

## Roadmap
 * Zstd encoding/decoding
 * Wang Tile and Terrain Brushes
 * Text Support