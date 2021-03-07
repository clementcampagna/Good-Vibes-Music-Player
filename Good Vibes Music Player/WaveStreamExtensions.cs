using NAudio.Wave;
using System;

namespace Good_Vibes_Music_Player
{
	static class WaveStreamExtensions
	{
		#region SetPosition( this WaveStream strm, long position )
		// Sets position of WaveStream to nearest block to supplied position
		public static void SetPosition( this WaveStream strm, long position )
		{
			// distance from block boundary (may be 0)
			long adj = position % strm.WaveFormat.BlockAlign;

			// adjusts position to boundary and clamp to valid range
			long newPos = Math.Max( 0, Math.Min( strm.Length, position - adj ) );

			// sets playback position
			strm.Position = newPos;
		}
		#endregion

		#region SetPosition( this WaveStream strm, double seconds )
		// Sets playback position of WaveStream by seconds
		public static void SetPosition( this WaveStream strm, double seconds )
		{
			strm.SetPosition( (long)( seconds * strm.WaveFormat.AverageBytesPerSecond ) );
		}
		#endregion

		#region SetPosition( this WaveStream strm, TimeSpan time )
		// Sets playback position of WaveStream by time (as a TimeSpan)
		public static void SetPosition( this WaveStream strm, TimeSpan time )
		{
			strm.SetPosition( time.TotalSeconds );
		}
		#endregion
	}
}
