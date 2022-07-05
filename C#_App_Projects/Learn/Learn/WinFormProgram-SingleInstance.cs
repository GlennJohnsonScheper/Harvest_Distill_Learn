using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

// Single-Instance Provisions:
using System.Diagnostics;
using System.Runtime.InteropServices;

//App assumes the Harvest "Q" folder is peer to executable.
//rem this assumes ...\EXE contains SRCS contains Solution:
//Copy both EXE and DLLS to 2 levels above solution folder:
//copy "$(TargetPath)" "$(SolutionDir)\..\..\$(TargetFilename)"
//copy "$(TargetDir)\*.dll" "$(SolutionDir)\..\..\"

// This was the "program.cs" created by VS2013 New Windows Form Application.
// I am adding to it my code provisions that allow only a single executable.

namespace Learn
{
	static class Program
	{
		// Single-Instance Provisions:
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
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			// Single-Instance Provisions:
			try
			{
				// If another instance is already running, activate it and exit
				Process currentProc = Process.GetCurrentProcess();
				foreach (Process proc in Process.GetProcessesByName(currentProc.ProcessName))
				{
					if (proc.Id != currentProc.Id)
					{
						ShowWindow(proc.MainWindowHandle, SW_RESTORE);
						SetForegroundWindow(proc.MainWindowHandle);
						return;   // Exit application
					}
				}
				
				// Continue with the normal contents of Main():
				// (SharpDevelop:MainForm or VisualStudio:Form1)
				
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new Form1());
			}
			catch (Exception)
			{
			}

		}
	}
}
