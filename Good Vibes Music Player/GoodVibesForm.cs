using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using NAudio.Wave;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace Good_Vibes_Music_Player
{
	public partial class GoodVibesMainForm : Form
	{
		[DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
		private static extern IntPtr CreateRoundRectRgn
		(
			int nLeftRect, // x-coordinate of upper-left corner
			int nTopRect, // y-coordinate of upper-left corner
			int nRightRect, // x-coordinate of lower-right corner
			int nBottomRect, // y-coordinate of lower-right corner
			int nWidthEllipse, // height of ellipse
			int nHeightEllipse // width of ellipse
		 );

		// Global variables
		public string[] Args;
		private string songPathOnDrive = "";
		private string lastSongPlayed = "";
		readonly private string[] fileExts = { ".mp3", ".m4a", ".m4v", ".wav", ".wma", ".mp4", ".avi", ".mov", ".wmv", ".asf", ".3g2", ".3gp", ".3gp2", ".3gpp", ".aac", ".adts", ".sami", ".smi" };
		private bool isSongCurrentlyPlaying = false;
		private bool isSongOnRepeatEnabled = false;
		private bool isShufflingEnabled = false;
		private IWavePlayer waveOutDevice;
		private MediaFoundationReader audioFileReader;
		private static readonly string APPDATA_PATH = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // AppData folder
		private static readonly string CFGFOLDER_PATH = Path.Combine(APPDATA_PATH, "Good Vibes Music Player"); // Path for program config folder
		private static readonly string CFGFILE_PATH = Path.Combine(CFGFOLDER_PATH, "config.ini"); // Path for config.ini file
		private readonly string[] CFG_STR_DELIM = new string[] { " = " }; // Config file string delimiter
		private readonly UserPreferences userPreferences = new UserPreferences(); // Holds settings for program

		public GoodVibesMainForm()
		{
			InitializeComponent();

			// Rounds winform's corners
			Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 15, 15));

			// Loads application configuration
			LoadConfigFile();
		}

		private void GoodVibesForm_Load(object sender, EventArgs e)
		{
			// The single-instance code is going to save the command line  arguments in this member variable before
			// opening the first instance of the app.
			if (this.Args != null)
			{
				ProcessParameters(null, this.Args);
				this.Args = null;
			}
		}

		public delegate void ProcessParametersDelegate(object sender, string[] args);
		public void ProcessParameters(object sender, string[] args)
		{
			// The form has loaded, and initialization will have been done.

			// Adds songs to the playlist and starts playing the first one if arguments are being parsed to this application
			if (args != null && args.Length != 0)
			{
				songPathOnDrive = args[0];

				foreach (String file in args)
				{
					playlistDataGridView.Rows.Add(Path.GetFileNameWithoutExtension(file), file);
				}

				if (!isSongCurrentlyPlaying)
				{
					// Changes the color of the song that's just been played from blue back to black in the playlist data grid view.
					// This is very important as GetIndexOfRowCurrentlyPlaying() declared below uses the forecolor parameter to find
					// the index of the song that is currently being played.
					int decolorIndex = GetIndexOfRowCurrentlyPlaying();
					if (decolorIndex != -1)
						playlistDataGridView.Rows[decolorIndex].Cells[0].Style = new DataGridViewCellStyle { ForeColor = Color.Black };
					else
						DecolorAllCells();

					// Selects the last song that's just been added and that is going to be played now in the playlist data grid view
					if (playlistDataGridView.Rows.Count > 0)
					{
						playlistDataGridView.ClearSelection();
						playlistDataGridView.Rows[playlistDataGridView.Rows.Count - 1].Selected = true;
					}

					if (LoadTrackInformation())
						PlayTrack(playlistDataGridView.Rows[playlistDataGridView.Rows.Count - 1].Index);
				}

				try
				{
					if (playlistDataGridView.Rows.Count - 1 != -1)
						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
				}
				catch { }

				this.Show();
				this.WindowState = FormWindowState.Normal;
				this.TopMost = true;
				this.TopMost = false;
			}
		}

		private void CreateConfigFile()
		{
			StreamWriter cfgWriter = File.CreateText(CFGFILE_PATH);

			string[] cfgDefaults = new string[] { "volume" + CFG_STR_DELIM[0] + "55" };

			foreach (string setting in cfgDefaults)
			{
				cfgWriter.WriteLine(setting);
			}

			cfgWriter.Close();
		}

		private void LoadConfigFile()
		{
			// Does the config folder not exist?
			if (!Directory.Exists(CFGFOLDER_PATH))
				Directory.CreateDirectory(CFGFOLDER_PATH); // Creates the Config File folder

			// Does config.ini not exist?
			if (!File.Exists(CFGFILE_PATH))
				CreateConfigFile(); // Creates the Config file

			ReadConfigFile();
		}

		private void ReadConfigFile()
		{
			StreamReader cfgReader = File.OpenText(CFGFILE_PATH);

			int settingNameIndex = 0;               // The index of the setting name
			int settingValueIndex = 1;              // The index of the setting value
			string settingLine;                     // String that holds the text read from config file
			string[] cfgSettingArr = new string[2]; // String array that holds the split settingLine string
			List<string[]> cfgList = new List<string[]>(); // List that holds all the cfgSettingArr objects

			// Reads the config file until the end of the file
			for (int i = 0; !cfgReader.EndOfStream; i++)
			{
				settingLine = cfgReader.ReadLine();

				// Does the line contain text?
				if (!String.IsNullOrWhiteSpace(settingLine))
				{
					// Splits the read text into cfgSettingArr
					cfgSettingArr = settingLine.Split(CFG_STR_DELIM, StringSplitOptions.None);

					// Adds to the cfgList
					cfgList.Add(cfgSettingArr);
				}
			}

			// Reads all the settings in the cfgList
			foreach (string[] setting in cfgList)
			{
				string settingName = setting[settingNameIndex];

				// Reads the setting name and update corresponding UserPreference value
				switch (settingName)
				{
					case "volume":
						{
							userPreferences.GetSetVolume = int.Parse(setting[settingValueIndex]);
							break;
						}
					// Default statement for invalid setting name
					default:
						{
							CreateConfigFile();
							break;
						}
				}
			}

			volumeTrackBar.Value = userPreferences.GetSetVolume;

			cfgReader.Close();
		}

		private void SaveConfigFile()
		{
			userPreferences.GetSetVolume = volumeTrackBar.Value;

			UpdateConfigFile();
		}

		private void UpdateConfigFile()
		{
			StreamWriter cfgUpdater = new StreamWriter(CFGFILE_PATH);

			List<string> cfgValues = new List<string>
			{
				"volume" + CFG_STR_DELIM[0] + volumeTrackBar.Value
			};

			foreach (string setting in cfgValues)
				cfgUpdater.WriteLine(setting);

			cfgUpdater.Close();
		}

		private bool LoadTrackInformation()
		{
			bool hasTrackBeenLoadedSuccessfully = true;

			// Changes the color of the song that's just been played from blue back to black in the playlist data grid view.
			// This is very important as GetIndexOfRowCurrentlyPlaying() declared below uses the forecolor parameter to find
			// the index of the song that is currently being played.
			int decolorIndex = GetIndexOfRowCurrentlyPlaying();
			if (decolorIndex != -1)
				playlistDataGridView.Rows[decolorIndex].Cells[0].Style = new DataGridViewCellStyle { ForeColor = Color.Black };
			else
				DecolorAllCells();

			try
			{
				TagLib.File tagFile = TagLib.File.Create(songPathOnDrive);

				// Loads album cover from song file (if available)
				try
				{
					if (tagFile.Tag.Pictures.Length >= 1)
					{
						var bin = (byte[])(tagFile.Tag.Pictures[0].Data.Data);
						albumCoverPictureBox.Image = Image.FromStream(new MemoryStream(bin));
					}
					else
						albumCoverPictureBox.Image = Properties.Resources.cover;
				}
				catch
				{
					albumCoverPictureBox.Image = Properties.Resources.cover;
				}

				// Loads other song information
				if (tagFile.Tag.Title != null)
					songTitleLabel.Text = tagFile.Tag.Title;
				else
				{
					var songName = Path.GetFileName(songPathOnDrive);
					songTitleLabel.Text = songName;
				}

				// If song title is more than 27 chars long, starts songTitleTimer to make label loop
				if (songTitleLabel.Text.Length > 27)
				{
					songTitleLabel.Text += " - ";
					songTitleTimer.Enabled = true;
				}
				else
					songTitleTimer.Enabled = false;

				// Loads first album artist found from the track that is going to be played
				if (tagFile.Tag.FirstAlbumArtist != null)
					artistNameLabel.Text = tagFile.Tag.FirstAlbumArtist;
				else
					artistNameLabel.Text = "Artist unknown";

				// Pretty much the same as for song title, if artist name is more than 38 chars long, make label loop
				if (artistNameLabel.Text.Length > 38)
				{
					artistNameLabel.Text += " - ";
					artistNameTimer.Enabled = true;
				}
				else
					artistNameTimer.Enabled = false;

				// Updates songTotalLengthLabel to correct length of the track that is going to be played
				var totalLengthOfTrackInSeconds = tagFile.Properties.Duration.TotalSeconds;
				TimeSpan totalLengthOfTrackInHoursMinutesAndSeconds = TimeSpan.FromSeconds(totalLengthOfTrackInSeconds);
				songTotalLengthLabel.Text = totalLengthOfTrackInHoursMinutesAndSeconds.ToString(@"h\:mm\:ss");

				// Resets songTrackBar position to 0
				songTrackBar.Value = 0;

				// Math.Floor is used to round down track length (in seconds) when converting from double to int
				songTrackBar.Maximum = Convert.ToInt32(Math.Floor(totalLengthOfTrackInSeconds));
			}
			catch
			{
				// An unhandled exception happened while getting the track info, skipping to the next song in the playlist if there is any

				hasTrackBeenLoadedSuccessfully = false;
				int indexOfSongThatFailedLoading = -1;

				for (int i = 0; i < playlistDataGridView.Rows.Count; i++)
				{
					if (playlistDataGridView.Rows[i].Cells[1].Value.ToString() == songPathOnDrive)
					{
						indexOfSongThatFailedLoading = i;
						break;
					}
				}

				if (indexOfSongThatFailedLoading != -1)
					playlistDataGridView.Rows.RemoveAt(indexOfSongThatFailedLoading);

				if (isSongOnRepeatEnabled)
				{
					isSongOnRepeatEnabled = false;
					repeatPictureBox.Image = Properties.Resources.repeat_off;
				}

				if (indexOfSongThatFailedLoading != -1
					&& playlistDataGridView.Rows.Count >= 1
					&& indexOfSongThatFailedLoading + 1 <= playlistDataGridView.Rows.Count)
				{
					songPathOnDrive = playlistDataGridView.Rows[indexOfSongThatFailedLoading].Cells[1].Value.ToString();
					playlistDataGridView.ClearSelection();
					playlistDataGridView.Rows[indexOfSongThatFailedLoading].Selected = true;

					if (LoadTrackInformation())
						PlayTrack(indexOfSongThatFailedLoading);
				}
				else
				{
					StopTrack();
					songPathOnDrive = "";
				}
			}

			return hasTrackBeenLoadedSuccessfully;
		}

		private bool PlayTrack(int indexOfTrackInPlaylist)
		{
			bool isSongPlayingSuccessfully = true;

			if (isSongCurrentlyPlaying)
				StopTrack();

			// If the song that is going to be played next is different than the one that's just been played, we do the following
			if (songPathOnDrive != lastSongPlayed)
			{
				if (audioFileReader != null)
					audioFileReader.Dispose();

				if (waveOutDevice != null)
					waveOutDevice.Dispose();

				lastSongPlayed = songPathOnDrive;

				try
				{
					waveOutDevice = new WaveOut();
					audioFileReader = new MediaFoundationReader(songPathOnDrive);
					waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
					waveOutDevice.Init(audioFileReader);
					waveOutDevice.Play();
				}
				catch
				{
					/* something wrong happened while trying to play the next song */
					isSongPlayingSuccessfully = false;
				}

				// Changes icon of the play button to a pause icon
				playPictureBox.Image = Properties.Resources.pause;
			}
			else
			{
				if (songTrackBar.Value == 0)
				{
					try
					{
						waveOutDevice = new WaveOut();
						audioFileReader = new MediaFoundationReader(songPathOnDrive);

						waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
						waveOutDevice.Init(audioFileReader);
						waveOutDevice.Play();
					}
					catch
					{
						/* something wrong happened while trying to play the same song */
						isSongCurrentlyPlaying = false;
					}

					playPictureBox.Image = Properties.Resources.pause;
				}
				else
				{
					// If we are here, it is because no new song has been selected and
					// the song trackbar value isn't equal to zero, so we simply have to un-pause the music
					try
					{
						waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
						waveOutDevice.Play();
					}
					catch
					{
						/* something wrong happened while trying to un-pause the music */
						isSongPlayingSuccessfully = false;
					}

					playPictureBox.Image = Properties.Resources.pause;
				}
			}

			if (isSongPlayingSuccessfully)
			{
				isSongCurrentlyPlaying = true;
				playTimer.Enabled = true;
				songTrackBar.Enabled = true;

				// Changes the text color of the current song playing in the playlist data grid view.
				// This is very important as GetIndexOfRowCurrentlyPlaying() declared below uses it to find the index of the current song playing.
				// Therefore, if you change the forecolor in the next line, don't forget to update it in GetIndexOfRowCurrentlyPlaying() also.
				try
				{
					if (indexOfTrackInPlaylist != -1)
						playlistDataGridView.Rows[indexOfTrackInPlaylist].Cells[0].Style = new DataGridViewCellStyle { ForeColor = Color.Blue };
				}
				catch { }

				// As systray icon's text is limited to 64 chars, the following code ensures that we do not go over that limit
				if ((songTitleLabel.Text + " (" + artistNameLabel.Text + ")").Length < 64)
					systrayNotifyIcon.Text = songTitleLabel.Text + " (" + artistNameLabel.Text + ")";
				else
				{
					// Otherwise, truncates the string to the first 64 chars of the song title + artist name
					string tempText = songTitleLabel.Text + " (" + artistNameLabel.Text + ")";
					tempText = tempText.Substring(0, 63);
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

		private void PauseTrack()
		{
			try
			{
				waveOutDevice.Stop();
			}
			catch { /* something wrong happened while pausing the track */ }

			songTrackBar.Enabled = false;
			playTimer.Enabled = false;
			isSongCurrentlyPlaying = false;

			playPictureBox.Image = Properties.Resources.play;
		}

		private void StopTrack()
		{
			try
			{
				waveOutDevice.Stop();
			}
			catch { /* something wrong happened while stopping the track */ }

			songTrackBar.Enabled = false;
			playTimer.Enabled = false;
			isSongCurrentlyPlaying = false;

			// Restores play button icon from pause to play icon
			playPictureBox.Image = Properties.Resources.play;

			// Resets songTrackBar position to 0
			songTrackBar.Value = 0;

			if (playlistDataGridView.Rows.Count > 0)
			{
				// Changes the color of the song that's just been played from blue back to black in the playlist data grid view.
				// This is very important as GetIndexOfRowCurrentlyPlaying() declared below uses the forecolor parameter to find
				// the index of the song that is currently being played.
				int decolorIndex = GetIndexOfRowCurrentlyPlaying();
				if (decolorIndex != -1)
					playlistDataGridView.Rows[decolorIndex].Cells[0].Style = new DataGridViewCellStyle { ForeColor = Color.Black };
				else
					DecolorAllCells();
			}
		}

		// playNow is a boolean paramater that can either be switched to true or false depending on if the
		// first selected song has to be played immediately or simply added to the playlist datagridview.
		private void AddFilesToPlaylist(bool playNow)
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

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				if (playNow)
				{
					playlistDataGridView.Rows.Clear();
					songPathOnDrive = openFileDialog.FileName;

					foreach (String file in openFileDialog.FileNames)
					{
						playlistDataGridView.Rows.Add(Path.GetFileNameWithoutExtension(file), file);
					}

					if (LoadTrackInformation())
						PlayTrack(playlistDataGridView.CurrentCell.RowIndex);
				}
				else
				{
					foreach (String file in openFileDialog.FileNames)
					{
						playlistDataGridView.Rows.Add(Path.GetFileNameWithoutExtension(file), file);
					}
				}

				if (playlistDataGridView.Rows.Count - 1 != -1)
					playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
			}
		}

		// playNow is a boolean paramater that can either be switched to true or false depending on if the
		// first selected song has to be played immediately or simply added to the playlist datagridview.
		private void AddFolderToPlaylist(bool playNow)
		{
			using CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				RestoreDirectory = true,
				IsFolderPicker = true
			};

			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				String[] files = Directory.GetFiles(@dialog.FileName);

				if (playNow)
					playlistDataGridView.Rows.Clear();

				for (int i = 0; i < files.Length; i++)
				{
					FileInfo file = new FileInfo(files[i]);

					if (fileExts.Contains(file.Extension.ToLower()))
						playlistDataGridView.Rows.Add(Path.GetFileNameWithoutExtension(file.Name), file.FullName);
				}

				if (playNow)
				{
					try
					{
						songPathOnDrive = playlistDataGridView.Rows[playlistDataGridView.CurrentCell.RowIndex].Cells[1].Value.ToString();
						
						if (LoadTrackInformation())
							PlayTrack(playlistDataGridView.CurrentCell.RowIndex);
					}
					catch { }
				}
				else
					if (playlistDataGridView.Rows.Count - 1 != -1)
						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
			}
		}

		private void DeleteSongsFromPlaylist()
		{
			try
			{
				foreach (DataGridViewRow r in playlistDataGridView.SelectedRows)
				{
					if (!r.IsNewRow)
					{
						playlistDataGridView.Rows.RemoveAt(r.Index);
					}
				}

				playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.CurrentCell.RowIndex;
			}
			catch
			{
				// we ignore exceptions as this might happen when playlist 
				// is empty and user clicks the delete song (-) button.
			}
		}

		private int GetIndexOfRowCurrentlyPlaying()
		{
			int rowCurrentlyPlaying = -1;

			for (int i = 0; i < playlistDataGridView.Rows.Count; i++)
			{
				if (playlistDataGridView.Rows[i].Cells[0].Style.ForeColor == Color.Blue)
				{
					rowCurrentlyPlaying = i;
					break;
				}
			}

			return rowCurrentlyPlaying;
		}

		private void DecolorAllCells()
		{
			for (int i = 0; i < playlistDataGridView.Rows.Count; i++)
			{
				playlistDataGridView.Rows[i].Cells[0].Style.ForeColor = Color.Black;
			}
		}

		private void TopBarLogoPictureBox_MouseDown(object sender, MouseEventArgs e)
		{
			// Opens Context Menu on left click
			if (e.Button == MouseButtons.Left)
				playContextMenuStrip.Show(this.PointToScreen(e.Location));
		}

		private void TopBarMinimizePictureBox_Click(object sender, EventArgs e)
		{
			this.Hide();
		}

		private void TopBarClosePictureBox_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void PlayFilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			AddFilesToPlaylist(true);
		}

		private void PlayDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
		{
			AddFolderToPlaylist(true);
		}

		private void AboutThisMusicPlayerToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string message = "Made by Clément Campagna under MIT License, August 2020.\nhttps://clementcampagna.com";
			string title = "Good Vibes Music Player v1.0";
			MessageBox.Show(message, title);
		}

		private void SongTrackBar_ValueChanged(object sender, EventArgs e)
		{
			// Updates songCurrentPositionLabel
			TimeSpan totalLengthOfTrackInHoursMinutesAndSeconds = TimeSpan.FromSeconds(songTrackBar.Value);
			songCurrentPositionLabel.Text = totalLengthOfTrackInHoursMinutesAndSeconds.ToString(@"h\:mm\:ss");
		}

		private void AdjustTrackPosition()
		{
			playTimer.Enabled = false;

			try
			{
				audioFileReader.SetPosition(TimeSpan.FromSeconds(songTrackBar.Value));
			}
			catch { /* something wrong happened while trying to set new track position */ }

			playTimer.Enabled = true;
		}

		private void SongTrackBar_KeyUp(object sender, KeyEventArgs e)
		{
			AdjustTrackPosition();
		}

		private void SongTrackBar_MouseUp(object sender, MouseEventArgs e)
		{
			AdjustTrackPosition();
		}

		private void SongTrackBar_MouseWheel(object sender, MouseEventArgs e)
		{
			AdjustTrackPosition();
		}

		private void PlayTimer_Tick(object sender, EventArgs e)
		{
			if (songTrackBar.Value < songTrackBar.Maximum)
				songTrackBar.Value += 1;
			else
			{
				if (!isSongOnRepeatEnabled)
				{
					int currentlyPlayingSongIndex;

					if (isShufflingEnabled)
					{
						// Generates random index from 0 to playlist count
						Random rnd = new Random();
						currentlyPlayingSongIndex = rnd.Next(0, playlistDataGridView.Rows.Count);
					}
					else
						currentlyPlayingSongIndex = GetIndexOfRowCurrentlyPlaying();

					if ((playlistDataGridView.Rows.Count >= 1
						&& currentlyPlayingSongIndex + 1 < playlistDataGridView.Rows.Count)
						|| (isShufflingEnabled && playlistDataGridView.Rows.Count >= 1))
					{
						if (isShufflingEnabled)
						{
							songPathOnDrive = playlistDataGridView.Rows[currentlyPlayingSongIndex].Cells[1].Value.ToString();
							playlistDataGridView.ClearSelection();
							playlistDataGridView.Rows[currentlyPlayingSongIndex].Selected = true;
						}
						else
						{
							songPathOnDrive = playlistDataGridView.Rows[currentlyPlayingSongIndex + 1].Cells[1].Value.ToString();
							playlistDataGridView.ClearSelection();
							playlistDataGridView.Rows[currentlyPlayingSongIndex + 1].Selected = true;
						}

						if (LoadTrackInformation())
							PlayTrack(playlistDataGridView.SelectedRows[0].Index);
					}
					else
					{
						if (currentlyPlayingSongIndex == -1 && playlistDataGridView.Rows.Count >= 1)
						{
							try
							{
								songPathOnDrive = playlistDataGridView.Rows[0].Cells[1].Value.ToString();
								playlistDataGridView.ClearSelection();
								playlistDataGridView.Rows[0].Selected = true;

								if (LoadTrackInformation())
									PlayTrack(playlistDataGridView.SelectedRows[0].Index);
							}
							catch { /* unable to start playing the first track in the playlist */ }
						}
						else
							StopTrack();
					}
				}
				else
				{
					int indexOfRowCurrentlyPlaying = GetIndexOfRowCurrentlyPlaying();
					if (LoadTrackInformation())
						PlayTrack(indexOfRowCurrentlyPlaying);
				}
			}
		}

		private void SongTitleTimer_Tick(object sender, EventArgs e)
		{
			// Adds horizontal scrolling effect to song title label
			songTitleLabel.Text = songTitleLabel.Text.Substring(1, songTitleLabel.Text.Length - 1) + songTitleLabel.Text.Substring(0, 1);
		}

		private void ArtistNameTimer_Tick(object sender, EventArgs e)
		{
			// Adds horizontal scrolling effect to artist name label
			artistNameLabel.Text = artistNameLabel.Text.Substring(1, artistNameLabel.Text.Length - 1) + artistNameLabel.Text.Substring(0, 1);
		}

		private void RepeatPictureBox_Click(object sender, EventArgs e)
		{
			if (!isSongOnRepeatEnabled)
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

		private void PreviousPictureBox_Click(object sender, EventArgs e)
		{
			int currentlyPlayingSongIndex = GetIndexOfRowCurrentlyPlaying();

			if (playlistDataGridView.Rows.Count >= 1 && currentlyPlayingSongIndex - 1 >= 0)
			{
				songPathOnDrive = playlistDataGridView.Rows[currentlyPlayingSongIndex - 1].Cells[1].Value.ToString();
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[currentlyPlayingSongIndex - 1].Selected = true;

				if (LoadTrackInformation())
					PlayTrack(currentlyPlayingSongIndex - 1);
			}
		}

		private void PlayPictureBox_MouseDown(object sender, MouseEventArgs e)
		{
			// Always opens Context Menu on right click, or on left click only if no song is playing
			if (e.Button == MouseButtons.Right)
				playContextMenuStrip.Show(this.PointToScreen(playPictureBox.Location));
			else
			{
				if (!isSongCurrentlyPlaying)
				{
					if (songPathOnDrive != "")
						PlayTrack(-1);
					else
					{
						if (playlistDataGridView.Rows.Count > 0)
						{
							songPathOnDrive = playlistDataGridView.Rows[playlistDataGridView.CurrentCell.RowIndex].Cells[1].Value.ToString();

							if (LoadTrackInformation())
								PlayTrack(playlistDataGridView.CurrentCell.RowIndex);
						}
						else
							playContextMenuStrip.Show(this.PointToScreen(playPictureBox.Location));
					}
				}
				else
					PauseTrack();
			}
		}

		private void NextPictureBox_Click(object sender, EventArgs e)
		{
			int currentlyPlayingSongIndex = GetIndexOfRowCurrentlyPlaying();

			if (playlistDataGridView.Rows.Count >= 1 && currentlyPlayingSongIndex + 1 < playlistDataGridView.Rows.Count)
			{
				songPathOnDrive = playlistDataGridView.Rows[currentlyPlayingSongIndex + 1].Cells[1].Value.ToString();
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[currentlyPlayingSongIndex + 1].Selected = true;

				if (LoadTrackInformation())
					PlayTrack(currentlyPlayingSongIndex + 1);
			}
		}

		private void ShufflePictureBox_Click(object sender, EventArgs e)
		{
			if (!isShufflingEnabled)
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

		private void VolumeTrackBar_ValueChanged(object sender, EventArgs e)
		{
			if (waveOutDevice != null)
				waveOutDevice.Volume = (float)volumeTrackBar.Value / 100;
		}

		private void UpPictureBox_Click(object sender, EventArgs e)
		{
			DataGridView dgv = playlistDataGridView;

			if (dgv.Rows.Count > 1)
				try
				{
					int totalRows = dgv.Rows.Count;

					// Gets index of the row for the selected cell
					int rowIndex = dgv.SelectedCells[0].OwningRow.Index;
					if (rowIndex == 0)
						return;

					// Gets index of the column for the selected cell
					int colIndex = dgv.SelectedCells[0].OwningColumn.Index;
					DataGridViewRow selectedRow = dgv.Rows[rowIndex];
					dgv.Rows.Remove(selectedRow);
					dgv.Rows.Insert(rowIndex - 1, selectedRow);
					dgv.ClearSelection();
					dgv.Rows[rowIndex - 1].Cells[colIndex].Selected = true;
					playlistDataGridView.FirstDisplayedScrollingRowIndex = dgv.Rows[rowIndex - 1].Cells[colIndex].RowIndex;
				}
				catch { /* unable to re-order playlist datagridview */ }
		}

		private void DownPictureBox_Click(object sender, EventArgs e)
		{
			DataGridView dgv = playlistDataGridView;

			if (dgv.Rows.Count > 1)
				try
				{
					int totalRows = dgv.Rows.Count;

					// Gets index of the row for the selected cell
					int rowIndex = dgv.SelectedCells[0].OwningRow.Index;
					if (rowIndex == totalRows - 1)
						return;

					// Gets index of the column for the selected cell
					int colIndex = dgv.SelectedCells[0].OwningColumn.Index;
					DataGridViewRow selectedRow = dgv.Rows[rowIndex];
					dgv.Rows.Remove(selectedRow);
					dgv.Rows.Insert(rowIndex + 1, selectedRow);
					dgv.ClearSelection();
					dgv.Rows[rowIndex + 1].Cells[colIndex].Selected = true;
					playlistDataGridView.FirstDisplayedScrollingRowIndex = dgv.Rows[rowIndex - 1].Cells[colIndex].RowIndex;
				}
				catch { /* unable to re-order playlist datagridview */ }
		}

		private void PlaylistDataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex >= 0)
			{
				DataGridViewRow row = playlistDataGridView.Rows[e.RowIndex];
				songPathOnDrive = row.Cells[1].Value.ToString();

				if (LoadTrackInformation())
					PlayTrack(e.RowIndex);
			}
		}

		private void PlaylistDataGridView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete)
				DeleteSongsFromPlaylist();
		}

		private void FindCurrentTrackInPlaylistPictureBox_Click(object sender, EventArgs e)
		{
			int currentIndex = GetIndexOfRowCurrentlyPlaying();

			if (currentIndex != -1)
			{
				playlistDataGridView.FirstDisplayedScrollingRowIndex = currentIndex;
				playlistDataGridView.ClearSelection();
				playlistDataGridView.Rows[currentIndex].Selected = true;
			}
		}

		private void MinusPictureBox_Click(object sender, EventArgs e)
		{
			DeleteSongsFromPlaylist();
		}

		private void PlusPictureBox_MouseDown(object sender, MouseEventArgs e)
		{
			// Opens Context Menu on left click
			if (e.Button == MouseButtons.Left)
				plusContextMenuStrip.Show(this.PointToScreen(plusPictureBox.Location));
		}

		private void AddFilesToQueueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			AddFilesToPlaylist(false);
		}

		private void AddDirectoryToQueueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			AddFolderToPlaylist(false);
		}

		private void LoadPlaylistFromPlusMenuToolStripMenuItem_Click(object sender, EventArgs e)
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

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				try
				{
					ReadPlaylistFiles playlist = new ReadPlaylistFiles(openFileDialog.FileName);

					try
					{
						DataTable playlistDataTable = playlist.readPlaylistFile;

						foreach (DataRow row in playlistDataTable.Rows)
						{
							playlistDataGridView.Rows.Add(row.ItemArray);
						}

						playlistDataGridView.FirstDisplayedScrollingRowIndex = playlistDataGridView.Rows.Count - 1;
					}
					catch { }
				}
				catch { }
			}
		}

		private void SavePlaylistFromPlusMenuToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFileDialog savePlaylistDialog = new SaveFileDialog();
			savePlaylistDialog.Title = "Save Playlist";
			savePlaylistDialog.CheckPathExists = true;
			savePlaylistDialog.DefaultExt = "gvmp";
			savePlaylistDialog.Filter = "Good Vibes Music Player Playlists (*.gvmp)|*.gvmp";
			savePlaylistDialog.FilterIndex = 1;
			savePlaylistDialog.RestoreDirectory = true;

			if (savePlaylistDialog.ShowDialog() == DialogResult.OK)
			{
				var sb = new StringBuilder();

				var headers = playlistDataGridView.Columns.Cast<DataGridViewColumn>();
				sb.AppendLine(string.Join(",", headers.Select(column => "\"" + column.HeaderText + "\""))); headers.Select(column => "\"" + column.HeaderText + "\"").ToArray();

				foreach (DataGridViewRow row in playlistDataGridView.Rows)
				{
					var cells = row.Cells.Cast<DataGridViewCell>();
					sb.AppendLine(string.Join(",", cells.Select(cell => "\"" + (cell.ColumnIndex == 0 ? cell.FormattedValue : cell.Value) + "\"").ToArray()));
				}

				using StreamWriter file = new StreamWriter(savePlaylistDialog.FileName);
				file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
			}
		}

		private void SystrayNotifyIcon_MouseClick(object sender, MouseEventArgs e)
		{
			this.Show();
			this.WindowState = FormWindowState.Normal;
			this.TopMost = true;
			this.TopMost = false;
		}

		private void GoodVibesForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (isSongCurrentlyPlaying)
				StopTrack();

			if (audioFileReader != null)
				audioFileReader.Dispose();

			if (waveOutDevice != null)
				waveOutDevice.Dispose();

			SaveConfigFile();
		}
	}
}
