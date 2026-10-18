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
            if(Resources.Load<Material>("Generated/VoxelPBR")&&Resources.Load<Texture2D>("Generated/Icons/Item148"))return;
            MethodInfo ensure=null;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type=assembly.GetType("PresentationAssets",false);
                if(type!=null){ensure=type.GetMethod("Ensure",BindingFlags.Public|BindingFlags.Static);break;}
            }
            Assert.IsNotNull(ensure,"PresentationAssets.Ensure is missing from the editor assembly");
            ensure.Invoke(null,null);
        }
    }
}
