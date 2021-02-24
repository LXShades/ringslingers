using UnityEngine;

public static class Compressor
{
    const int kBitsPerComponent = 8;
    const int kMaskPerComponent = ~(~0 << kBitsPerComponent);
    const int kMultiplierPerComponent = ~(~0 << (kBitsPerComponent - 1));

    public static ushort CompressFloat16(float value, float min, float max)
    {
        return (ushort)((value - min) * 65535 / (max - min) + 0.49999f);
    }

    public static float DecompressFloat16(ushort value, float min, float max)
    {
        return min + value * (max - min) / 65535;
    }

    public static int CompressQuaternion(Quaternion quaternion)
    {
        int result = 0;

        result |= ((int)(quaternion.x * kMultiplierPerComponent) & kMaskPerComponent);
        result |= ((int)(quaternion.y * kMultiplierPerComponent) & kMaskPerComponent) << kBitsPerComponent;
        result |= ((int)(quaternion.z * kMultiplierPerComponent) & kMaskPerComponent) << (kBitsPerComponent * 2);
        result |= ((int)(quaternion.w * kMultiplierPerComponent) & kMaskPerComponent) << (kBitsPerComponent * 3);

        return result;
    }

    public static Quaternion DecompressQuaternion(int quaternion)
    {
        Quaternion result = default;

        result.x = (quaternion << (32 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;
        result.y = (quaternion << (24 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;
        result.z = (quaternion << (16 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;
        result.w = (quaternion << (8 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;

        return result;
    }

    public static int CompressNormal24(Vector3 normalVector)
    {
        int result = 0;

        result |= ((int)(normalVector.x * kMultiplierPerComponent) & kMaskPerComponent);
        result |= ((int)(normalVector.y * kMultiplierPerComponent) & kMaskPerComponent) << kBitsPerComponent;
        result |= ((int)(normalVector.z * kMultiplierPerComponent) & kMaskPerComponent) << (kBitsPerComponent * 2);

        return result;
    }

    public static Vector3 DecompressNormal24(int normalVector)
    {
        Vector3 result = default;

        result.x = (normalVector << (32 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;
        result.y = (normalVector << (24 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;
        result.z = (normalVector << (16 - kBitsPerComponent) >> (32 - kBitsPerComponent)) / (float)kMultiplierPerComponent;

        return result;
    }

    public static short CompressNormal16(Vector3 normalVector)
    {
        int result = 0;

        result |= ((int)(normalVector.x * 63f) & ~(~0 << 7));
        result |= ((int)(normalVector.z * 63f) & ~(~0 << 7)) << 7;
        result |= (normalVector.y >= 0f ? 1 : 0) << 14;

        return (short)result;
    }

    public static Vector3 DecompressNormal16(short _normalVector)
    {
        Vector3 result = default;
        int normalVector = (int)_normalVector; // we're working in an int context while shifting, usually
        float ySign = (normalVector & (1 << 14)) == 0 ? -1f : 1f;

        result.x = (normalVector << (32 - 7) >> (32 - 7)) / 63f;
        result.z = (normalVector << (25 - 7) >> (32 - 7)) / 63f;

        result.y = Mathf.Sqrt(1 - result.x * result.x - result.z * result.z) * ySign;

        return result;
    }

    public static int UnitFloatToBits(float value, int numBits)
    {
        float multiplier = (1 << (numBits - 1)) - 1f;
        int result = (int)(value * multiplier);
        return (value >= 0f ? result : result | (1 << (numBits - 1))) & ~(~0 << numBits);
    }

    public static float BitsToUnitFloat(int value, int numBits)
    {
        float multiplier = (1 << (numBits - 1)) - 1f;
        float result = (value << (32 - numBits) >> (32 - numBits)) / multiplier;
        return result;
    }
}