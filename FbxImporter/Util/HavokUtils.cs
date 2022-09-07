using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FbxImporter.Util
{
    public static class HavokUtils
    {
        public static float[] UnpackHkPackedVector3(IEnumerable<ushort> hkPackedVector3)
        {
            int[] intVector = hkPackedVector3.Select(x => x << 16).ToArray();
            float[] hkVector4 = intVector.Select(Convert.ToSingle).ToArray();

            byte[] expBytes = BitConverter.GetBytes(intVector[3]);
            float expCorrection = BitConverter.ToSingle(expBytes, 0);
            hkVector4 = hkVector4.Select(x => x * expCorrection).ToArray();

            return hkVector4;
        }

        public static ushort[] PackHkPackedVector3(IList<float> vector)
        {
            if (vector.Count == 3)
            {
                vector = vector.Append(1).ToArray();
            }

            const float floatEpsilon = 1.192092896e-07f;
            float[] vectorEpsSquared = vector.Take(3).Append(floatEpsilon * floatEpsilon).ToArray();

            const float rounding = 1.00001633167266845703125f; //0x3f800089
            float[] vectorRounded = HavokVectorMul(new[] { rounding, rounding, rounding, rounding }, vectorEpsSquared);

            const uint exponentMask = 0x7f800000;
            uint uintMax = vectorRounded.Select(BitConverter.SingleToUInt32Bits).Select(x => x & exponentMask).Max();

            const uint offset = 0x4e800000;
            uint[] uintVector = vector.Select(BitConverter.SingleToUInt32Bits)
                .Select(x => unchecked(x + offset - uintMax)).ToArray();

            uintMax = unchecked(uintMax - offset);

            const uint roundingCorrection = 0x00008000;
            uint[] result = uintVector.Select(BitConverter.UInt32BitsToSingle).Select(Convert.ToInt32)
                .Select(x => unchecked((uint)x) + roundingCorrection).ToArray();

            uint w = unchecked(uintMax + 0x3F800000);

            ushort[] packedVector3 = result.Take(3).Append(w).Select(x => ExtractUShort(x, 1)).ToArray();

            return packedVector3;
        }

        private static float[] HavokVectorMul(IList<float> vec1, IList<float> vec2)
        {
            float[] outVec = new float[vec1.Count];
            for (int i = 0; i < vec1.Count; i++)
            {
                outVec[i] = vec1[i] * vec2[i];
            }

            return outVec;
        }

        private static ushort ExtractUShort(uint num, int shortIndex)
        {
            byte[] bytes = BitConverter.GetBytes(num);
            return BitConverter.ToUInt16(bytes, shortIndex * 2);
        }

        public static float[] ToFloatArray(this Vector3 vector)
        {
            return new[] { vector.X, vector.Y, vector.Z };
        }

        public static Vector3 ToVector3(this float[] array)
        {
            return new Vector3(array[0], array[1], array[2]);
        }
    }
}
