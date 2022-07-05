/*
 * Learn.cs
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
// beyond the minimums:
using System.Text.RegularExpressions;

// This file "Learn.cs" was started by copying the "Form1.cs" created by VS2013 New Windows Form Application.
// Which extant "Form1.cs" file will add one line, calling: Learn_FinishConstructor(); right after InitializeComponent();
// Here is all of my significant code development beyond the bare WinForm generated project, by using this PARTIAL class:

namespace Learn
{
	public partial class Form1 : Form
	{
		// pre-primary provisions: diagnosing events:
		static Boolean logging = false;
        static string logFilename = @"debug_Learn.txt";
		static List<String> myLog = new List<string>();


		// These record all of user's latest situation; See SettingsDefaultSave().
		// static uint myFormWinState; // copied to/from a member of Settings1.Default
		static Point myFormLocation; // copied to/from a member of Settings1.Default
		static Size myFormSize; // copied to/from a member of Settings1.Default
		static float myFontSize; // copied to/from a member of Settings1.Default
		static double myLinesPerSec; // copied to/from a member of Settings1.Default
		static String myLastInputFilePath; // copied to/from a member of Settings1.Default
		static Boolean myKeepSpacesMode; // copied to/from a member of Settings1.Default
		static Color myColorChoice; // copied to/from a member of Settings1.Default
		// Primary matters: Form-existential controls
		Boolean myForm1ShownHappened = false; // gates Form1_SizeChanged activity (off until Form1_Shown)
		Boolean myClientSizeIsStable = false; // gates hotPaintWorker painting activity (off for longer intervals than the Mutex)
		System.Threading.Mutex myBackingAndFontAvailableMutex; // controls access to myForm1GraphicsObject, myBackingContext, myBackingBuffer, myFormFont, CharWidth

		// Primary matters: Form-drawing objects
		System.Drawing.Graphics myForm1GraphicsObject;  // needed to create a similar myBackingBuffer, and later to render
		System.Drawing.BufferedGraphicsContext myBackingContext; // Some kind of needful wrapper around myBackingBuffer
		System.Drawing.BufferedGraphics myBackingBuffer; // The double-buffered graphics object where HotPaintWorker paints

		// Primary matters: Thread controls
		Boolean myFormIsClosing; // Form1_FormClosing comes first; then Application_Exit
		System.Threading.Thread myHotLoopThread;

		// Primary matters: Painting controls
		System.Diagnostics.Stopwatch myRealTimeClock; // 1 ms granualiry, whereas Sleep or Wait have 10-15 ms granularity
		long myLastPaintingMs;
		Boolean PaintingRequired; // better known as Invalidate
		SolidBrush foreBrush; // = new SolidBrush(Color.Black);

		// Secondary-(but-interlocked-with-primary) matters: fonts and char widths
		System.Drawing.Font myFormFont;
		int myIntegerFontHeight; // screen pixels
		Dictionary<char, int> CharWidth = new Dictionary<char, int>();

		// Secondary matters: user text
		string RawText = string.Empty; // It's crazy to ever leave a string null
		Regex NonASCII = new Regex(@"[^\r\n\t -\377]+", RegexOptions.Singleline); // Keeps CR LF TAB, and ' ' to 0xff
		Regex PairedCRLF = new Regex(@"\r\n", RegexOptions.Singleline);
		Regex SingleCR = new Regex(@"\r", RegexOptions.Singleline);
		Regex MultipleTabsSpaces = new Regex(@"[ \t]+", RegexOptions.Singleline); // optional
		string CookedText = string.Empty;

		// Secondary matters: display line buffers
		string[] WrappedLineArray = null;
		int[] WrappedLineOffsets = null;
		int WrappedLineArrayCount = 0; // separate int to obviate null checking before WrappedLineArray.Length
		int TotalScanLines = 0;

		// Tertiary matters: smooth scroll advance
		Boolean ScrollingEnabled = false;
		const double StartingLinesPerSec = 0.5d;
		int myMsPerScanline;

		// Tertiary matters: text line offset and scanline offset
		const float mySizeFactor = 1.23f;
		const double myLPSFactor = 1.23d;
		const int leftMargin = 3;
		const int TopMargin = 3;
		const int rightMargin = 20; // Need about 1/8 inch extra on the right - no, more than 15.
		
		int ScrolledToScanLine;
		
		int SaveScrollScanLine;

		static string keypressAccumulator = string.Empty;
		static int AccuPowerTen = 1;
		static int AccuDigitRun = 0;
		static string searchPattern = string.Empty;
		static Boolean matchBOW = false;
		static Boolean matchEOW = false;

		static int logMarkCount = 0;
		static int logSpacing = 1;
		static Boolean loggedPaint = false;
		static Boolean loggedinKey = false;
		static Boolean loggedexKey = false;

		static Random rand = new Random();

		void log(String line)
		{
			if (line.StartsWith("ex "))
				logSpacing -= 3;
			{
				// I think to rid certain repeating conditions, like:
				//0984467    !-paint +17:33860-33867/137536@-98-322+60/336
				//0984484    !-paint +17:33860-33867/137536@-99-321+60/336
				//0984496    in Form1_KeyDown 20011
				//0984496    ex Form1_KeyDown
				Boolean doPaint = true;

				if(line.StartsWith("!-paint")
				   && loggedPaint == true)
					doPaint = false;

				if(line == "in Form1_KeyDown 20011"
				   && loggedinKey == true)
					doPaint = false;

				if(line == "ex Form1_KeyDown"
				   && loggedexKey == true)
					doPaint = false;

				if(doPaint)
					myLog.Add(myRealTimeClock.ElapsedMilliseconds.ToString("d7") + string.Empty.PadLeft(logSpacing) + line);
				
				if(line.StartsWith("!-paint"))
				{
					loggedPaint = true;
				}
				else if(line == "in Form1_KeyDown 20011")
				{
					loggedinKey = true;
				}
				else if(line == "ex Form1_KeyDown")
				{
					loggedexKey = true;
				}
				else
				{
					loggedPaint = false;
					loggedinKey = false;
					loggedexKey = false;
				}
			}
			if (line.StartsWith("in "))
				logSpacing += 3;
		}


        public void Learn_FinishConstructor()
        {

            myRealTimeClock = new System.Diagnostics.Stopwatch();
            myRealTimeClock.Start();

            if (logging) log("in Form1 ctor " + DateTime.Now);

            // Forms Constructor must do all initialization code, long before the visible Load action.
            // Form1 ctor should set all Form1 fields affecting form layout ahead of the OnLoad event.

            myForm1ShownHappened = false; // gates SizeChanged work

            myClientSizeIsStable = false; // gates hotPaintWorker painting activity

            myBackingAndFontAvailableMutex = new System.Threading.Mutex(true); // ASAP! owned by main thread from Form1 ctor until Form1_Shown

            // You dance with the Devil, you pick up fleas. Skirt this M$ bloat:
            // SettingsDefaultGet(); // Recover user settings from last execution

            SettingsReset();

            Application.ApplicationExit += new EventHandler(Application_Exit); // Does: 1. Disposing, 2. Save user settings. 3. Write Log

            Load += new EventHandler(Form1_Load);
            Shown += new EventHandler(Form1_Shown);
            Resize += new EventHandler(Form1_Resize);
            SizeChanged += new EventHandler(Form1_SizeChanged);
            KeyDown += new KeyEventHandler(Form1_KeyDown);
            KeyUp += new KeyEventHandler(Form1_KeyUp);
            KeyPress += new KeyPressEventHandler(Form1_KeyPress);
            LostFocus += new EventHandler(Form1_LostFocus);
            Activated += new EventHandler(Form1_Activate);
            GotFocus += new EventHandler(Form1_GotFocus);
            FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            Click += new EventHandler(Form1_Click);

            if (logging) log("ex Form1 ctor");
            // Here ends Learn_FinishConstructor:
        }
		
		void Application_Exit(object sender, EventArgs e)
		{
			if (logging) log("in Application_Exit");
			// 1. Disposing
			{
				myBackingAndFontAvailableMutex.WaitOne(); // Intentional unbalanced extra WaitOne, so main will never release Mutex again.
				if (logging) log("in Mutex");
				{
					DiscardBacking();
				}
				if (logging) log("ex Mutex will never happen again"); // unbalanced during Application_Exit
				// Intentionally omitting -- myBackingAndFontAvailableMutex.ReleaseMutex(); -- during Application_Exit
			}

			// 2. Save user settings
			// SettingsDefaultSave();

			// 3. Write log
			if (logging)
			{
				log("ex Application_Exit " + DateTime.Now); // last chance to be logged
				System.IO.File.WriteAllLines(logFilename, myLog.ToArray());
			}
			// just above here is my... if (logging) log("ex Application_Exit");
		}


		void SettingsReset()
		{
			// like (old) Keys.R reset (but in ctor, before shown)

			// part 1
			RawText = string.Empty;
			CookedText = string.Empty;
			WrappedLineArray = null;
			WrappedLineArrayCount = 0; // separate int to obviate null checking before WrappedLineArray.Length
			TotalScanLines = 0;
			ScrollingEnabled = false; // during ^R = Reset
			ScrolledToScanLine = 0;

			// part 2
			// myFormWinState = (uint)FormWindowState.Normal; // Normal == 0

			myFormLocation = new Point(Screen.PrimaryScreen.WorkingArea.Width * 35/100, Screen.PrimaryScreen.WorkingArea.Height * 30/100);
			myFormSize = new Size(Screen.PrimaryScreen.WorkingArea.Width * 60/100, Screen.PrimaryScreen.WorkingArea.Height * 60/100);
			myFontSize = 25.0f;
			myLinesPerSec = StartingLinesPerSec;
			myColorChoice = Color.Lime;
			myLastInputFilePath = string.Empty;
		}


		void CreateBacking()
		{
			if (logging) log("in CreateBacking");
			// My caller did WaitOne;
			Rectangle nonemptySafeRect = ClientRectangle; // takes exception to zero area
			if (nonemptySafeRect.Width < 1)
				nonemptySafeRect.Width = 1;
			if (nonemptySafeRect.Height < 1)
				nonemptySafeRect.Height = 1;
			myBackingContext = BufferedGraphicsManager.Current;
			myBackingContext.MaximumBuffer = nonemptySafeRect.Size;
			myForm1GraphicsObject = CreateGraphics(); // during CreateBacking
			myBackingBuffer = myBackingContext.Allocate(myForm1GraphicsObject, nonemptySafeRect);

			// 1. Avoid mixed colors. Should speed up rendering.
			myBackingBuffer.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

			// My caller will do Release.
			if (logging) log("ex CreateBacking");
		}

		void DiscardBacking()
		{
			if (logging) log("in DiscardBacking");
			// My caller did WaitOne;
			myBackingContext = null;
			myForm1GraphicsObject = null;
			if (myBackingBuffer != null)
			{
				myBackingBuffer.Dispose();
				myBackingBuffer = null;
			}
			// My caller will do Release (except when program exits).
			if (logging) log("ex DiscardBacking");
		}
		

		void Form1_Load(object sender, EventArgs e)
		{
			if (logging) log("in Form1_Load");
			// Occurs before a form is displayed for the first time.
			// Here, put code that requires the window size and location to be known.

			// WindowState = (FormWindowState)myFormWinState;

			SetStyle( // Set all three of these window flags to true, for myHotLoopThread/hotPaintWorker will paint window
			         ControlStyles.UserPaint |
			         ControlStyles.AllPaintingInWmPaint |
			         ControlStyles.OptimizedDoubleBuffer, true);

			// Set Location and Size from User Settings
			StartPosition = FormStartPosition.Manual;
			Location = myFormLocation;
			Size = myFormSize;
			Text = "Learn";
			if (logging) log("ex Form1_Load");
		}


		void Form1_Shown(object sender, EventArgs e)
		{
			if (logging) log("in Form1_Shown");
			{
				if (logging) log("in Mutex is not required here, it is already owned");
				// No need here to do -- myBackingAndFontAvailableMutex.WaitOne(); -- it has been owned by main thread from Form1 ctor until now
				{
					CommonTo_ShownOr_SizeChanged(); // during Form1_Shown
					myForm1GraphicsObject.Clear(myColorChoice); // does this exist yet? -- not until here, after first CommonTo_ShownOr_SizeChanged
					SetFontAndComputeCharWidthsAndSpeed(); // now that myBackingBuffer exists

                    // This shown is a one-time event at app startup...

                    // Let's start program with a convenient, automatic CONTROL-O action = Open input file.
                    {
                        OpenFileDialog ofd = new OpenFileDialog();
                        ofd.Title = "Learn: Choose Input File";
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            myLastInputFilePath = ofd.FileName;
                            RawText = System.IO.File.ReadAllText(myLastInputFilePath, Encoding.UTF8);
                            MakeRawTextIntoCookedText();
                            MakeCookedTextIntoWrappedLineArray();
                        }
                    }

					// Currently, there is no text, but for generallity...
					// If there is any text in view, it must be re-wrapped
					if (CookedText.Length > 0)
						MakeCookedTextIntoWrappedLineArray();
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex(); // which has been owned by main thread from Form1 ctor until Form1_Shown (now)
			}

			myFormIsClosing = false;
			myHotLoopThread = new System.Threading.Thread(hotPaintWorker);
			myHotLoopThread.SetApartmentState(System.Threading.ApartmentState.STA);
			myHotLoopThread.Start();

			myForm1ShownHappened = true; // enables SizeChanged work
			PaintingRequired = true; // in Form1_Shown
			if (logging) log("ex Form1_Shown"); // unbalanced during Form1_Shown
		}

		void Form1_Resize(object sender, EventArgs e)
		{
			if (logging) log("in Form1_Resize");
			// This is only when User tries to resize, not programmatic resizing.
			// I will use Form1_Resize to stop painting until Form1_SizeChanged.
			// Affecting a long-term bool under the short-term mutex, of course.
			if (myForm1ShownHappened)
			{
				{
					myBackingAndFontAvailableMutex.WaitOne(); // infinite timeout, as painter is brief by design
					if (logging) log("in Mutex");
					{
						if (logging) log("stable=F(resize)");
						myClientSizeIsStable = false; // becomes unstable starting with Form1_Resize
					}
					if (logging) log("ex Mutex");
					myBackingAndFontAvailableMutex.ReleaseMutex();
				}
			}
			if (logging) log("ex Form1_Resize");
		}

		void Form1_SizeChanged(object sender, EventArgs e)
		{
			if (logging) log("in Form1_SizeChanged");
			// This event is raised if the Size property is changed by either a programmatic modification or user interaction,
			// including due to size change in Form1 ctor or Form1_Load; This bool prevents me from working until Form1_Shown.
			if (myForm1ShownHappened)
			{
				CommonTo_ShownOr_SizeChanged(); // during Form1_SizeChanged (only if myForm1ShownHappened)
				// If there is any text in view, it must be re-wrapped
				if (CookedText.Length > 0)
				{
					if (logging) log("Old ScrolledToScanLine = " + ScrolledToScanLine);
					// First, solve the current viewing position's offset in input text.
					int InputOffset = 0;
					if (WrappedLineOffsets.Length > ScrolledToScanLine / myIntegerFontHeight)
						InputOffset = WrappedLineOffsets[ScrolledToScanLine / myIntegerFontHeight];

					// Second, revise the wrapping.
					MakeCookedTextIntoWrappedLineArray();
					// Third, try to set a scroll position to match that same input offset.
					for (int i = 1; i < WrappedLineOffsets.Length; i++) // skip 0 to allow - 1.
					{
						if (WrappedLineOffsets[i] > InputOffset)
						{
							ScrolledToScanLine = i * myIntegerFontHeight;
							if (logging) log("New ScrolledToScanLine = " + ScrolledToScanLine);
							break;
						}
					}
				}

				if (WindowState == FormWindowState.Normal)
				{
					// Save a copy to revise user settings from last "Normal" form size and location.
					myFormLocation = Location;
					myFormSize = Size;
				}
				PaintingRequired = true; // in Form1_SizeChanged (only if myForm1ShownHappened)
			}
			if (logging) log("ex Form1_SizeChanged");
		}
		void Form1_Activate(object sender, EventArgs e)
		{
			if (logging) log("in Form1_Activate");
			if (logging) log("ex Form1_Activate");
		}
		void Form1_GotFocus(object sender, EventArgs e)
		{
			if (logging) log("in Form1_GotFocus");
			if (myForm1ShownHappened)
			{
				myBackingAndFontAvailableMutex.WaitOne(); // infinite timeout, as painter is brief by design
				if (logging) log("in Mutex");
				{
					if (myBackingBuffer != null
						&& myForm1GraphicsObject != null)
					{
						myBackingBuffer.Graphics.Clear(myColorChoice);
						myBackingBuffer.Render(myForm1GraphicsObject);
					}
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex();
			}
			PaintingRequired = true; // in Form1_GotFocus

			if (logging) log("ex Form1_GotFocus");
		}

		void Form1_LostFocus(object sender, EventArgs e)
		{
			if (logging) log("in Form1_LostFocus");

			// Let me restate this finding: 1. Keydown; 2. Lost Focus; 3. NO Keyup!

            // So what would KeyUp do, that LostFocus should do instead?

			if (myForm1ShownHappened)
			{
				myBackingAndFontAvailableMutex.WaitOne(); // infinite timeout, as painter is brief by design
				if (logging) log("in Mutex");
				{
					if (myBackingBuffer != null
						&& myForm1GraphicsObject != null)
					{
						myBackingBuffer.Graphics.Clear(myColorChoice);
						myBackingBuffer.Render(myForm1GraphicsObject);
					}
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex();
			}
			PaintingRequired = true; // in Form1_LostFocus

			keypressAccumulator = string.Empty;
			AccuPowerTen = 1;
			AccuDigitRun = 0;

			PaintingRequired = true; // in Form1_LostFocus
			if (logging) log("ex Form1_LostFocus");
		}

		void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (logging) log("in Form1_FormClosing");

			// User clicking System Close button comes here.
			// User typing ALT-F4 keystroke comes here.
			// Timer invoking "Invoke(new Action(Close))" comes here.
			// Right-clicking toolbar icon, Close Window, comes here.
			// Aha! Finally!
			// Setting myFormIsClosing prevents .Render(myForm1GraphicsObject) from throwing.
			myFormIsClosing = true; // at Form1_FormClosing hotPaintWorker()
			if (myHotLoopThread != null)
			{
				if (logging) log("joining");
				myHotLoopThread.Join(); // await return of hotPaintWorker() before many things disposed
				myHotLoopThread = null;
			}
			if (logging) log("ex Form1_FormClosing");
		}


		void Form1_Click(object sender, EventArgs e)
		{
			if (logging) log("in Form1 click");
			// I want a click when minimized to restore the program.
			if (WindowState == FormWindowState.Minimized)
			{
				WindowState = FormWindowState.Normal;
				// and do what? invalidate?
				PaintingRequired = true;
			}
			if (logging) log("ex Form1 click");
		}
		
		
		void Form1_KeyDown(object sender, KeyEventArgs e)
		{
			if (logging) log("in Form1_KeyDown " + ((int)e.KeyData).ToString("x2"));

			// First of all, be ready to go away!
			if (e.KeyCode == Keys.Escape)
			{
				if (logging) log("ESCAPE key = EXIT");
				Application.Exit();
			}

			if (e.KeyCode == Keys.ControlKey)
			{
				// here to avoid setting PaintingRequired = true, which spoils the smooth scroll.
				// if (logging) log("CONTROL KEY");
			}
			else
			{
				// This ELSE is the alternative to ControlKey, not Control Status.
				// In the following block, process special function Keys with or without CONTROL status.
				{
					switch (e.KeyCode)
					{
							// INSERT / DELETE are just left of HOME / END and of PAGEUP / PAGEDOWN.
							// Let them serve as my scrolling STOP / START keys.

                        case Keys.Insert: // Bare or any Insert = START SCROLLING
							{
                                if (logging) log("INSERT Key = START SCROLLING");
                                myLinesPerSec = StartingLinesPerSec;
                                myMsPerScanline = (int)Math.Round(1000d / (myLinesPerSec * (double)myIntegerFontHeight));
                                if (logging) log("myMsPerScanline = " + myMsPerScanline);

								ScrollingEnabled = true; // due to INSERT Key = START SCROLLING
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

                        case Keys.Delete: // Bare or any Delete = STOP SCROLLING
							{
								if (logging) log("DELETE Key = STOP SCROLLING");
								ScrollingEnabled = false; // due to DELETE Key = STOP SCROLLING
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;
						
                        case Keys.Home: // Bare or Any Home = Stop scrolling, go to top of document
							{
								if (logging) log("HOME key");
								ScrollingEnabled = false; // during HOME key

								if (ScrolledToScanLine > 0
								    && ScrolledToScanLine < TotalScanLines)
								{
									SaveScrollScanLine = ScrolledToScanLine; // set during HOME key
								}
								ScrolledToScanLine = 0;
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

                        case Keys.End: // Bare or Any End = Stop scrolling, go to end of document
							{
								if (logging) log("END key");
								ScrollingEnabled = false; // during HOME key

								if (ScrolledToScanLine > 0
								    && ScrolledToScanLine < TotalScanLines)
								{
									SaveScrollScanLine = ScrolledToScanLine; // set during HOME key
								}
								ScrolledToScanLine = TotalScanLines; // i.e., just past end of all text
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

                        case Keys.Down: // Bare or any Arrow Down = STOP SCROLLING, and MINIMIZE
                            {
                                if (logging) log("DOWN key = STOP SCROLLING, and MINIMIZE");
                                {
                                    ScrollingEnabled = false; // during Arrow DOWN key
                                    WindowState = FormWindowState.Minimized;
                                    PaintingRequired = true;
                                }
                                keypressAccumulator = string.Empty;
                                AccuPowerTen = 1;
                                AccuDigitRun = 0;
                            }
							break;

                        case Keys.Up: // Bare or any Arrow Up = START/RESUME SCROLLING
                            {
                                if (logging) log("UP key = START/RESUME SCROLLING");
                                {
                                    // A Key cannot cause app to rise up from minimized state!
                                    // -- Instead, assign a Windows hotkey (Ctl-Alt-L).
                                    // WindowState = FormWindowState.Normal;
                                    ScrollingEnabled = true;
                                    PaintingRequired = true;
                                }
                                keypressAccumulator = string.Empty;
                                AccuPowerTen = 1;
                                AccuDigitRun = 0;
                            }
                            break;

                        case Keys.Tab: // Bare or any TAB = STOP SCROLLING, and MINIMIZE
							{
								if (logging) log("TAB key");
								ScrollingEnabled = false; // during TAB key
                                WindowState = FormWindowState.Minimized;
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

                        case Keys.PageUp: // Simple Navigation, no change to scrolling state
							{
								if (logging) log("PAGE UP key");
								ScrolledToScanLine -= (ClientRectangle.Height - myIntegerFontHeight);
								if (ScrolledToScanLine < 0)
									ScrolledToScanLine = 0;
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

                        case Keys.PageDown: // Simple Navigation, no change to scrolling state
							{
								if (logging) log("PAGE DOWN key");
								ScrolledToScanLine += (ClientRectangle.Height - myIntegerFontHeight);
								if (ScrolledToScanLine > TotalScanLines)
									ScrolledToScanLine = TotalScanLines;
								PaintingRequired = true;
								keypressAccumulator = string.Empty;
								AccuPowerTen = 1;
								AccuDigitRun = 0;
							}
							break;

						case Keys.Left: // Bare or any Arrow Left = SCROLL SLOWER
							{
								if (logging) log("Arrow Left (slower)");
								myLinesPerSec /= myLPSFactor;
								if (myLinesPerSec < 0.1d)
									myLinesPerSec = 0.1d;
								if (logging) log("myLinesPerSec = " + myLinesPerSec.ToString("f2"));
								// I have TEXT lines per second. I need to compute ms per SCAN line.
								// 1000msPerSec / (LinesPerSec*myIntegerFontHeight(rastersPerTextLine)) = ms / raster
								myMsPerScanline = (int)Math.Round(1000d / (myLinesPerSec * (double)myIntegerFontHeight));
								if (logging) log("myMsPerScanline = " + myMsPerScanline);
								PaintingRequired = true;
							}
							break;
						case Keys.Right: // Bare or any Arrow Right = SCROLL FASTER
							{
								if (logging) log("Arrow Right (faster)");
								myLinesPerSec *= myLPSFactor;
								if (myLinesPerSec > 10.0d)
									myLinesPerSec = 10.0d;
								if (logging) log("myLinesPerSec = " + myLinesPerSec.ToString("f2"));
								// I have TEXT lines per second. I need to compute ms per SCAN line.
								// 1000msPerSec / (LinesPerSec*myIntegerFontHeight(rastersPerTextLine)) = ms / raster
								myMsPerScanline = (int)Math.Round(1000d / (myLinesPerSec * (double)myIntegerFontHeight));
								if (logging) log("myMsPerScanline = " + myMsPerScanline);
								PaintingRequired = true;
							}
							break;
							
					}
				}
				// In the next block, process ordinary printables that came with CONTROL status.
				if (e.Control)
				{
					// Due to CONTROL status do not regard SHIFT status, e.g., '5' versus '%'.
					switch (e.KeyCode)
					{
						case Keys.Q: // Control + 'Q' = Quit
						case Keys.X: // Control + 'X' = eXit
						case Keys.Z: // Control + 'Z' = End (mental artifact from DOS EOF)
							{
								// CONTROL keyDown events - which auto repeat while held (unless another KeyPress).
								if (logging) log("control Q-X-Z key");
								Application.Exit();
							}
							break;

						case Keys.A: // Control + 'A' = ANYWHERE (random jump)
							{
								if (logging) log("control A key");
								// cloning from jump code:
								{
									ScrolledToScanLine = rand.Next(TotalScanLines);
									PaintingRequired = true;
								}
							}
							break;

						case Keys.B: // Control + 'B' = Brown Background
							{
								if (logging) log("control B key");

								myColorChoice = Color.FromArgb(218, 170, 125); // Low Vision website suggested this (218, 170, 125) brown. Also rgb(74, 10, 5) instead of black.

								if (myColorChoice == Color.Lime)
									foreBrush = new SolidBrush(Color.Black);
								else
									foreBrush = new SolidBrush(Color.FromArgb(74, 10, 5)); // goes with rgb(218, 170, 125) bkgnd

								PaintingRequired = true;
							}
							break;

						case Keys.G: // Control + 'G' = Green Background
							{

								if (logging) log("control G key");

								myColorChoice = Color.Lime; // LIME is highest contrast, and monochromatic against black (no refraction rainbows)

								if (myColorChoice == Color.Lime)
									foreBrush = new SolidBrush(Color.Black);
								else
									foreBrush = new SolidBrush(Color.FromArgb(74, 10, 5)); // goes with rgb(218, 170, 125) bkgnd

								PaintingRequired = true;
							}
							break;

						case Keys.F: // Control + 'F' = FIND
							{
								if (logging) log("control F key");
								FindCommand(1); // forward
							}
							break;

						case Keys.R: // Control + 'R' = REVERSE FIND
							{
								if (logging) log("control R key");
								FindCommand(-1); // reverse
							}
							break;

						case Keys.J: // Control + 'J' = JUMP
							{
								if (logging) log("control J key");
								if (AccuPowerTen > 1)
								{
									ScrolledToScanLine = TotalScanLines * AccuDigitRun / AccuPowerTen;
									PaintingRequired = true;
								}
							}
							break;

						case Keys.V: // Control + 'V' = paste text from system clipboard
							{

								if (logging) log("control V key");
								if (Clipboard.ContainsText())
									RawText = Clipboard.GetText();
								else
									RawText = "No text in clipboard";
								MakeRawTextIntoCookedText();
								MakeCookedTextIntoWrappedLineArray();
								PaintingRequired = true;
							}
							break;

						case Keys.W: // Control + 'W' = toggle handling of multiple whitespaces in line
							{

								if (logging) log("control W key");
								myKeepSpacesMode = !myKeepSpacesMode;
								MakeRawTextIntoCookedText();
								MakeCookedTextIntoWrappedLineArray();
								PaintingRequired = true;
							}
							break;

						case Keys.O: // Control + 'O' = File Open
                            {
                                if (logging) log("control O key");
                                ScrollingEnabled = false; // during ^O = Open File
                                {
                                    OpenFileDialog ofd = new OpenFileDialog();
                                    ofd.Title = "Learn: Choose Input File";
                                    if (ofd.ShowDialog() == DialogResult.OK)
                                    {
                                        myLastInputFilePath = ofd.FileName;
                                        RawText = System.IO.File.ReadAllText(myLastInputFilePath, Encoding.UTF8);
                                        MakeRawTextIntoCookedText();
                                        MakeCookedTextIntoWrappedLineArray();
                                    }
                                    this.Activate();
                                }
                                PaintingRequired = true;
                            }
							break;

						case Keys.L: // Control + 'L' = Re-open Last input file
							{

								if (logging) log("control L key");
								if (System.IO.File.Exists(myLastInputFilePath))
								{
									RawText = System.IO.File.ReadAllText(myLastInputFilePath, Encoding.UTF8);
									MakeRawTextIntoCookedText();
									MakeCookedTextIntoWrappedLineArray();
									ScrollingEnabled = false; // during ^L = Last File
									PaintingRequired = true;
								}
							}
							break;

						case Keys.Oemplus: // Control + ('=' or '+') = bigger font
							{
								// First, solve the current viewing position's offset in input text.
								int InputOffset = 0;
								if(WrappedLineOffsets.Length > ScrolledToScanLine / myIntegerFontHeight)
									InputOffset = WrappedLineOffsets[ScrolledToScanLine / myIntegerFontHeight];

								// Second, do the size change.
								if (logging) log("Control plus key");
								myFontSize *= mySizeFactor;
								if (logging) log("myFontSize = " + myFontSize.ToString("f2"));
								SetFontAndComputeCharWidthsAndSpeed();

								// Third, try to set a scroll position to match that same input offset.
								for (int i = 1; i < WrappedLineOffsets.Length; i++) // skip 0 to allow - 1.
								{
									if (WrappedLineOffsets[i] > InputOffset)
									{
										ScrolledToScanLine = i * myIntegerFontHeight;
										break;
									}
								}
								PaintingRequired = true;
							}
							break;

                        case Keys.OemMinus: // Control + ('-' or '_') = smaller font
							{
								// First, solve the current viewing position's offset in input text.
								int InputOffset = 0;
								if (WrappedLineOffsets.Length > ScrolledToScanLine / myIntegerFontHeight)
									InputOffset = WrappedLineOffsets[ScrolledToScanLine / myIntegerFontHeight];

								// Second, do the size change.
								if (logging) log("Control minus key");
								myFontSize /= mySizeFactor;
								if (logging) log("myFontSize = " + myFontSize.ToString("f2"));
								SetFontAndComputeCharWidthsAndSpeed();

								// Third, try to set a scroll position to match that same input offset.
								for (int i = 1; i < WrappedLineOffsets.Length; i++) // skip 0 to allow - 1.
								{
									if (WrappedLineOffsets[i] > InputOffset)
									{
										ScrolledToScanLine = i * myIntegerFontHeight;
										break;
									}
								}
								PaintingRequired = true;
							}
							break;

						case Keys.M: // Control + 'M' = Mark the debugging log.
							{
								if (logging) log("Control M key");
								if (logging) log("USER MARK #" + ++logMarkCount);
							}
							break;
					}
					// Reset these AFTER (not before) any CONTROL + KEY uses them
					keypressAccumulator = string.Empty;
					AccuPowerTen = 1;
					AccuDigitRun = 0;
				}
				PaintingRequired = true; // near ex Form1_KeyDown
			}
			if (logging) log("ex Form1_KeyDown");
		}

		void Form1_KeyUp(object sender, KeyEventArgs e)
		{
			if (logging) log("in Form1_KeyUp " + ((int)e.KeyData).ToString("x2"));
			if (logging) log("ex Form1_KeyUp");
		}

		void Form1_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (logging) log("in Form1_KeyPress");
			if (e.KeyChar >= ' '
			    && e.KeyChar <= '~')
			{
				keypressAccumulator += e.KeyChar;
				if(char.IsDigit(e.KeyChar))
				{
					AccuPowerTen *= 10;
					AccuDigitRun *= 10;
					AccuDigitRun += (int)(e.KeyChar - '0');
				}
				else
				{
					AccuPowerTen = 1;
					AccuDigitRun = 0;
				}
			}
			if (e.KeyChar == '\b' // Backspace
			    && keypressAccumulator.Length > 0)
			{
				keypressAccumulator = keypressAccumulator.Substring(0, keypressAccumulator.Length - 1);
				AccuPowerTen /= 10;
				AccuDigitRun /= 10;
				if (AccuDigitRun == 0)
				{
					AccuPowerTen = 1;
					AccuDigitRun = 0;
				}
			}
			if (logging) log("ex Form1_KeyPress");
		}




		void CommonTo_ShownOr_SizeChanged()
		{
			if (logging) log("in CommonTo_ShownOr_SizeChanged");
			{
				myBackingAndFontAvailableMutex.WaitOne(); // locks out hotPaintWorker before starting to resize form1
				if (logging) log("in Mutex");
				{
					DiscardBacking();
					CreateBacking(); // in CommonTo_ShownOr_SizeChanged

					if (WindowState == FormWindowState.Minimized)
					{
						if (logging) log("stable=F(minimized)");
						myClientSizeIsStable = false; // in case window is Minimized upon leaving CommonTo_ShownOr_SizeChanged
					}
					else
					{
						if (logging) log("stable=T");
						myClientSizeIsStable = true; // becomes stable if Normal or Maximized upon leaving CommonTo_ShownOr_SizeChanged
					}
					// It is not my job to re-wrap any text,
					// because I have no font on first call.
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex(); // permits hotPaintWorker after resizing form1
			}
			if (logging) log("ex CommonTo_ShownOr_SizeChanged");
		}

		void SetFontAndComputeCharWidthsAndSpeed()
		{
			if (logging) log("in SetFontAndComputeCharWidthsAndSpeed");
			{
				myBackingAndFontAvailableMutex.WaitOne(); // locks out hotPaintWorker before changing font (and line buffer)
				if (logging) log("in Mutex");
				{
					if (myFontSize <= 1f) // I once saw zero during IDE debug run
						myFontSize = 1f;
					if (myFontSize > 400f) // total nonsense
						myFontSize = 400f;

					myFormFont = new Font(System.Drawing.FontFamily.GenericSansSerif, myFontSize * 96f / 72f, FontStyle.Regular, GraphicsUnit.Pixel);

					myIntegerFontHeight = myFormFont.Height;
					if (logging) log("myIntegerFontHeight = " + myIntegerFontHeight);

					RecomputeCharWidths(); // in SetFontAndComputeCharWidthsAndSpeed

					myMsPerScanline = (int)Math.Round(1000d / (myLinesPerSec * (double)myIntegerFontHeight));
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex(); // permits hotPaintWorker after changing font (and line buffer)
			}
			PaintingRequired = true; // in SetFontAndComputeCharWidthsAndSpeed
			if (logging) log("ex SetFontAndComputeCharWidthsAndSpeed");
		}

		void RecomputeCharWidths()
		{
			if (logging) log("in RecomputeCharWidths");
			// My caller did WaitOne;
			// BY DESIGN, DO NOT COME HERE WHEN myBackingBuffer == null.
			// Otherwise, Downstream must test for CharWidth.Count == 0.
			CharWidth.Clear();
			Size proposedSize = new Size(int.MaxValue, int.MaxValue); // unlimited
			TextFormatFlags formatFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
			for (int i = 0; i <= 255; i++)
			{
				char c = (char)i;
				CharWidth.Add(c, TextRenderer.MeasureText(myBackingBuffer.Graphics, string.Empty + c, myFormFont, proposedSize, formatFlags).Width);
			}
			// If there is any text in view, it must be re-wrapped
			if (CookedText.Length > 0)
				MakeCookedTextIntoWrappedLineArray();
			// My caller will do Release.
			if (logging) log("ex RecomputeCharWidths");
		}

		void MakeRawTextIntoCookedText()
		{
			if (logging) log("in MakeRawTextIntoCookedText");
			string clean1 = NonASCII.Replace(RawText, "");
			string clean2 = PairedCRLF.Replace(clean1, "\n");
			string clean3 = SingleCR.Replace(clean2, "\n");
			if (myKeepSpacesMode)
			{
				// I need a write loop to convert TABS to SPACES.
				CookedText = clean3;
			}
			else
			{
				CookedText = MultipleTabsSpaces.Replace(clean3, " "); // this step should be optional, also ridding all newlines.
			}
			if (logging) log("ex MakeRawTextIntoCookedText");
		}

		void MakeCookedTextIntoWrappedLineArray()
		{
			if (logging) log("in MakeCookedTextIntoWrappedLineArray");
			{
				myBackingAndFontAvailableMutex.WaitOne(); // before I yank Charlie Brown's football, setting WrappedLineArray = null.
				if (logging) log("in Mutex");
				{
					// predict failure
					WrappedLineArrayCount = 0; // This zero will also act as a gate over the longer time of text wrapping computations
					TotalScanLines = 0; // This zero will also act as a gate over the longer time of text wrapping computations
					ScrolledToScanLine = 0;
					WrappedLineArray = null; // I dare not take this step outside the Mutex!
				}
				if (logging) log("ex Mutex");
				myBackingAndFontAvailableMutex.ReleaseMutex();
			}
			PaintingRequired = true; // early in MakeCookedTextIntoWrappedLineArray

			// I need both:
			// 1. CookedText, and
			// 2. CharWidth[]
			// I do not actually need myBackingBuffer etc.

			// But do not "return;". Rather, "if" over the block of work.

			int drawWidth = ClientRectangle.Width - leftMargin - rightMargin; // Copied because is in danger of changing
			Dictionary<char, int> CopiedCharWidth = CharWidth; // Protect from loss during font size change

			// I don't need to add arbitrary Height and Width limits here,
			// as long as my text drawing loop must always advance safely.
			if (CookedText == string.Empty
			    // No, got a copy now... || CharWidth.Count == 0
			    // || drawWidth < 96
			    // || drawWidth < 5 * myIntegerFontHeight
			    // || ClientRectangle.Height < myIntegerFontHeight / 2
			    || CopiedCharWidth.Count < 256)
			{
				if (logging)
				{
					// explain
					if (CookedText == string.Empty) log("no, CookedText == string.Empty");
					// if (drawWidth < 96) log("no, drawWidth < 96");
					// if (drawWidth < 5 * myIntegerFontHeight) log("no, drawWidth < 5 * myIntegerFontHeight");
					// if (ClientRectangle.Height < myIntegerFontHeight / 2) log("no, ClientRectangle.Height < myIntegerFontHeight / 2");
					if (CopiedCharWidth.Count < 256) log("no, CopiedCharWidth.Count < 256");
				}
			}
			else
			{
				String[] InputLines = CookedText.Split(new char[] { '\n' });
				List<string> OutList = new List<string>();
				List<int> OffsetList = new List<int>();

				int ConsecutiveNewlines = 2;
				int inputIndex = 0;
				foreach (string line in InputLines)
				{
					string printables = line; // Some earlier process will regulate input lines' whitespace

					if (printables.Length == 0)
					{
						// Blank input line
						if (++ConsecutiveNewlines <= 2)
						{
							OutList.Add(string.Empty); // Preserve up to two Newlines to separate paragraphs.
							OffsetList.Add(inputIndex);
							inputIndex += 1;
						}
						continue;
					}

					// Now break the input line to fit into drawWidth according to char widths.
					char[] ca = printables.ToCharArray(); // not empty

					int index0 = 0; // of the [zeroth] character of a piece
					int indexPast = 0; // for an instant remembers index0 to start next loop
					int indexSpace = -1; // of the last space in ca[]
					int sumWidths = 0; // of the piece
					int sumAfterSpace = 0; // of any last word

					for (int i = 0; i < ca.Length; i++)
					{
						char c = ca[i];
						if (CopiedCharWidth.ContainsKey(c) == false)
							continue; // skip aberrent char
						int cWidth = CopiedCharWidth[c];
						if (c == ' ')
						{
							indexSpace = i;
							sumAfterSpace = 0;
						}

						sumWidths += cWidth;
						sumAfterSpace += cWidth;

						if (sumWidths > drawWidth)
						{
							// Cannot fit current c at ca[i] into line.
							// But if only zero chars now, do place it.
							// Also, if a space character, do place it.
							if (index0 == i || indexSpace == i)
							{
								// Break input line after index i.
								indexPast = i + 1;
								sumWidths = 0; // Restart after this i.
							}
							else
							{
								// Break input line before index i.
								indexPast = i;
								sumWidths = cWidth; // Restart before this i.

								// Moreover, go back to the last space.
								if (indexSpace != -1)
								{
									indexPast = indexSpace + 1; // leaving space at end of line
									sumWidths = sumAfterSpace; // width of left-over word starts sum of next line
								}
							}

							// output the determined piece
							String s = new string(ca, index0, indexPast - index0);
							OutList.Add(s);
							OffsetList.Add(inputIndex + index0);
							// start another piece
							index0 = indexPast;
							indexSpace = -1;
						}
					} // end of for (int i = 0; i < ca.Length; i++)

					// After loop ran off end of ca:
					if (index0 < ca.Length)
					{
						// output final (or frequently, the only) piece
						String s = new string(ca, index0, ca.Length - index0);
						OutList.Add(s);
						OffsetList.Add(inputIndex + index0);
					}
					ConsecutiveNewlines = 1; // The actual input Newline counts as #1

					inputIndex += line.Length;
				} // end of foreach (string line in InputLines)

				if (logging) log("OutList.Count = " + OutList.Count);

				// Smooth Scrolling requires an Array to index.
				if (OutList.Count > 0)
				{
					WrappedLineArray = OutList.ToArray();
					WrappedLineOffsets = OffsetList.ToArray();
					WrappedLineArrayCount = WrappedLineArray.Length; // This non-zero makes HotPainter salivate.
					TotalScanLines = WrappedLineArrayCount * myIntegerFontHeight; // And/Or This non-zero makes HotPainter salivate.
					// Console.Out.WriteLine("WrappedLineArrayCount = " + WrappedLineArrayCount);
				}
				PaintingRequired = true; // at the completion of MakeCookedTextIntoWrappedLineArray
			}
			if (logging) log("ex MakeCookedTextIntoWrappedLineArray");
		}

		void hotPaintWorker()
		{
			if (logging) log(@"in \-\-\- hotPaintWorker (A THREAD)");
			// The hotPaintWorker must remain in a hot loop burning up computer cycles,
			// because timing based on WaitOne or Sleep shows 10 or 15 ms granularity.
			// Hmmm... Perhaps I should play with thread priority levels too?

			for (; !myFormIsClosing; )
			{
				// Is repainting required?

				// I need logic to do the occasional repaints as by Invalidate/OnPaint,
				// and otherwise, to do every ms-accurate repaint for smooth scrolling.

				// Many situations in the main thread set PaintingRequired = true;

                // If the screen was invalidated by other events, they already set PaintingRequired = true;
				// Otherwise, we need only consider current needs of smooth scrolling timing and scanlines.

				if (myClientSizeIsStable // just a casual pretest
					&& ScrollingEnabled
					&& TotalScanLines > ScrolledToScanLine)
				{
					if ((myRealTimeClock.ElapsedMilliseconds - myLastPaintingMs) >= myMsPerScanline)
					{
						// Faster? Later, I could add a multiple scanline per ms algorithm.
						ScrolledToScanLine++;
						PaintingRequired = true;
					}
				}

				Boolean calmYourself = false;

				if (PaintingRequired)
				{
					// This brace and the next (if) brace are a pair to emphasize the Mutex possesion boundary.
					{
						if (myBackingAndFontAvailableMutex.WaitOne(100)) // else, just skip an occaional ms's update
						{
							// Let's not log mutex in this other thread -- if (logging) log("in Mutex");
							if (myClientSizeIsStable // more rigorous retest
							    && myBackingBuffer != null
							    && myForm1GraphicsObject != null)
							{
								// It is safe to paint.
								PaintingRequired = false; // disarm
								long priorMs = myLastPaintingMs; // for logging dt
								myLastPaintingMs = myRealTimeClock.ElapsedMilliseconds;

								// Common clear
								myBackingBuffer.Graphics.Clear(myColorChoice);

                                if (ScrolledToScanLine >= TotalScanLines)
								{
									if (logging) log("!-empty");
									calmYourself = true;
								}
								else
								{
									// This is the real text drawing act.
									// I already did Clear;
									SolidBrush foreBrush = new SolidBrush(Color.Black);

									int firstTextLine = ScrolledToScanLine / myIntegerFontHeight;
									int rasterOffset = ScrolledToScanLine % myIntegerFontHeight;
									// TopMargin is nominal starting value for text at HOME position without smooth scrolling
									int paintAtPixel = TopMargin - rasterOffset;

									// Beep beep beep. She's backing up.
									while (firstTextLine > 0
									       && paintAtPixel + myIntegerFontHeight >= 0)
									{
										// paint line above the 0 location.
										// In fact, go back more than once,
										// for when the font is very small.
										firstTextLine--;
										paintAtPixel -= myIntegerFontHeight;
									}
									int firstPixel = paintAtPixel;
									int didLine = -1;
									int lastPixel = -1;
									for (
										int i = firstTextLine;
										paintAtPixel < ClientRectangle.Height && i < WrappedLineArrayCount;
										i++, paintAtPixel += myIntegerFontHeight
									)
									{
										didLine = i;
										lastPixel = paintAtPixel;
										// string prefix = WrappedLineOffsets[i].ToString(); // debugging - just to verify new code.
										myBackingBuffer.Graphics.DrawString(WrappedLineArray[i], myFormFont, foreBrush, (float)leftMargin, (float)paintAtPixel);
									}
									if (logging) log("!-paint +" + (myLastPaintingMs - priorMs) + ":" + firstTextLine + "-" + didLine + "/" + WrappedLineArrayCount + "@" + firstPixel + "-" + lastPixel + "+" + myIntegerFontHeight + "/" + ClientRectangle.Height);
									// Go on to Render.
								}

								// Common Render

								myBackingBuffer.Render(myForm1GraphicsObject);
							}
							else
							{
								if (logging) log("!-unstable");
								calmYourself = true;
							}
							// Let's not log mutex in this other thread -- if (logging) log("ex Mutex");
							myBackingAndFontAvailableMutex.ReleaseMutex();
						} // end of inside the Mutex atttempt
						else
						{
							// else -- no ReleaseMutex is necessary -- (the Mutex was not obtained).
							if (logging) log("!-busy");
						}
					} // end of outside the Mutex atttempt

					// I was going to draw a % of document position in form caption.
					// But this hot painter is executing in a different thread.
					// It may not be safe to change the form's Text property here.
					// Correct, I got the cross-thread exception (after divide zero).

					//if (TotalScanLines > 0)
					//{
					//    Text = (ScrolledToScanLine * 100 / TotalScanLines).ToString("d2") + "%";
					//}

				} // end of (PaintingRequired)
				// else -- no need to log lack of a need to paint in this hot loop.            }
				if (calmYourself)
					System.Threading.Thread.Sleep(300);
			} // end of for (; !myFormIsClosing; )
			if (logging) log(@"ex /-/-/- hotPaintWorker (A THREAD)");
		}

		static int lastDirection = 1;

		void FindCommand(int direction)
		{
			if (logging) log("in FindCommand");
			// what have I done? Shot my foot?
			// int stepNumber = -1;
			// int lastI = -1;
			try
			{
				// let direction: 0 = same, 1 = fwd, -1 = reverse.
				if (direction == 0)
					direction = lastDirection;
				else
					lastDirection = direction;

				// stepNumber = 100;
				if (keypressAccumulator.Length > 0)
				{
					// search for string (from current position)
					// stepNumber = 101;
					searchPattern = keypressAccumulator.ToLower();
					// stepNumber = 102;
					if (searchPattern[0] == '*')
					{
						// stepNumber = 103;
						searchPattern = searchPattern.Substring(1);
						// stepNumber = 104;
						matchBOW = false;
					}
					else
						matchBOW = true;
					// stepNumber = 105;
					if (searchPattern.Length > 0)
					{
						// stepNumber = 106;
						if (searchPattern[searchPattern.Length - 1] == '*')
						{
							// stepNumber = 107;
							searchPattern = searchPattern.Substring(0, searchPattern.Length - 1);
							// stepNumber = 108;
							matchEOW = false;
						}
						else
							matchEOW = true;
						// stepNumber = 109;
					}
				}
				// else // search again

				// stepNumber = 200;
				if (searchPattern.Length > 0)
				{
					// stepNumber = 201;
					// Adjust the current viewing moment to be 1/3 of height down from the top.
					int NowAt = (ScrolledToScanLine + ClientRectangle.Height / 3) / myIntegerFontHeight;
					// stepNumber = 202;
					if (direction < 0)
					{
						// stepNumber = 203;
						for (int i = NowAt - 1; i >= 0; i--) // reverse find
						{
							// lastI = i;
							// stepNumber = 204;
							if (WrappedLineArray[i].Length > 0
							    && WrappedLineArray[i].ToLower().Contains(searchPattern)
							    && VerifyContainsDetails(WrappedLineArray[i].ToLower(), searchPattern))
							{
								// stepNumber = 205;
								ScrolledToScanLine = i * myIntegerFontHeight - ClientRectangle.Height / 3;
								PaintingRequired = true;
								// stepNumber = 206;
								break;
							}
							// stepNumber = 207;
						}
						// stepNumber = 208;
					}
					else
					{
						// stepNumber = 209;
						for (int i = NowAt + 1; i < WrappedLineArrayCount; i++)
						{
							// lastI = i;
							// stepNumber = 210;
							if (WrappedLineArray[i].Length > 0
							    && WrappedLineArray[i].ToLower().Contains(searchPattern)
							    && VerifyContainsDetails(WrappedLineArray[i].ToLower(), searchPattern))
							{
								// stepNumber = 211;
								ScrolledToScanLine = i * myIntegerFontHeight - ClientRectangle.Height / 3;
								PaintingRequired = true;
								// stepNumber = 212;
								break;
							}
							// stepNumber = 213;
						}
						// stepNumber = 214;
					}
					// stepNumber = 215;
				}
				// stepNumber = 300;
			}
			catch (Exception e)
			{
				if (logging) log("exception " + e.Message);
			}
			if (logging) log("ex FindCommand");
		}

		bool VerifyContainsDetails(string lowercaseLine, string lowercasePattern)
		{
			// My 2 callers already proved:
			// if (WrappedLineArray[i].Length > 0
			//     && WrappedLineArray[i].ToLower().Contains(searchPattern)
			// are both true before calling me.

			// int stepNumber = -1;
			// int past = -1; // here for exception
			try
			{
				// stepNumber = 101;
				if (matchBOW == false && matchEOW == false)
					return true; // any match will do

				// Now examine each match to apply the BOW and EOW rules.
				int nowAt = 0;
				for (; ; )
				{
					// stepNumber = 201;
					int index = lowercaseLine.IndexOf(lowercasePattern, nowAt);
					// stepNumber = 202;
					if (index == -1)
						return false; // no more matches to examine
					// stepNumber = 203;
					bool Okay = true;
					if (matchBOW == true)
					{
						// stepNumber = 204;
						// Either, match must be atop line, or have a prior space
						if (index != 0
						    && char.IsLetter(lowercaseLine[index - 1]) == true)
						{
							// stepNumber = 205;
							Okay = false;
						}
					}
					// stepNumber = 206;
					if (matchEOW == true)
					{
						// stepNumber = 207;
						// Either, match must end lines, or have a following space
						int past = index + lowercasePattern.Length;
						// stepNumber = 208;
						if (past != lowercaseLine.Length
						    && char.IsLetter(lowercaseLine[past]) == true) // Exception: [past + 1] is too far!
						{
							// stepNumber = 209;
							Okay = false;
						}
					}
					// stepNumber = 210;

					if (Okay)
						return true; // match meets specifications

					// stepNumber = 211;

					nowAt = index + 1;
					// stepNumber = 211;
					if (nowAt == lowercaseLine.Length)
					{
						// stepNumber = 212;
						return false; // no more matches to examine
					}
				}
				// stepNumber = 300;
			}
			catch (Exception e)
			{
				if (logging) log("exception " + e.Message);
			}
			return false; // in case of exception
		}
		
		
		
	}
}
