using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests
{
    /// <summary>
    /// Generated/* assets (materials, icons, arrays) are produced by the editor-only PresentationAssets.Ensure.
    /// The test runner may reach a test before its delayed Ensure call has run, so tests that read them call this first.
    /// </summary>
    public static class GeneratedPresentationAssets
    {
        public static void EnsureLoaded()
        {
            // Always run Ensure: it is idempotent and re-binds textures that an earlier call could not yet import (e.g. _LeafOpacity).
            MethodInfo ensure=null;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type=assembly.GetType("PresentationAssets",false);
                if(type!=null){ensure=type.GetMethod("Ensure",BindingFlags.Public|BindingFlags.Static);break;}
            }
            Assert.IsNotNull(ensure,"PresentationAssets.Ensure is missing from the editor assembly");
            ensure.Invoke(null,null);
            Assert.IsNotNull(Resources.Load<Material>("Generated/VoxelPBR"),"Generated/VoxelPBR was not created");
        }

        public static string Diagnostics()
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type=assembly.GetType("PresentationAssets",false);
                var method=type?.GetMethod("LeafDiagnostics",BindingFlags.Public|BindingFlags.Static);
                if(method!=null)return (string)method.Invoke(null,null);
            }
            return "PresentationAssets.LeafDiagnostics missing";
        }
    }
}
