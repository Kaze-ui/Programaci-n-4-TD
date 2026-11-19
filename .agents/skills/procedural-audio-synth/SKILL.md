---
name: procedural-audio-synth
description: >-
  Use this skill when designing, authoring, or synthesizing procedural audio effects
  directly in Unity C# using AudioClip.Create, mathematical frequency sweeps, FM modulation,
  and soft-clip normalization without relying on external audio assets.
---

# Procedural Audio Synthesis for Unity (AudioClip.Create)

This skill provides the mathematical recipes and architectural patterns for generating high-definition, zero-dependency sound effects inside Unity C#.

## Core Architecture

1. **Clip Initialization**:
   ```csharp
   const int sampleRate = 44100;
   float duration = 0.70f;
   int totalSamples = Mathf.RoundToInt(duration * sampleRate);
   float[] samples = new float[totalSamples];
   ```

2. **Synthesis Loop**:
   Always compute time `t = i / (float)sampleRate` and normalized progress `u = Mathf.Clamp01(t / duration)`.
   Accumulate phases continuously (`phase += 2f * Mathf.PI * freq / sampleRate; if (phase > 2f * Mathf.PI) phase -= 2f * Mathf.PI;`).

3. **Attack & Release Envelopes**:
   - Explosive punch (3ms attack): `float envIn = Mathf.Clamp01(t / 0.003f);`
   - Smooth decay: `float envOut = Mathf.Clamp01((duration - t) / releaseSec);`

4. **Sound Recipes**:

   ### A. Energy Shield Collapse & Glass Shatter
   - **Crystal Glass Transient**: High pings at 2800 Hz, 1940 Hz, and 3600 Hz with fast exponential decay (`Mathf.Exp(-t * 18f)`).
   - **Plasma Decompression Sweep**: Descending frequency from 920 Hz down to 110 Hz (`110f + 810f * Mathf.Exp(-t * 9.5f)`).
   - **Sub-Bass Discharge Boom**: Deep rumble dropping from 160 Hz to 42 Hz (`42f + 118f * Mathf.Exp(-t * 14f)`).
   - **Warning Power-Down Chirp**: Dual tone interval descending (e.g. 784 Hz -> 523 Hz) between 0.1s and 0.5s.

   ### B. Heavy Sci-Fi Laser & Beam
   - **Core Sweep**: 980 Hz plunging to 185 Hz (`190f + 820f * Mathf.Exp(-t * 16f)`).
   - **Harmonic Body**: Sub-sine (68 Hz) + Core sine (136 Hz) + 5th harmonic (204 Hz).
   - **Plasma Hiss**: Double low-pass filtered deterministic pseudo-noise.

5. **Essential Normalization (Never Skip)**:
   Track `peak = Mathf.Max(peak, Mathf.Abs(sample));` in the loop, then normalize:
   ```csharp
   float norm = 0.85f / peak;
   for (int i = 0; i < totalSamples; i++) samples[i] *= norm;

   AudioClip clip = AudioClip.Create("clip_name", totalSamples, 1, sampleRate, false);
   clip.SetData(samples, 0);
   return clip;
   ```

