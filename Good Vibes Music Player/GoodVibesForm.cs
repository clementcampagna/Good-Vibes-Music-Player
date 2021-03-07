using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Good_Vibes_Music_Player
{
	public partial class GoodVibesMainForm : Form
	{
		[DllImport( "Gdi32.dll", EntryPoint = "CreateRoundRectRgn" )]
		private static extern IntPtr CreateRoundRectRgn
		(
			int nLeftRect,      // x-coordinate of the upper-left corner
			int nTopRect,       // y-coordinate of the upper-left corner
			int nRightRect,     // x-coordinate of the lower-right corner
			int nBottomRect,    // y-coordinate of the lower-right corner
			int nWidthEllipse,  // height of the ellipse
			int nHeightEllipse  // width of the ellipse
		 );

		#region Global variables
		public string[] Args;
		private string songPathOnDrive = "";
		private string lastSongPlayed = "";
		private readonly string[] fileExts = { ".mp3", ".m4a", ".m4v", ".wav", ".wma", ".mp4", ".avi", ".mov", ".wmv", ".asf", ".3g2", ".3gp", ".3gp2", ".3gpp", ".aac", ".adts", ".sami", ".smi" };
		private bool isSongCurrentlyPlaying;
		private bool isSongOnRepeatEnabled;
		private bool isShufflingEnabled;
		private IWavePlayer waveOutDevice;
		private MediaFoundationReader audioFileReader;
		private static readonly string APPDATA_PATH = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );   // AppData folder
		private static readonly string CFGFOLDER_PATH = Path.Combine( APPDATA_PATH, "Good Vibes Music Player" );                // Path of program config folder
		private static readonly string CFGFILE_PATH = Path.Combine( CFGFOLDER_PATH, "config.ini" );                             // Path of config.ini file
		private readonly string[] CFG_STR_DELIM = new string[] { " = " };                                                       // Config file string delimiter
		private readonly UserPreferences userPreferences = new UserPreferences();                                               // Holds settings of Good Vibe Music Player
		private int indexOfSongCurrentlyPlaying = -1;
		#endregion

		#region Constructor
		public GoodVibesMainForm()
		{
			InitializeComponent();

			// Rounds the winform's corners
			Region = Region.FromHrgn( CreateRoundRectRgn( 0, 0, Width, Height, 15, 15 ) );

			// Loads the application's settings
			LoadConfigFile();
		}
		#endregion

		#region GoodVibesForm_Load( object sender, EventArgs e )
		private void GoodVibesForm_Load( object sender, EventArgs e )
		{
			// The single-instance code is going to save the command line arguments in this member variable before opening the first instance of the app.
			if( this.Args != null )
			{
				ProcessParameters( null, this.Args );
				this.Args = null;
			}
		}
		#endregion

		public delegate void ProcessParametersDelegate( object sender, string[] args );

		#region ProcessParameters( object sender, string[] args )
		public void ProcessParameters( object sender, string[] args )
		{
			// Adds songs to the playlist and starts playing the first one if arguments are being parsed to this application
			if( args != null && args.Length != 0 )
			{
				songPathOnDrive = args[ 0 ];

				foreach( String file in args )
				{
					playlistDataGridView.Rows.Add( Path.GetFileNameWithoutExtension( file ), file );
				}

				if( !isSongCurrentlyPlaying )
				{
					// Selects the last song that's just been added and that is going to be played next in the playlist
					if( playlistDataGridView.Rows.Count > 0 )
					{
						playlistDataGridView.ClearSelection();
						playlistDataGridView.Rows[ playlistDataGridView.Rows.Count - 1 ].Selected = true;
					}

					if( LoadTrackInformation() )
						PlayTrack( playlistDataGridView.Rows[ playlistDataGridView.Rows.Count - 1 ].Index );
				}

				try
				{
					if( playlistDataGridView.Rows.Count - 1 != -1 )
						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
				}
				catch( Exception exc )
				{
					MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				}

				this.Show();
				this.WindowState = FormWindowState.Normal;
				this.TopMost = true;
				this.TopMost = false;
			}
		}
		#endregion

		#region CreateConfigFile()
		private void CreateConfigFile()
		{
			try
			{
				StreamWriter cfgWriter = File.CreateText(CFGFILE_PATH);
				string[] cfgDefaults = new string[] { "volume" + CFG_STR_DELIM[0] + "55" };

				foreach( string setting in cfgDefaults )
				{
					cfgWriter.WriteLine( setting );
				}

				cfgWriter.Close();
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
		}
		#endregion

		#region LoadConfigFile()
		private void LoadConfigFile()
		{
			try
			{
				// Does the config folder not exist?
				if( !Directory.Exists( CFGFOLDER_PATH ) )
					Directory.CreateDirectory( CFGFOLDER_PATH ); // Creates the Config File folder

				// Does config.ini not exist?
				if( !File.Exists( CFGFILE_PATH ) )
					CreateConfigFile(); // Creates the Config file

				ReadConfigFile();
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
		}
		#endregion

		#region ReadConfigFile()
		private void ReadConfigFile()
		{
			StreamReader cfgReader = File.OpenText( CFGFILE_PATH );

			int settingNameIndex = 0;                       // The index of the setting name
			int settingValueIndex = 1;                      // The index of the setting value
			string settingLine;                             // String that holds the text read from config file
			string[] cfgSettingArr;                         // String array that holds the split settingLine string
			List<string[]> cfgList = new List<string[]>();  // List that holds all the cfgSettingArr objects

			// Reads the config file until the end of the file
			for( int i = 0; !cfgReader.EndOfStream; i++ )
			{
				settingLine = cfgReader.ReadLine();

				// Does the line contain text?
				if( !String.IsNullOrWhiteSpace( settingLine ) )
				{
					// Splits the read text into cfgSettingArr
					cfgSettingArr = settingLine.Split( CFG_STR_DELIM, StringSplitOptions.None );

					// Adds to the cfgList
					cfgList.Add( cfgSettingArr );
				}
			}

			// Reads all the settings in the cfgList
			foreach( string[] setting in cfgList )
			{
				string settingName = setting[ settingNameIndex ];

				// Reads the setting name and update corresponding UserPreference value
				switch( settingName )
				{
					case "volume":
					{
						userPreferences.GetSetVolume = int.Parse( setting[ settingValueIndex ] );
						break;
					}

					// Default statement for invalid setting name
					default:
					{
						CreateConfigFile(); //Recreates the config file
						break;
					}
				}
			}

			volumeTrackBar.Value = userPreferences.GetSetVolume;

			cfgReader.Close();
		}
		#endregion

		#region SaveConfigFile()
		private void SaveConfigFile()
		{
			userPreferences.GetSetVolume = volumeTrackBar.Value;

			UpdateConfigFile();
		}
		#endregion

		#region UpdateConfigFile()
		private void UpdateConfigFile()
		{
			StreamWriter cfgUpdater = new StreamWriter( CFGFILE_PATH );

			List<string> cfgValues = new List<string>
			{
				"volume" + CFG_STR_DELIM[ 0 ] + volumeTrackBar.Value
			};

			foreach( string setting in cfgValues )
			{
				cfgUpdater.WriteLine( setting );
			}

			cfgUpdater.Close();
		}
		#endregion

		#region LoadTrackInformation()
		private bool LoadTrackInformation()
		{
			bool hasTrackBeenLoadedSuccessfully = true;

			// Changes the color of the song that's just been played from blue back to black in the playlist
			if( indexOfSongCurrentlyPlaying != -1 && playlistDataGridView.Rows.Count > indexOfSongCurrentlyPlaying )
				playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Cells[ 0 ].Style = new DataGridViewCellStyle { ForeColor = Color.Black };

			try
			{
				TagLib.File tagFile = TagLib.File.Create( songPathOnDrive );

				// Loads the album's cover from the song's file (if available)
				try
				{
					if( tagFile.Tag.Pictures.Length >= 1 )
					{
						var bin = tagFile.Tag.Pictures[ 0 ].Data.Data;
						albumCoverPictureBox.Image = Image.FromStream( new MemoryStream( bin ) );
					}
					else
						albumCoverPictureBox.Image = Properties.Resources.cover;
				}
				catch
				{
					albumCoverPictureBox.Image = Properties.Resources.cover;
				}

				// Loads other song's information
				if( tagFile.Tag.Title != null )
				{
					songTitleLabel.Text = tagFile.Tag.Title;
				}
				else
				{
					var songName = Path.GetFileName( songPathOnDrive );
					songTitleLabel.Text = songName;
				}

				// If the song's title is more than 27 chars long, starts songTitleTimer to make the label loop
				if( songTitleLabel.Text.Length > 27 )
				{
					songTitleLabel.Text += " - ";
					songTitleTimer.Enabled = true;
				}
				else
					songTitleTimer.Enabled = false;

				// Loads the first album's artist found for the track that is going to be played
				if( tagFile.Tag.FirstAlbumArtist != null )
					artistNameLabel.Text = tagFile.Tag.FirstAlbumArtist;
				else
					artistNameLabel.Text = "Artist unknown";

				// Pretty much the same as for the song's title, if the artist's name is more than 38 chars long, we make the label loop
				if( artistNameLabel.Text.Length > 38 )
				{
					artistNameLabel.Text += " - ";
					artistNameTimer.Enabled = true;
				}
				else
					artistNameTimer.Enabled = false;

				// Updates songTotalLengthLabel to correct length of the track that is going to be played
				var totalLengthOfTrackInSeconds = tagFile.Properties.Duration.TotalSeconds;
				TimeSpan totalLengthOfTrackInHoursMinutesAndSeconds = TimeSpan.FromSeconds( totalLengthOfTrackInSeconds );
				songTotalLengthLabel.Text = totalLengthOfTrackInHoursMinutesAndSeconds.ToString( @"h\:mm\:ss" );

				// Resets songTrackBar's position to 0
				songTrackBar.Value = 0;

				// Math.Floor is used to round down track length (in seconds) when converting from double to int
				songTrackBar.Maximum = Convert.ToInt32( Math.Floor( totalLengthOfTrackInSeconds ) );
			}
			catch
			{
				// An exception occurred while getting the track info, skipping to the next song in the playlist if there is any

				hasTrackBeenLoadedSuccessfully = false;
				int indexOfSongThatFailedLoading = -1;

				for( int i = 0; i < playlistDataGridView.Rows.Count; i++ )
				{
					if( playlistDataGridView.Rows[ i ].Cells[ 1 ].Value.ToString() == songPathOnDrive )
					{
						indexOfSongThatFailedLoading = i;
						break;
					}
				}

				if( indexOfSongThatFailedLoading != -1 )
					playlistDataGridView.Rows.RemoveAt( indexOfSongThatFailedLoading );

				if( isSongOnRepeatEnabled )
				{
					isSongOnRepeatEnabled = false;
					repeatPictureBox.Image = Properties.Resources.repeat_off;
				}

				if( indexOfSongThatFailedLoading != -1
					&& playlistDataGridView.Rows.Count >= 1
					&& indexOfSongThatFailedLoading + 1 <= playlistDataGridView.Rows.Count )
				{
					songPathOnDrive = playlistDataGridView.Rows[ indexOfSongThatFailedLoading ].Cells[ 1 ].Value.ToString();
					playlistDataGridView.ClearSelection();
					playlistDataGridView.Rows[ indexOfSongThatFailedLoading ].Selected = true;

					if( LoadTrackInformation() )
						PlayTrack( indexOfSongThatFailedLoading );
				}
				else
				{
					StopTrack();
					songPathOnDrive = "";
				}
			}

			return hasTrackBeenLoadedSuccessfully;
		}
		#endregion

		#region PlayTrack( int indexOfTrackInPlaylist )
		private bool PlayTrack( int indexOfTrackInPlaylist )
		{
			bool isSongPlayingSuccessfully = true;

			if( isSongCurrentlyPlaying )
				StopTrack();

			if( songPathOnDrive != lastSongPlayed )
			{
				if( audioFileReader != null )
					audioFileReader.Dispose();

				if( waveOutDevice != null )
					waveOutDevice.Dispose();

				lastSongPlayed = songPathOnDrive;

				try
				{
					waveOutDevice = new WaveOut();
					audioFileReader = new MediaFoundationReader( songPathOnDrive );
					waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
					waveOutDevice.Init( audioFileReader );
					waveOutDevice.Play();
				}
				catch
				{
					// Something wrong occurred while we tried to play the song
					isSongPlayingSuccessfully = false;
				}

				// Changes the icon of the play button to a pause icon
				playPictureBox.Image = Properties.Resources.pause;
			}
			else
			{
				if( songTrackBar.Value == 0 )
				{
					try
					{
						waveOutDevice = new WaveOut();
						audioFileReader = new MediaFoundationReader( songPathOnDrive );

						waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
						waveOutDevice.Init( audioFileReader );
						waveOutDevice.Play();
					}
					catch
					{
						// Something wrong occurred while trying to play the same song
						isSongCurrentlyPlaying = false;
					}

					playPictureBox.Image = Properties.Resources.pause;
				}
				else
				{
					// If we are here, it is because no new song has been selected and
					// the song's trackbar value isn't equal to zero, so we simply have to un-pause the music
					try
					{
						waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
						waveOutDevice.Play();
					}
					catch
					{
						// Something wrong occurred while trying to un-pause the music
						isSongPlayingSuccessfully = false;
					}

					playPictureBox.Image = Properties.Resources.pause;
				}
			}

			if( isSongPlayingSuccessfully )
			{
				isSongCurrentlyPlaying = true;
				playTimer.Enabled = true;
				songTrackBar.Enabled = true;

				// Changes the text color of the current song playing in the playlist
				try
				{
					if( indexOfTrackInPlaylist != -1 && playlistDataGridView.Rows.Count >= indexOfSongCurrentlyPlaying )
					{
						indexOfSongCurrentlyPlaying = indexOfTrackInPlaylist;
						playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Cells[ 0 ].Style = new DataGridViewCellStyle { ForeColor = Color.Blue };
					}
				}
				catch( Exception exc )
				{
					MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				}

				// As systray icon's text is limited to 64 chars, the following code ensures that we do not go over that limit
				if( ( songTitleLabel.Text + " (" + artistNameLabel.Text + ")" ).Length < 64 )
				{
					systrayNotifyIcon.Text = songTitleLabel.Text + " (" + artistNameLabel.Text + ")";
				}
				else
				{
					// Otherwise, we truncate the string to the first 64 chars of the song title + artist name
					string tempText = songTitleLabel.Text + " (" + artistNameLabel.Text + ")";
					tempText = tempText.Substring( 0, 63 );
					systrayNotifyIcon.Text = tempText;
				}
			}
			else
			{
				isSongCurrentlyPlaying = false;
				playTimer.Enabled = false;
				songTrackBar.Enabled = false;
			}

			return isSongCurrentlyPlaying;
		}
		#endregion

		#region PauseTrack()
		private void PauseTrack()
		{
			try
			{
				waveOutDevice.Stop();
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}

			songTrackBar.Enabled = false;
			playTimer.Enabled = false;
			isSongCurrentlyPlaying = false;

			playPictureBox.Image = Properties.Resources.play;
		}
		#endregion

		#region StopTrack()
		private void StopTrack()
		{
			try
			{
				waveOutDevice.Stop();
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}

			songTrackBar.Enabled = false;
			playTimer.Enabled = false;
			isSongCurrentlyPlaying = false;

			// Restores the play button's icon from pause to play icon
			playPictureBox.Image = Properties.Resources.play;

			// Resets songTrackBar position to 0
			songTrackBar.Value = 0;

			if( playlistDataGridView.Rows.Count > 0 )
			{
				// Changes the color of the song that's just been played from blue back to black in the playlist
				if( indexOfSongCurrentlyPlaying != -1 && playlistDataGridView.Rows.Count > indexOfSongCurrentlyPlaying )
					playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Cells[ 0 ].Style = new DataGridViewCellStyle { ForeColor = Color.Black };
			}
		}
		#endregion

		#region AddFilesToPlaylist( bool playNow )
		// playNow is a boolean paramater that can either be switched to true or false depending on if the
		// first selected song has to be played immediately or simply added to the playlist.
		private void AddFilesToPlaylist( bool playNow )
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "Browse Media Files",

				Multiselect = true,
				CheckFileExists = true,
				CheckPathExists = true,

				Filter = "Media files (*.mp3; *.m4a; *.m4v; *.wav; *.wma; *.mp4; *.avi; *.mov; *.wmv; *.asf; *.3g2; *.3gp; *.3gp2; *.3gpp; *.aac; *.adts; *.sami; *.smi)|*.mp3;*.m4a;*.m4v;*.wav;*.wma;*.mp4;*.avi;*.mov;*.wmv;*.asf;*.3g2;*.3gp;*.3gp2;*.3gpp;*.aac;*.adts;*.sami;*.smi|" +
						 "All files (*.*)|*.*",
				FilterIndex = 1,
				RestoreDirectory = true,
			};

			if( openFileDialog.ShowDialog() == DialogResult.OK )
			{
				if( playNow )
				{
					playlistDataGridView.Rows.Clear();
					songPathOnDrive = openFileDialog.FileName;

					foreach( String file in openFileDialog.FileNames )
					{
						playlistDataGridView.Rows.Add( Path.GetFileNameWithoutExtension( file ), file );
					}

					if( LoadTrackInformation() )
						PlayTrack( playlistDataGridView.CurrentCell.RowIndex );
				}
				else
				{
					foreach( String file in openFileDialog.FileNames )
					{
						playlistDataGridView.Rows.Add( Path.GetFileNameWithoutExtension( file ), file );
					}
				}

				if( playlistDataGridView.Rows.Count - 1 != -1 )
					playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
			}
		}
		#endregion

		#region AddFolderToPlaylist( bool playNow )
		// playNow is a boolean paramater that can either be switched to true or false depending on if the
		// first selected song has to be played immediately or simply added to the playlist.
		private void AddFolderToPlaylist( bool playNow )
		{
			using CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				RestoreDirectory = true,
				IsFolderPicker = true
			};

			if( dialog.ShowDialog() == CommonFileDialogResult.Ok )
			{
				String[] files = Directory.GetFiles( @dialog.FileName );

				if( playNow )
					playlistDataGridView.Rows.Clear();

				for( int i = 0; i < files.Length; i++ )
				{
					FileInfo file = new FileInfo(files[i]);

					if( fileExts.Contains( file.Extension.ToLower() ) )
						playlistDataGridView.Rows.Add( Path.GetFileNameWithoutExtension( file.Name ), file.FullName );
				}

				if( playNow )
				{
					try
					{
						songPathOnDrive = playlistDataGridView.Rows[ playlistDataGridView.CurrentCell.RowIndex ].Cells[ 1 ].Value.ToString();

						if( LoadTrackInformation() )
							PlayTrack( playlistDataGridView.CurrentCell.RowIndex );
					}
					catch( Exception exc )
					{
						MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
					}
				}
				else
				{
					if( playlistDataGridView.Rows.Count - 1 != -1 )
						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
				}
			}
		}
		#endregion

		#region DeleteSongsFromPlaylist()
		private void DeleteSongsFromPlaylist()
		{
			try
			{
				foreach( DataGridViewRow r in playlistDataGridView.SelectedRows )
				{
					if( !r.IsNewRow )
					{
						if( r.Index < indexOfSongCurrentlyPlaying )
							indexOfSongCurrentlyPlaying--;

						playlistDataGridView.Rows.RemoveAt( r.Index );
					}
				}

				if( playlistDataGridView.Rows.Count > 0 )
					playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.CurrentCell.RowIndex;
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
		}
		#endregion

		#region TopBarLogoPictureBox_MouseDown( object sender, MouseEventArgs e )
		private void TopBarLogoPictureBox_MouseDown( object sender, MouseEventArgs e )
		{
			// Opens the context menu on left click
			if( e.Button == MouseButtons.Left )
				playContextMenuStrip.Show( this.PointToScreen( e.Location ) );
		}
		#endregion

		#region TopBarMinimizePictureBox_Click( object sender, EventArgs e )
		private void TopBarMinimizePictureBox_Click( object sender, EventArgs e )
		{
			this.Hide();
		}
		#endregion

		#region TopBarClosePictureBox_Click( object sender, EventArgs e )
		private void TopBarClosePictureBox_Click( object sender, EventArgs e )
		{
			this.Close();
		}
		#endregion

		#region PlayFilesToolStripMenuItem_Click( object sender, EventArgs e )
		private void PlayFilesToolStripMenuItem_Click( object sender, EventArgs e )
		{
			AddFilesToPlaylist( true );
		}
		#endregion

		#region PlayDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
		private void PlayDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
		{
			AddFolderToPlaylist( true );
		}
		#endregion

		#region AboutThisMusicPlayerToolStripMenuItem_Click( object sender, EventArgs e )
		private void AboutThisMusicPlayerToolStripMenuItem_Click( object sender, EventArgs e )
		{
			string message = "Made by Clément Campagna under MIT License, March 2021.\nhttps://clementcampagna.com";
			string title = "Good Vibes Music Player v1.1";
			MessageBox.Show( message, title );
		}
		#endregion

		#region SongTrackBar_ValueChanged( object sender, EventArgs e )
		private void SongTrackBar_ValueChanged( object sender, EventArgs e )
		{
			// Updates songCurrentPositionLabel
			TimeSpan totalLengthOfTrackInHoursMinutesAndSeconds = TimeSpan.FromSeconds(songTrackBar.Value);
			songCurrentPositionLabel.Text = totalLengthOfTrackInHoursMinutesAndSeconds.ToString( @"h\:mm\:ss" );
		}
		#endregion

		#region AdjustTrackPosition()
		private void AdjustTrackPosition()
		{
			playTimer.Enabled = false;

			try
			{
				audioFileReader.SetPosition( TimeSpan.FromSeconds( songTrackBar.Value ) );
			}
			catch( Exception exc )
			{
				MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}

			playTimer.Enabled = true;
		}
		#endregion

		#region SongTrackBar_KeyUp( object sender, KeyEventArgs e )
		private void SongTrackBar_KeyUp( object sender, KeyEventArgs e )
		{
			AdjustTrackPosition();
		}
		#endregion

		#region SongTrackBar_MouseUp( object sender, MouseEventArgs e )
		private void SongTrackBar_MouseUp( object sender, MouseEventArgs e )
		{
			AdjustTrackPosition();
		}
		#endregion

		#region SongTrackBar_MouseWheel( object sender, MouseEventArgs e )
		private void SongTrackBar_MouseWheel( object sender, MouseEventArgs e )
		{
			AdjustTrackPosition();
		}
		#endregion

		#region PlayTimer_Tick( object sender, EventArgs e )
		private void PlayTimer_Tick( object sender, EventArgs e )
		{
			if( songTrackBar.Value < songTrackBar.Maximum )
			{
				songTrackBar.Value += 1;
			}
			else
			{
				if( !isSongOnRepeatEnabled )
				{
					if( isShufflingEnabled )
					{
						// Changes the color of the song that's just been played from blue back to black in the playlist
						if( indexOfSongCurrentlyPlaying != -1 && playlistDataGridView.Rows.Count > indexOfSongCurrentlyPlaying )
							playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Cells[ 0 ].Style = new DataGridViewCellStyle { ForeColor = Color.Black };

						// Generates random index from 0 to playlist count
						Random rnd = new Random();
						indexOfSongCurrentlyPlaying = rnd.Next( 0, playlistDataGridView.Rows.Count );
					}

					if( ( playlistDataGridView.Rows.Count >= 1 &&
						  indexOfSongCurrentlyPlaying + 1 < playlistDataGridView.Rows.Count ) ||
						  isShufflingEnabled && playlistDataGridView.Rows.Count >= 1  )
					{
						if( isShufflingEnabled )
						{
							songPathOnDrive = playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Cells[ 1 ].Value.ToString();
							playlistDataGridView.ClearSelection();
							playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Selected = true;
						}
						else
						{
							songPathOnDrive = playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying + 1 ].Cells[ 1 ].Value.ToString();
							playlistDataGridView.ClearSelection();
							playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying + 1 ].Selected = true;
						}

						if( LoadTrackInformation() )
							PlayTrack( playlistDataGridView.SelectedRows[ 0 ].Index );
					}
					else
					{
						if( indexOfSongCurrentlyPlaying == -1 && playlistDataGridView.Rows.Count >= 1 )
						{
							try
							{
								songPathOnDrive = playlistDataGridView.Rows[ 0 ].Cells[ 1 ].Value.ToString();
								playlistDataGridView.ClearSelection();
								playlistDataGridView.Rows[ 0 ].Selected = true;

								if( LoadTrackInformation() )
									PlayTrack( playlistDataGridView.SelectedRows[ 0 ].Index );
							}
							catch( Exception exc )
							{
								MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
							}
						}
						else
							StopTrack();
					}
				}
				else
					if( LoadTrackInformation() )
						PlayTrack( indexOfSongCurrentlyPlaying );
			}
		}
		#endregion

		#region SongTitleTimer_Tick( object sender, EventArgs e )
		private void SongTitleTimer_Tick( object sender, EventArgs e )
		{
			// Adds horizontal scrolling effect to the song title's label
			songTitleLabel.Text = songTitleLabel.Text.Substring( 1, songTitleLabel.Text.Length - 1 ) + songTitleLabel.Text.Substring( 0, 1 );
		}
		#endregion

		#region ArtistNameTimer_Tick( object sender, EventArgs e )
		private void ArtistNameTimer_Tick( object sender, EventArgs e )
		{
			// Adds horizontal scrolling effect to the artist name's label
			artistNameLabel.Text = artistNameLabel.Text.Substring( 1, artistNameLabel.Text.Length - 1 ) + artistNameLabel.Text.Substring( 0, 1 );
		}
		#endregion

		#region RepeatPictureBox_Click( object sender, EventArgs e )
		private void RepeatPictureBox_Click( object sender, EventArgs e )
		{
			if( !isSongOnRepeatEnabled )
			{
				isSongOnRepeatEnabled = true;
				repeatPictureBox.Image = Properties.Resources.repeat_on;
			}
			else
			{
				isSongOnRepeatEnabled = false;
				repeatPictureBox.Image = Properties.Resources.repeat_off;
			}
		}
		#endregion

		#region PreviousPictureBox_Click( object sender, EventArgs e )
		private void PreviousPictureBox_Click( object sender, EventArgs e )
		{
			if( playlistDataGridView.Rows.Count >= 1 && indexOfSongCurrentlyPlaying - 1 >= 0 )
			{
				songPathOnDrive = playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying - 1 ].Cells[ 1 ].Value.ToString();
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying - 1 ].Selected = true;

				if( LoadTrackInformation() )
					PlayTrack( indexOfSongCurrentlyPlaying - 1 );
			}
		}
		#endregion

		#region PlayPictureBox_MouseDown( object sender, MouseEventArgs e )
		private void PlayPictureBox_MouseDown( object sender, MouseEventArgs e )
		{
			// Always opens the context menu on right click, or on left click if no song is currently playing
			if( e.Button == MouseButtons.Right )
			{
				playContextMenuStrip.Show( this.PointToScreen( playPictureBox.Location ) );
			}
			else
			{
				if( !isSongCurrentlyPlaying )
				{
					if( !string.IsNullOrEmpty( songPathOnDrive ) )
						PlayTrack( -1 );
					else
					{
						if( playlistDataGridView.Rows.Count > 0 )
						{
							songPathOnDrive = playlistDataGridView.Rows[ playlistDataGridView.CurrentCell.RowIndex ].Cells[ 1 ].Value.ToString();

							if( LoadTrackInformation() )
								PlayTrack( playlistDataGridView.CurrentCell.RowIndex );
						}
						else
							playContextMenuStrip.Show( this.PointToScreen( playPictureBox.Location ) );
					}
				}
				else
				{
					PauseTrack();
				}
			}
		}
		#endregion

		#region NextPictureBox_Click( object sender, EventArgs e )
		private void NextPictureBox_Click( object sender, EventArgs e )
		{
			if( playlistDataGridView.Rows.Count >= 1 && indexOfSongCurrentlyPlaying + 1 < playlistDataGridView.Rows.Count )
			{
				songPathOnDrive = playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying + 1 ].Cells[ 1 ].Value.ToString();
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying + 1 ].Selected = true;

				if( LoadTrackInformation() )
					PlayTrack( indexOfSongCurrentlyPlaying + 1 );
			}
		}
		#endregion

		#region ShufflePictureBox_Click( object sender, EventArgs e )
		private void ShufflePictureBox_Click( object sender, EventArgs e )
		{
			if( !isShufflingEnabled )
			{
				isShufflingEnabled = true;
				shufflePictureBox.Image = Properties.Resources.shuffle_on;
			}
			else
			{
				isShufflingEnabled = false;
				shufflePictureBox.Image = Properties.Resources.shuffle_off;
			}
		}
		#endregion

		#region VolumeTrackBar_ValueChanged( object sender, EventArgs e )
		private void VolumeTrackBar_ValueChanged( object sender, EventArgs e )
		{
			if( waveOutDevice != null )
				waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
		}
		#endregion

		#region UpPictureBox_Click( object sender, EventArgs e )
		private void UpPictureBox_Click( object sender, EventArgs e )
		{
			DataGridView dgv = playlistDataGridView;

			if( dgv.Rows.Count > 1 )
			{
				try
				{
					// Gets the index of the row for the selected cell
					int rowIndex = dgv.SelectedCells[ 0 ].OwningRow.Index;

					if( rowIndex == 0 )
						return;

					// Gets the index of the column for the selected cell
					int colIndex = dgv.SelectedCells[ 0 ].OwningColumn.Index;

					DataGridViewRow selectedRow = dgv.Rows[rowIndex];
					dgv.Rows.Remove( selectedRow );
					dgv.Rows.Insert( rowIndex - 1, selectedRow );
					dgv.ClearSelection();
					dgv.Rows[ rowIndex - 1 ].Cells[ colIndex ].Selected = true;

					if( indexOfSongCurrentlyPlaying > 0 && indexOfSongCurrentlyPlaying == rowIndex )
						indexOfSongCurrentlyPlaying--;
					else if( rowIndex - 1 == indexOfSongCurrentlyPlaying )
						indexOfSongCurrentlyPlaying++;

					if( rowIndex > 0 )
						playlistDataGridView.FirstDisplayedScrollingRowIndex = dgv.Rows[ rowIndex - 1 ].Cells[ colIndex ].RowIndex;
				}
				catch( Exception exc )
				{
					MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				}
			}
		}
		#endregion

		#region DownPictureBox_Click( object sender, EventArgs e )
		private void DownPictureBox_Click( object sender, EventArgs e )
		{
			DataGridView dgv = playlistDataGridView;

			if( dgv.Rows.Count > 1 )
			{
				try
				{
					// Gets the index of the row for the selected cell
					int rowIndex = dgv.SelectedCells[ 0 ].OwningRow.Index;

					if( rowIndex == dgv.Rows.Count - 1 )
						return;

					// Gets the index of the column for the selected cell
					int colIndex = dgv.SelectedCells[ 0 ].OwningColumn.Index;
					DataGridViewRow selectedRow = dgv.Rows[rowIndex];

					dgv.Rows.Remove( selectedRow );
					dgv.Rows.Insert( rowIndex + 1, selectedRow );
					dgv.ClearSelection();
					dgv.Rows[ rowIndex + 1 ].Cells[ colIndex ].Selected = true;

					if( rowIndex == indexOfSongCurrentlyPlaying )
						indexOfSongCurrentlyPlaying++;
					else if( rowIndex + 1 == indexOfSongCurrentlyPlaying )
						indexOfSongCurrentlyPlaying--;

					if( rowIndex > 0 )
						playlistDataGridView.FirstDisplayedScrollingRowIndex = dgv.Rows[ rowIndex + 1 ].Cells[ colIndex ].RowIndex;
				}
				catch( Exception exc )
				{
					MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				}
			}
		}
		#endregion

		#region PlaylistDataGridView_CellDoubleClick( object sender, DataGridViewCellEventArgs e )
		private void PlaylistDataGridView_CellDoubleClick( object sender, DataGridViewCellEventArgs e )
		{
			if( e.RowIndex >= 0 )
			{
				DataGridViewRow row = playlistDataGridView.Rows[e.RowIndex];
				songPathOnDrive = row.Cells[ 1 ].Value.ToString();

				if( LoadTrackInformation() )
					PlayTrack( e.RowIndex );
			}
		}
		#endregion

		#region PlaylistDataGridView_KeyDown( object sender, KeyEventArgs e )
		private void PlaylistDataGridView_KeyDown( object sender, KeyEventArgs e )
		{
			if( e.KeyCode == Keys.Delete )
				DeleteSongsFromPlaylist();
		}
		#endregion

		#region FindCurrentTrackInPlaylistPictureBox_Click( object sender, EventArgs e )
		private void FindCurrentTrackInPlaylistPictureBox_Click( object sender, EventArgs e )
		{
			if( indexOfSongCurrentlyPlaying != -1 )
			{
				playlistDataGridView.FirstDisplayedScrollingRowIndex = indexOfSongCurrentlyPlaying;
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[ indexOfSongCurrentlyPlaying ].Selected = true;
			}
		}
		#endregion

		#region MinusPictureBox_Click( object sender, EventArgs e )
		private void MinusPictureBox_Click( object sender, EventArgs e )
		{
			DeleteSongsFromPlaylist();
		}
		#endregion

		#region PlusPictureBox_MouseDown( object sender, MouseEventArgs e )
		private void PlusPictureBox_MouseDown( object sender, MouseEventArgs e )
		{
			// Opens the context menu on left click
			if( e.Button == MouseButtons.Left )
				plusContextMenuStrip.Show( this.PointToScreen( plusPictureBox.Location ) );
		}
		#endregion

		#region AddFilesToQueueToolStripMenuItem_Click( object sender, EventArgs e )
		private void AddFilesToQueueToolStripMenuItem_Click( object sender, EventArgs e )
		{
			AddFilesToPlaylist( false );
		}
		#endregion

		#region AddDirectoryToQueueToolStripMenuItem_Click( object sender, EventArgs e )
		private void AddDirectoryToQueueToolStripMenuItem_Click( object sender, EventArgs e )
		{
			AddFolderToPlaylist( false );
		}
		#endregion

		#region LoadPlaylistFromPlusMenuToolStripMenuItem_Click( object sender, EventArgs e )
		private void LoadPlaylistFromPlusMenuToolStripMenuItem_Click( object sender, EventArgs e )
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "Load Playlist",

				Multiselect = false,
				CheckFileExists = true,
				CheckPathExists = true,

				Filter = "Good Vibes Music Player Playlists (*.gvmp)|*.gvmp",
				FilterIndex = 1,
				RestoreDirectory = true,
			};

			if( openFileDialog.ShowDialog() == DialogResult.OK )
			{
				try
				{
					ReadPlaylistFiles playlist = new ReadPlaylistFiles( openFileDialog.FileName );

					try
					{
						DataTable playlistDataTable = playlist.readPlaylistFile;

						foreach( DataRow row in playlistDataTable.Rows )
						{
							playlistDataGridView.Rows.Add( row.ItemArray );
						}

						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
					}
					catch
					{
						throw;
					}
				}
				catch( Exception exc )
				{
					MessageBox.Show( exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				}
			}
		}
		#endregion

		#region SavePlaylistFromPlusMenuToolStripMenuItem_Click( object sender, EventArgs e )
		private void SavePlaylistFromPlusMenuToolStripMenuItem_Click( object sender, EventArgs e )
		{
			SaveFileDialog savePlaylistDialog = new SaveFileDialog
			{
				Title = "Save Playlist",
				CheckPathExists = true,
				DefaultExt = "gvmp",
				Filter = "Good Vibes Music Player Playlists (*.gvmp)|*.gvmp",
				FilterIndex = 1,
				RestoreDirectory = true
			};

			if( savePlaylistDialog.ShowDialog() == DialogResult.OK )
			{
				var sb = new StringBuilder();
				var headers = playlistDataGridView.Columns.Cast<DataGridViewColumn>();

				sb.AppendLine( string.Join( ",", headers.Select( column => "\"" + column.HeaderText + "\"" ) ) ); headers.Select( column => "\"" + column.HeaderText + "\"" ).ToArray();

				foreach( DataGridViewRow row in playlistDataGridView.Rows )
				{
					var cells = row.Cells.Cast<DataGridViewCell>();
					sb.AppendLine( string.Join( ",", cells.Select( cell => "\"" + ( cell.ColumnIndex == 0 ? cell.FormattedValue : cell.Value ) + "\"" ).ToArray() ) );
				}

				using( StreamWriter file = new StreamWriter( savePlaylistDialog.FileName ) )
				{
					file.WriteLine( sb.ToString() );
				}
			}
		}
		#endregion

		#region SystrayNotifyIcon_MouseClick( object sender, MouseEventArgs e )
		private void SystrayNotifyIcon_MouseClick( object sender, MouseEventArgs e )
		{
			this.Show();
			this.WindowState = FormWindowState.Normal;
			this.TopMost = true;
			this.TopMost = false;
		}
		#endregion

		#region GoodVibesForm_FormClosing( object sender, FormClosingEventArgs e )
		private void GoodVibesForm_FormClosing( object sender, FormClosingEventArgs e )
		{
			if( isSongCurrentlyPlaying )
				StopTrack();

			if( audioFileReader != null )
				audioFileReader.Dispose();

			if( waveOutDevice != null )
				waveOutDevice.Dispose();

			SaveConfigFile();
		}
		#endregion
	}
}
