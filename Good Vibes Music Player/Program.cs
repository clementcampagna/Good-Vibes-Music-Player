using Microsoft.VisualBasic.ApplicationServices;
using System;

namespace Good_Vibes_Music_Player
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]

		static void Main( string[] args )
		{
			App myApp = new App();
			myApp.Run( args );
		}

		/// <summary>
		/// We inherit from the VB.NET WindowsFormApplicationBase class, which has the single-instance functionality.
		/// </summary>
		class App : WindowsFormsApplicationBase
		{
			public App()
			{
				// Makes this a single-instance application
				this.IsSingleInstance = true;
				this.EnableVisualStyles = true;

				// There are some other things available in the VB application model, for instance the shutdown style:
				this.ShutdownStyle = ShutdownMode.AfterMainFormCloses;

				// Adds StartupNextInstance handler
				this.StartupNextInstance += new StartupNextInstanceEventHandler( this.SIApp_StartupNextInstance );
			}

			/// <summary>
			/// We are responsible for creating the application's main form in this override.
			/// </summary>
			protected override void OnCreateMainForm()
			{
				// Creates an instance of the main form and set it in the application; but don't try to run it
				this.MainForm = new GoodVibesMainForm();

				// We want to pass along the command-line arguments to this first instance

				// Allocates room in our string array
				( (GoodVibesMainForm)this.MainForm ).Args = new string[ this.CommandLineArgs.Count ];

				// And copies the arguments over to our form
				this.CommandLineArgs.CopyTo( ( (GoodVibesMainForm)this.MainForm ).Args, 0 );
			}

			/// <summary>
			/// This is called for additional instances. The application model will call this 
			/// function, and terminate the additional instance when this returns.
			/// </summary>
			/// <param name="eventArgs"></param>
			protected void SIApp_StartupNextInstance( object sender, StartupNextInstanceEventArgs eventArgs )
			{
				// Copies the arguments to a string array
				string[] args = new string[ eventArgs.CommandLine.Count ];
				eventArgs.CommandLine.CopyTo( args, 0 );

				// Creates an argument array for the Invoke method
				object[] parameters = new object[ 2 ];
				parameters[ 0 ] = this.MainForm;
				parameters[ 1 ] = args;

				// Needs to use invoke to b/c this is being called from another thread.
				this.MainForm.Invoke( new GoodVibesMainForm.ProcessParametersDelegate( ( (GoodVibesMainForm)this.MainForm ).ProcessParameters ), parameters );
			}
		}
	}
}
