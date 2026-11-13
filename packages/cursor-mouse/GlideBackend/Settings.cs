#nullable disable
// Adapted from qemvix/Glide at the owner's request. See docs/GLIDE_INTEGRATION.md.
using System;
using Nexus.Core;
namespace Nexus.Cursor.GlideBackend
{
    internal sealed class Settings
    {
        internal bool Enabled = true, NativeTouchpad = true, PauseFullscreen = true, Invert;
        internal int GlideMilliseconds = 320, SpeedPercent = 100;
        internal string ExcludedApps = "";
        internal ScrollProfile[] Profiles = Array.Empty<ScrollProfile>();
        internal Settings Copy() { var copy = (Settings)MemberwiseClone(); copy.Profiles = (ScrollProfile[])Profiles.Clone(); return copy; }
        internal void Validate()
        {
            GlideMilliseconds = Math.Clamp(GlideMilliseconds, 100, 700); SpeedPercent = Math.Clamp(SpeedPercent, 25, 250);
            ExcludedApps ??= ""; Profiles ??= Array.Empty<ScrollProfile>();
        }
    }
    // Finite, overlapping ease-out pulses preserve distance and avoid an endless tail.
    internal sealed class Motion
    {
        private sealed class Pulse { internal double Amount, Start, Duration, Progress; }
        private readonly System.Collections.Generic.List<Pulse> pulses = new System.Collections.Generic.List<Pulse>();
        private double carry; private int direction;
        internal bool Active { get { return pulses.Count != 0 || Math.Abs(carry) >= 0.5; } }
        internal void Clear() { pulses.Clear(); carry = 0; direction = 0; }
        internal void Add(double amount, double now, int duration)
        {
            int next = Math.Sign(amount); if (next == 0) return;
            if (direction != 0 && direction != next) Clear(); direction = next;
            if (pulses.Count >= 512) { pulses[pulses.Count - 1].Amount += amount; return; }
            pulses.Add(new Pulse { Amount = amount, Start = now, Duration = duration });
        }
        internal int Step(double now)
        {
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                Pulse pulse = pulses[i]; double t = Math.Max(0, Math.Min(1, (now - pulse.Start) / pulse.Duration));
                double eased = 1 - Math.Pow(1 - t, 3); carry += pulse.Amount * (eased - pulse.Progress); pulse.Progress = eased;
                if (t >= 1) pulses.RemoveAt(i);
            }
            int output = pulses.Count == 0 ? (int)Math.Round(carry, MidpointRounding.AwayFromZero) : (int)carry;
            output = Math.Max(-30000, Math.Min(30000, output)); carry -= output;
            if (pulses.Count == 0 && Math.Abs(carry) < 0.5) carry = 0; return output;
        }
    }
}
