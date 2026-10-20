# Approved exterior geometry for future gameplay integration

The owner inspected renders of the actual voxel geometry and approved the Shuttle, LaunchPad and Station exterior drafts on 2026-10-20. `Shuttle.json`, `LaunchPad.json`, `Station.json` are the source volumes shown, not AI concept images. The RLE volumes compile to `Assets/Resources/Space/*.bytes` using `Tools/build_approved_space_models.py`; the Unity `SpaceVoxelVisual` renderer culls hidden faces and builds three-dimensional geometry from those exact cells. External `.glb` review exports and true-voxel renders are in `/home/user/work/space-approval/`, outside game assets.

Source SHA-256:
- Shuttle: `b402a78f02456c7a50d1235ba923857fb645a88552d3eadb7a0339ac853df019`
- LaunchPad: `efe90a4b0c49464342b9a29c9ceb8632bf92a8e27d4ecfa181d1ca56e6cb4c1d`
- Station: `caad99eef5cda886ce155ecf03a33862ce64a68a5f90a06d47bde7488cf3e6ab`

**Approval of shape is not approval of gameplay completeness.** The lift, six-seat cabin and airlock are still a separate prototype; there is no correctly aligned vehicle exterior, collision/boarding transition, operational pad or docked station in a world yet. The approved assets must not be passed off as a finished ship. The pad has NO crawler/transporter. No boss asset is imported.
