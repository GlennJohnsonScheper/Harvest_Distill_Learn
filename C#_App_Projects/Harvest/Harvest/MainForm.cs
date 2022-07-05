/*
 * MainForm.cs for Pipe
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Harvest;

namespace Pipe
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : Form
	{
		public MainForm()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			//
			// TODO: Add constructor code after the InitializeComponent() call.
			//
			this.Shown += new EventHandler(Form1_Shown);
			this.WindowState = FormWindowState.Minimized;
			this.ShowInTaskbar = false;
		}
		private void Form1_Shown(object sender, EventArgs e)
		{
			this.Hide();

			// Kick off my Harvest operation!
			Harvest.Harvest.chronic = new Timer();
			Harvest.Harvest.chronic.Tick += new EventHandler(Harvest.Harvest.tickTock);
			Harvest.Harvest.chronic.Interval = 100; // ms for rapid start
			Harvest.Harvest.chronic.Start();
		}
	}
}
