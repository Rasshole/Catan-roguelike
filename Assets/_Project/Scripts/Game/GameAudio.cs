using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>v143: every gameplay sound effect. All are synthesized in code (no third-party audio files).</summary>
    public enum Sfx
    {
        Click,
        Road,
        Settlement,
        City,
        CardDraw,
        DevBuy,
        DevPlay,
        Dice,
        Robber,
        Steal,
        TradeAccepted,
        TradeDeclined,
        TurnStart,
        TimerTick,
        QuestDone,
        Vote,
        Auction,
        Sabotage,
        Victory,
        /// <summary>v145: a ship launched (splash + hull knock).</summary>
        Ship,
        /// <summary>v145: a ship sails to a new edge (soft wash).</summary>
        ShipMove,
        /// <summary>v145: the pirate moves (low horn).</summary>
        Pirate
    }

    /// <summary>
    /// v143: tiny cozy sound engine. Clips are synthesized once at first use (soft marimba/bell tones, wood knocks,
    /// paper swishes, dice rattles) so there are no licensing questions — see docs/art/CREDITS.md. Volume and mute
    /// persist in PlayerPrefs and are set from the in-game settings popup.
    /// </summary>
    public static class GameAudio
    {
        private const string VolumeKey = "catan.sfx.volume";
        private const string MuteKey = "catan.sfx.muted";
        private const int Rate = 44100;

        private static readonly Dictionary<Sfx, AudioClip> Clips = new();
        private static readonly Dictionary<Sfx, float> LastPlayed = new();
        private static AudioSource _source;
        private static float? _volume;
        private static bool? _muted;

        /// <summary>Plays recorded by tests (the last sound requested, even when muted or headless).</summary>
        public static readonly List<Sfx> PlayedForTests = new();

        public static float Volume
        {
            get => _volume ??= PlayerPrefs.GetFloat(VolumeKey, 0.7f);
            set
            {
                _volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
            }
        }

        public static bool Muted
        {
            get => _muted ??= PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                _muted = value;
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
            }
        }

        public static void Play(Sfx sfx, float volumeScale = 1f)
        {
            if (PlayedForTests.Count > 256)
                PlayedForTests.RemoveRange(0, 128);
            PlayedForTests.Add(sfx);
            if (!Application.isPlaying || Muted || Volume <= 0.001f)
                return;
            // Don't stack the same sound within 60 ms (a batch of AI builds should not clip).
            float now = Time.unscaledTime;
            if (LastPlayed.TryGetValue(sfx, out float last) && now - last < 0.06f)
                return;
            LastPlayed[sfx] = now;
            var source = EnsureSource();
            if (source == null)
                return;
            source.pitch = 1f;
            source.PlayOneShot(Clip(sfx), Volume * volumeScale * BaseGain(sfx));
        }

        public static AudioClip Clip(Sfx sfx)
        {
            if (Clips.TryGetValue(sfx, out var clip) && clip != null)
                return clip;
            clip = Synthesize(sfx);
            Clips[sfx] = clip;
            return clip;
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            return _source;
        }

        private static float BaseGain(Sfx sfx) => sfx switch
        {
            Sfx.Click => 0.35f,
            Sfx.TimerTick => 0.45f,
            Sfx.CardDraw => 0.5f,
            Sfx.Victory => 0.8f,
            _ => 0.65f
        };

        // ---------- synthesis ----------

        private static AudioClip Synthesize(Sfx sfx)
        {
            var rng = new System.Random(1000 + (int)sfx);
            float[] d;
            switch (sfx)
            {
                case Sfx.Click:
                    d = Buf(0.06f);
                    Knock(d, 0f, 1400f, 0.018f, 0.5f, rng);
                    break;
                case Sfx.Road:
                    d = Buf(0.28f);
                    Knock(d, 0f, 210f, 0.07f, 0.9f, rng);
                    Knock(d, 0.11f, 240f, 0.06f, 0.7f, rng);
                    break;
                case Sfx.Settlement:
                    d = Buf(0.6f);
                    Knock(d, 0f, 190f, 0.08f, 0.9f, rng);
                    Marimba(d, 0.06f, Note(72), 0.35f, 0.45f);
                    Marimba(d, 0.13f, Note(76), 0.35f, 0.45f);
                    break;
                case Sfx.City:
                    d = Buf(0.9f);
                    Knock(d, 0f, 150f, 0.1f, 1f, rng);
                    Knock(d, 0.09f, 170f, 0.09f, 0.8f, rng);
                    Bell(d, 0.12f, Note(67), 0.6f, 0.35f);
                    Bell(d, 0.12f, Note(72), 0.6f, 0.3f);
                    Bell(d, 0.12f, Note(76), 0.6f, 0.3f);
                    break;
                case Sfx.CardDraw:
                    d = Buf(0.16f);
                    Swish(d, 0f, 0.14f, 2500f, 5200f, 0.6f, rng);
                    break;
                case Sfx.DevBuy:
                    d = Buf(0.5f);
                    Swish(d, 0f, 0.14f, 2200f, 4800f, 0.5f, rng);
                    Bell(d, 0.1f, Note(79), 0.4f, 0.35f);
                    break;
                case Sfx.DevPlay:
                    d = Buf(0.6f);
                    Swish(d, 0f, 0.12f, 4000f, 2000f, 0.4f, rng);
                    Marimba(d, 0.06f, Note(72), 0.3f, 0.4f);
                    Marimba(d, 0.14f, Note(79), 0.3f, 0.4f);
                    Marimba(d, 0.22f, Note(84), 0.35f, 0.4f);
                    break;
                case Sfx.Dice:
                    d = Buf(0.95f);
                    float t = 0f;
                    while (t < 0.62f)
                    {
                        Knock(d, t, 900f + (float)rng.NextDouble() * 900f, 0.02f, 0.25f + 0.25f * (float)rng.NextDouble(), rng);
                        t += 0.035f + (float)rng.NextDouble() * 0.06f;
                    }

                    Knock(d, 0.7f, 420f, 0.05f, 0.7f, rng);
                    Knock(d, 0.79f, 380f, 0.05f, 0.6f, rng);
                    break;
                case Sfx.Robber:
                    d = Buf(0.8f);
                    Glide(d, 0f, 0.7f, 130f, 78f, 0.55f, 6f);
                    Knock(d, 0f, 110f, 0.12f, 0.7f, rng);
                    break;
                case Sfx.Steal:
                    d = Buf(0.3f);
                    Swish(d, 0f, 0.22f, 5000f, 1600f, 0.6f, rng);
                    break;
                case Sfx.TradeAccepted:
                    d = Buf(0.55f);
                    Marimba(d, 0f, Note(79), 0.3f, 0.45f);
                    Marimba(d, 0.1f, Note(84), 0.4f, 0.5f);
                    break;
                case Sfx.TradeDeclined:
                    d = Buf(0.5f);
                    Marimba(d, 0f, Note(64), 0.3f, 0.4f);
                    Marimba(d, 0.12f, Note(60), 0.35f, 0.4f);
                    break;
                case Sfx.TurnStart:
                    d = Buf(0.9f);
                    Bell(d, 0f, Note(81), 0.8f, 0.35f);
                    Bell(d, 0f, Note(88), 0.6f, 0.12f);
                    break;
                case Sfx.TimerTick:
                    d = Buf(0.08f);
                    Knock(d, 0f, 1800f, 0.03f, 0.6f, rng);
                    break;
                case Sfx.QuestDone:
                    d = Buf(1.0f);
                    Marimba(d, 0f, Note(72), 0.3f, 0.4f);
                    Marimba(d, 0.1f, Note(76), 0.3f, 0.4f);
                    Marimba(d, 0.2f, Note(79), 0.3f, 0.4f);
                    Bell(d, 0.3f, Note(84), 0.65f, 0.4f);
                    break;
                case Sfx.Vote:
                    d = Buf(0.4f);
                    Knock(d, 0f, 260f, 0.06f, 0.9f, rng); // gavel
                    Knock(d, 0.16f, 260f, 0.06f, 0.7f, rng);
                    break;
                case Sfx.Auction:
                    d = Buf(0.9f);
                    Bell(d, 0f, Note(76), 0.8f, 0.4f);
                    Bell(d, 0f, Note(83), 0.6f, 0.15f);
                    break;
                case Sfx.Sabotage:
                    d = Buf(0.6f);
                    Knock(d, 0f, 90f, 0.14f, 1f, rng);
                    Swish(d, 0.05f, 0.4f, 1200f, 400f, 0.35f, rng);
                    break;
                case Sfx.Victory:
                    d = Buf(1.8f);
                    int[] fan = { 67, 72, 76, 79 };
                    for (int i = 0; i < fan.Length; i++)
                        Marimba(d, i * 0.12f, Note(fan[i]), 0.3f, 0.45f);
                    Bell(d, 0.5f, Note(72), 1.2f, 0.3f);
                    Bell(d, 0.5f, Note(76), 1.2f, 0.25f);
                    Bell(d, 0.5f, Note(79), 1.2f, 0.25f);
                    Bell(d, 0.5f, Note(84), 1.2f, 0.25f);
                    break;
                case Sfx.Ship:
                    d = Buf(0.7f);
                    Swish(d, 0f, 0.45f, 900f, 2600f, 0.7f, rng); // splash
                    Knock(d, 0.05f, 170f, 0.09f, 0.8f, rng); // hull on water
                    Marimba(d, 0.18f, Note(74), 0.3f, 0.35f);
                    break;
                case Sfx.ShipMove:
                    d = Buf(0.6f);
                    Swish(d, 0f, 0.5f, 700f, 1800f, 0.6f, rng);
                    Swish(d, 0.18f, 0.35f, 1500f, 600f, 0.4f, rng);
                    break;
                case Sfx.Pirate:
                    d = Buf(1.0f);
                    Glide(d, 0f, 0.85f, 98f, 92f, 0.55f, 3f); // ship's horn
                    Glide(d, 0f, 0.85f, 147f, 138f, 0.3f, 3f);
                    Swish(d, 0.05f, 0.5f, 800f, 2000f, 0.3f, rng);
                    break;
                default:
                    d = Buf(0.05f);
                    break;
            }

            Normalize(d, 0.85f);
            var clip = AudioClip.Create("sfx_" + sfx, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        private static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];

        private static void Marimba(float[] d, float at, float f, float decay, float amp)
        {
            // Wooden bar: fundamental + the 4th partial (≈3.9×), soft attack, exponential decay.
            int s0 = (int)(at * Rate);
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / (decay * 0.35f));
                if (env < 1e-4f && t > 0.01f)
                    break;
                d[i] += amp * env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 3.9f * t)
                    * Mathf.Exp(-t / 0.05f));
            }
        }

        private static void Bell(float[] d, float at, float f, float decay, float amp)
        {
            int s0 = (int)(at * Rate);
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t / (decay * 0.4f));
                if (env < 1e-4f && t > 0.01f)
                    break;
                d[i] += amp * env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3.0f * t) * Mathf.Exp(-t / 0.15f));
            }
        }

        private static void Knock(float[] d, float at, float f, float decay, float amp, System.Random rng)
        {
            // Wood knock: a fast-decaying low sine plus a tiny filtered noise transient.
            int s0 = (int)(at * Rate);
            float lp = 0f;
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Exp(-t / (decay * 0.4f));
                if (env < 1e-4f)
                    break;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.25f * (noise - lp);
                d[i] += amp * env * (0.8f * Mathf.Sin(2f * Mathf.PI * f * t * (1f + 0.4f * Mathf.Exp(-t / 0.01f)))
                    + 0.5f * lp * Mathf.Exp(-t / 0.006f));
            }
        }

        private static void Swish(float[] d, float at, float len, float f0, float f1, float amp, System.Random rng)
        {
            // Paper swish: band-limited noise whose brightness glides f0 → f1 under a soft hump envelope.
            int s0 = (int)(at * Rate);
            int n = (int)(len * Rate);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float u = i / (float)n;
                float fc = Mathf.Lerp(f0, f1, u);
                float a = Mathf.Clamp01(2f * Mathf.PI * fc / Rate);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (noise - lp);
                lp2 += 0.02f * (lp - lp2);
                float env = Mathf.Sin(Mathf.PI * u);
                d[s0 + i] += amp * env * env * (lp - lp2) * 2f;
            }
        }

        private static void Glide(float[] d, float at, float len, float f0, float f1, float amp, float tremolo)
        {
            int s0 = (int)(at * Rate);
            int n = (int)(len * Rate);
            float phase = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float u = i / (float)n;
                float t = i / (float)Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u) / Rate;
                float env = Mathf.Min(1f, t / 0.03f) * (1f - u) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * tremolo * t));
                d[s0 + i] += amp * env * (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase));
            }
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            foreach (float v in d)
                max = Mathf.Max(max, Mathf.Abs(v));
            if (max < 1e-6f)
                return;
            float k = peak / max;
            for (int i = 0; i < d.Length; i++)
                d[i] *= k;
        }
    }
}
