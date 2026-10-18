// Минимальные заглушки Unity-API, чтобы ядро генерации компилировалось вне Unity.
// Используются только в Tools/worldgen-harness. В Unity-проекте не участвуют.
using System;

namespace Unity.Burst
{
    public sealed class BurstCompileAttribute : Attribute { }
}

namespace Unity.Jobs
{
    public interface IJobParallelFor { void Execute(int index); }
}

namespace Unity.Collections
{
    public enum Allocator { Temp, TempJob, Persistent }
    public sealed class NativeDisableParallelForRestrictionAttribute : Attribute { }
    public sealed class NativeArray<T> : IDisposable
    {
        private readonly T[] _data;
        public NativeArray(int length, Allocator allocator) { _data = new T[length]; }
        public T this[int index] { get => _data[index]; set => _data[index] = value; }
        public int Length => _data.Length;
        public void Dispose() { }
    }
}

namespace Unity.Mathematics
{
    public static class math
    {
        public static float floor(float x) => (float)Math.Floor(x);
        public static float abs(float x) => Math.Abs(x);
        public static int abs(int x) => Math.Abs(x);
        public static float lerp(float a, float b, float t) => a + (b - a) * t;
        public static float clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;
    }
}
