/*
 * SingleInstanceProgram.cs
 */

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pipe
{
	/// <summary>
	/// Class with program entry point.
	/// </summary>
	internal sealed class Program
	{
		// from http://stackoverflow.com/questions/51898/activating-the-main-form-of-a-single-instance-application
		// add using System.Diagnostics;
		// add using System.Runtime.InteropServices;

		// Sets the window to be foreground
		[DllImport("User32")]
		private static extern int SetForegroundWindow(IntPtr hwnd);

		// Activate or minimize a window
		[DllImportAttribute("User32.DLL")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
		private const int SW_RESTORE = 9;

		/// <summary>
		/// Program entry point.
		/// </summary>
		[STAThread]
		private static void Main(string[] args)
		{
			try
			{
				// If another instance is already running, activate it and exit
				Process currentProc = Process.GetCurrentProcess();
				foreach (Process proc in Process.GetProcessesByName(currentProc.ProcessName))
				{
					if (proc.Id != currentProc.Id)
					{
						// Not in this special case of MyCron:
						// ShowWindow(proc.MainWindowHandle, SW_RESTORE);
						// SetForegroundWindow(proc.MainWindowHandle);
						return;   // Exit application
					}
				}

				// Set the Cur Dir to the App location:
				Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

				using(FileStream fs = File.Create("KeepRunning.txt")) // Harvest, run!
				{
					fs.Close();
				}
				
				// Continue with the normal contents of Main():
				// (SharpDevelop:MainForm or VisualStudio:Form1)

				// -- Application.EnableVisualStyles();
				// -- Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
			catch (Exception ex)
			{
				MessageBox.Show("Exception: " + ex.ToString());
			}
		}
		
	}
}
