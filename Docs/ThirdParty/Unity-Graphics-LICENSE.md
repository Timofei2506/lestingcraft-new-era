Copyright © 2020 Unity Technologies ApS

Licensed under the Unity Companion License for Unity-dependent projects--see [Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).

Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.

Adapted component: `Assets/Shaders/VoxelLighting.hlsl`, based on `UniversalFragmentPBR` in
https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl

Changes: renamed entry point and added spatial voxel-sky gating to main directional light and sky GI/reflections; retained additional lights, shadowing, material BRDF and emission. This is a Unity-dependent project.
