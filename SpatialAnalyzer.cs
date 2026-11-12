
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpatialAudioRadar;

public static class SpatialAnalyzer
{
    private static readonly object Sync = new();

    private static float _smoothedAngle;
    private static float _smoothedLevel;

    // Windows speaker channel order for common layouts:
    // FL, FR, FC, LFE, BL, BR, SL, SR.
    private static readonly float[] SpeakerAngles =
    {
        -30f, 30f, 0f, 0f, -150f, 150f, -90f, 90f
    };

    public static float Sensitivity { get; set; } = 3f;

    public static void Process(
        ReadOnlySpan<byte> buffer,
        WaveFormat format)
    {
        int channels = format.Channels;
        int bits = format.BitsPerSample;

        if (channels < 1 || channels > 8)
            return;

        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

        if (format.Encoding == WaveFormatEncoding.Extensible &&
            format is WaveFormatExtensible ext)
        {
            isFloat = ext.SubFormat ==
                new Guid("00000003-0000-0010-8000-00AA00389B71");
        }

        int bytesPerSample = bits / 8;

        if (bytesPerSample is not (2 or 3 or 4))
            return;

        int sampleCount = buffer.Length / bytesPerSample;
        int frames = sampleCount / channels;

        if (frames == 0)
            return;

        float[] energy = new float[channels];

        for (int i = 0; i < frames * channels; i++)
        {
            int offset = i * bytesPerSample;
            float sample;

            if (isFloat && bits == 32)
            {
                sample = BitConverter.ToSingle(
                    buffer.Slice(offset, 4));
            }
            else if (bits == 16)
            {
                short value = BitConverter.ToInt16(
                    buffer.Slice(offset, 2));
                sample = value / 32768f;
            }
            else if (bits == 24)
            {
                int value =
                    buffer[offset] |
                    (buffer[offset + 1] << 8) |
                    (buffer[offset + 2] << 16);

                if ((value & 0x800000) != 0)
                    value |= unchecked((int)0xFF000000);

                sample = value / 8388608f;
            }
            else if (bits == 32 && !isFloat)
            {
                int value = BitConverter.ToInt32(
                    buffer.Slice(offset, 4));
                sample = value / 2147483648f;
            }
            else
            {
                return;
            }

            if (!float.IsFinite(sample))
                continue;

            int channel = i % channels;
            energy[channel] += sample * sample;
        }

        for (int c = 0; c < channels; c++)
            energy[c] = MathF.Sqrt(energy[c] / frames);

        float total = 0;
        foreach (float e in energy)
            total += e;

        // Ignore near-silence.
        if (total < 0.00001f)
        {
            lock (Sync)
            {
                _smoothedLevel *= 0.88f;
                AudioState.Level = _smoothedLevel;
            }
            return;
        }

        float angle;

        if (channels == 1)
        {
            // Mono has no directional information.
            angle = 0f;
        }
        else if (channels == 2)
        {
            // Stereo: center = 0 degrees.
            // Left = -45 degrees, right = +45 degrees.
            float left = energy[0];
            float right = energy[1];

            float pan = (right - left) /
                        (right + left + 0.000001f);

            angle = pan * 45f;
        }
        else
        {
            // Multichannel: use real speaker positions.
            float x = 0;
            float y = 0;
            float directionalEnergy = 0;

            for (int c = 0; c < channels; c++)
            {
                // LFE channel is omnidirectional.
                if (c == 3)
                    continue;

                float radians =
                    SpeakerAngles[c] * MathF.PI / 180f;

                float weight = energy[c];

                x += MathF.Sin(radians) * weight;
                y += MathF.Cos(radians) * weight;

                directionalEnergy += weight;
            }

            angle = directionalEnergy > 0.00001f
                ? MathF.Atan2(x, y) * 180f / MathF.PI
                : 0f;
        }

        // Normalize loudness and apply sensitivity.
        float level = Math.Clamp(
            total / MathF.Sqrt(channels) * Sensitivity,
            0f,
            1f);

        lock (Sync)
        {
            // Smooth angle without jumping across the -180/180 boundary.
            float difference = ((angle - _smoothedAngle + 540f)
                                % 360f) - 180f;

            _smoothedAngle += difference * 0.28f;

            _smoothedLevel +=
                (level - _smoothedLevel) * 0.22f;

            AudioState.Angle = _smoothedAngle;
            AudioState.Level = _smoothedLevel;
            AudioState.Channels = channels;
        }
    }
}
