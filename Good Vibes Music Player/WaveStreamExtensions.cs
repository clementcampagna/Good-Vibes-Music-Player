using NAudio.Wave;
using System;

namespace Good_Vibes_Music_Player
{
	public static class WaveStreamExtensions
	{
		// Sets position of WaveStream to nearest block to supplied position
		public static void SetPosition(this WaveStream strm, long position)
		{
			// distance from block boundary (may be 0)
			long adj = position % strm.WaveFormat.BlockAlign;
			// adjusts position to boundary and clamp to valid range
			long newPos = Math.Max(0, Math.Min(strm.Length, position - adj));
			// sets playback position
			strm.Position = newPos;
		}

		// Sets playback position of WaveStream by seconds
		public static void SetPosition(this WaveStream strm, double seconds)
		{
			strm.SetPosition((long)(seconds * strm.WaveFormat.AverageBytesPerSecond));
		}

		// Sets playback position of WaveStream by time (as a TimeSpan)
		public static void SetPosition(this WaveStream strm, TimeSpan time)
		{
			strm.SetPosition(time.TotalSeconds);
		}

		// Sets playback position of WaveStream relative to current position
		public static void Seek(this WaveStream strm, double offset)
		{
			strm.SetPosition(strm.Position + (long)(offset * strm.WaveFormat.AverageBytesPerSecond));
		}
	}
}
