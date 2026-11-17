using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public sealed class VoxelWorldGenerationTests
    {
        [Test]
        public void EvictedGenerationBufferIsRejectedBeforeCopy()
        {
            var guard = typeof(VoxelWorld).GetMethod("GenerationBufferAvailable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(guard, "VoxelWorld must retain the disposed-buffer guard before copying chunk data.");

            var buffer = new NativeArray<ushort>(32, Allocator.Temp);
            try
            {
                Assert.IsTrue((bool)guard.Invoke(null, new object[] { buffer }));
                buffer.Dispose();
                Assert.IsFalse((bool)guard.Invoke(null, new object[] { buffer }));
            }
            finally
            {
                if (buffer.IsCreated) buffer.Dispose();
            }
        }
    }
}
