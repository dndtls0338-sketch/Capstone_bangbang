Approved neighborhood art implementation

neighborhood.png is the approved town artwork with the baked people and moving
road car removed. It is imported losslessly with point filtering and no mipmaps.
Its native resolution is 1584 x 993. Runtime uses 18 source pixels per world unit;
the artwork's aspect ratio is preserved. Zooming cannot create extra source detail.

CapApprovedTown.cs builds 64 ground cells from the atlas and separate depth-sorted
building/tree/prop regions described by layout.json. Physical footprints are
independent BoxCollider2D rectangles, also used by server-authoritative movement.
Coordinates in layout.json use a top-left source-pixel origin. Each object has a
render rectangle, a foot coordinate for Y sorting, and an optional solid rectangle.
Tree render regions use an octagonal mesh to reduce square-corner occlusion.

This is an art-backed exploration map, not a collection of fully extracted,
independently movable art assets. The ground cells still contain the original
painted objects; the foreground regions repeat those pixels for depth ordering.
Moving/removing a building requires updating the corresponding ground artwork
as well as its overlay and collision data. Do not reposition overlays alone.
Building interiors and witness interactions are not part of this map change.

Backups of the previous blockout source are in CapSetupBackup/before-approved-town.
The previous blockout builder remains available as BuildBlockout, but is inactive.
The campus spawn, lobby/town phase and player movement still use the existing
networked player flow. CapTownSmoke checks paths, all four spawn positions,
physical footprints, front/behind rendering and lobby/town re-entry.
