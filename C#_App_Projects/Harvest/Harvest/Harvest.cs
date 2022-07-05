/*
 * Harvest.cs
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
// using System.Threading;
using System.Web; // Must add a reference to GAC: System.Web
using System.Windows.Forms;
//using System.Xml.XPath;
//using HtmlAgilityPack;

using HtmlToText;


//Harvest assumes the Harvest "Q" (QUEUE) folder is peer to executable.
//Harvest processes each subfolder of Q, unless starts with hyphen = Not In Service.
//Within each subfolder of Q:
//- Harvest processes input files (ask.txt, want.txt, site.txt) to search and fetch web information.
//- Harvest writes RAW HTTP DATA into subfolder "#" and VALUABLE .txt, .pdf, etc into subfolder "$".

//rem this assumes ...\EXE contains SRCS contains Solution:
//Copy both EXE and DLLS to 2 levels above solution folder:
//copy "$(TargetPath)" "$(SolutionDir)\..\..\$(TargetFilename)"
//copy "$(TargetDir)\*.dll" "$(SolutionDir)\..\..\"

namespace Harvest
{
	/// <summary>
	/// Triggered every 1 minute, checks tasks in .\Q and leisurely pushes any along.
	/// </summary>
	public static class Harvest
	{
		public static Timer chronic = null; // started by MainForm.

		static DateTime lastWork = DateTime.MinValue;
		
		static bool firstEvent = true;

		public static void tickTock(object sender, EventArgs e)
		{
			chronic.Stop();
			
			// Well, I am here!
			try
			{
				if(firstEvent)
				{
					firstEvent = false; // just once:
					
					// SharpDevelop only allows me to go up to .NET 4.5.2.
					
					// Below .Net 4.6, the desired use of TLS1.2 is *NOT*
					// the Windows default, but I can obtain it, thus:

					//ServicePointManager.SecurityProtocol Property
					//Namespace:
					//    System.Net

					ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
					
				}
				
				if(doWorkQuantum() == false) // Each Work Quantum is atomic - rewrite lists, start afresh
				{
					// Upon no work to perform...
					// but only after some looping, longer than the same-domain-busy interval [70..140] seconds ...
					// Oh, but also on the first loop with no work!
					if (DateTime.Now - lastWork > TimeSpan.FromSeconds(300)) // test if global timeout elapsed
					{
						anote("Harvest finished"); // 2019-08-26

						// act like the Adams Family "Thing" that turns its own lever back off:
						try { File.Delete("KeepRunning.txt"); } catch (Exception) { }
						Application.Exit();
					}
				}
				else
				{
					// restart the global timeout
					lastWork = DateTime.Now;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Exception: " + ex.ToString());
				try { File.Delete("KeepRunning.txt"); } catch (Exception) { }
				Application.Exit();
			}
			
			if(File.Exists("KeepRunning.txt")) // Harvest, keep running!
			{
				chronic.Interval = 10000; // comes back every 10 seconds
				chronic.Start();
			}
			else
			{
				Application.Exit();
			}
		}
		
		const int msWorkQuantum = 10 * 60 * 1000; // max 10 minutes of fetching work
		const int msDwellIdle = 15 * 1000; // 15 seconds of DWELL TIME (not period) between initiations
		const int maxRedirections = 9;
		
		static Timer tt = new Timer();
		static Random rand = new Random();

		static Dictionary<string, DateTime> busyUntil = new Dictionary<string, DateTime>();

		// 2020-11-09 Wanted to add QWANT.COM, but result page is Javascript.

		// 2016-02-11 Realized duckduckgo needs /html/ for non-Javascript version

		// 2022-07-04 Finally dropped Google, who went full Javascript long ago.

		// 2022-07-04 Rehabilitating Searx, who changed their URL, thus:
		// Request-Url: https://searx.run/?q=Golang
		// Status: 308; Permanent Redirect
		// Location: https://searx.run/search?q=Golang

		static string[] queryHeads = {
			// Omit, gone Full Javascript -- "https://www.google.com/search?q=",
			"https://www.bing.com/search?q=",
			"https://search.yahoo.com/search?p=",
			"https://html.duckduckgo.com/html/?q=", // 2020-11-09 adding html. prefix
			"https://searx.run/search?q=",
			// "https://twitter.com/search?q=",
		};

		// "ask/add" = Search engine query terms or expressions / related idea. (Parsing to ADD is not implemented yet)
		// "site" = top level domain Url used to auto-add found urls into want.
		// "want/get" = Urls (including SE query urls) that need to be fetched.
		// "have/got" = Urls that have already been fetched, to never re-fetch.
		// "link" = Anchors to manually examine, and place back into want file.
		
		static string[] ask = {}; // gone Global to test for uniqueness
		static List<string> nuAdd = new List<string>(); // gone Global so doFetch can Add to it.

		// HtmlToText does not receive depth from Harvest directly, but as a header line "Depth: 2" in Harvest's html files.
		// int depth = 1; // 2016-02-16 added a depth-of-redirection concept
		
		// In order to implement a limit on redirections, and to avoid devious web robot traps,
		// I may write these URLs with an optional number prefix in the external want.txt file.
		// Thus: 2 https://www.facebook.com/unsupportedbrowser
		// I will pass such prefixed URLs into and out of HtmlToText. No prefix means level 1.
		//
		// Since the process cycle includes, and may reprocess, saved raw (header+HTML) files,
		// those raw files need to record their depth/level, and any redirects from them will
		// report URLS at that depth/level + 1.
		//
		// I would leave the Request-Url line alone and create one new header line, "Depth: 2"
		
		// want holds what was input, and nuGet holds additions to that file, yet to be written.
		// Both want and nuGet strings can include the optional number, space prefix to the URL.
		static string[] wantArray = {}; // gone Global to test for uniqueness
		static List<string> nuGet = new List<string>(); // gone Global so doParse can Add to it.
		// But using .Contains() requires yet another string list without such number prefixes.
		// So anything in want or nuGet with a prefix will be added without a prefix to bareWG.
		static List<string> bareWG = new List<string>(); // such >= 2 stripped of number prefixes

		// So after writing this, I forgot... Reviewing, all fetches will be contained in this path:
		// site.txt line must contain http(s)://www.etc.etc/optional/path
		static string[] site = {}; // new idea, meaning gather all-most-some? files from this site.
		
		// have holds what was input, and nuGot holds additions to that file, yet to be written.
		// Since I do not re-fetch from have.txt, this file and lists do not have number prefix.
		static string[] haveArray = {}; // gone Global to test for uniqueness
		static List<string> nuGot = new List<string>(); // gone Global so doFetch can Add to it.

		static string[] linkArray = {}; // gone Global just cuz
		static List<string> nuLink = new List<string>(); // gone Global just cuz
		
		static string[] interestingHeaders = {
			// DIY -- "Status", // Status: 200 OK
			"Content-Type", // Content-Type: text/html; charset=utf-8
			"Content-Language", // Content-Language: da
			"Date", // Date: Tue, 15 Nov 1994 08:12:31 GMT
			"Last-Modified", // Last-Modified: Tue, 15 Nov 1994 12:45:26 GMT
			"Location", // (for redirections!) Location: http://www.w3.org/pub/WWW/People.html
			"Server", // Server: Apache/2.4.1 (Unix)
			"Warning", // Warning: 199 Miscellaneous warning
			"WWW-Authenticate", // WWW-Authenticate: Basic
		};
		static Regex nonPrintable = new Regex(@"[^ -~]");

		static char[] oneSpace = { ' ' }; // for .Split

		// This has gone partly into the mainform:
		
//		public static void StartChronicTask()
//		{
//			tt.Tick += new EventHandler(tt_Tick);
//			tt.Interval = msDwellIdle;
//			tt.Start();
//		}
//
//		static void tt_Tick(object sender, EventArgs e)
//		{
//			tt.Stop();
//			{
//				if(! File.Exists("KeepRunning.txt")) // to exit during timer tick
//				{
//					Application.Exit(); // Stop execution right now
//				}
//				try
//				{
//					doWorkQuantum(); // Each Work Quantum is atomic - rewrite lists, start afresh
//				}
//				catch (Exception)
//				{
//					Application.Exit();
//				}
//			}
//			tt.Start(); // keeping 500 * 1000 interval
//		}
		
		public static void fatal(string error)
		{
			// This did not exit? --- Application.Exit();
			// Maybe sometimes? -- Caller must do return.
			if(logFile != "")
			{
				File.AppendAllLines(logFile, new string [] {DateTime.Now.ToString() + " FATAL: " + error});
			}
			else
			{
				MessageBox.Show("FATAL:" + error, "Harvest");
			}
			Application.Exit();
		}

		public static void anote(string msg)
		{
			if(logFile != "")
			{
				File.AppendAllLines(logFile, new string [] { DateTime.Now.ToString() + " " + msg });
			}
		}

		// Where are we?
		// Was:
		static string topDirectory = ""; // = Application.ExecutablePath.Substring(0, n); // in G:\CHRONIC\ -above- \EXE\myfilename.exe
		// Is:
		// App set Cur Dir where EXE lives.
		static string logFile = ""; // Leave empty to not write a debug file
		static string workDirectory = "";
		static string eachDirectory = "";
		static string htmlDir = "";
		static string textDir = "";

		// Was:
		// all this code below to invoke the Html2Text DLL first.
		// Is:
		// A call within program:
		// string[] result = HtmlToText.ConvertHtmlFileToTextFile(htmlFilename, textFilename);
		
//		static bool firstUseofHtmlToTextDll = true;
//		static MethodInfo Html2Text = null;
//
//		public static void LoadHtmlToTextDll()
//		{
//			// Total cost of loading dll from SDCard was just eight (8) ms!
//
//			//filename HtmlToText.dll
//			//namespace HtmlToText
//			//public static class HtmlToText
//			//public static string ConvertHtmlFileToTextFile(string htmlPath, string textPath)
//
//			// One web clue said no path !?!?, also to omit the .dll on path:
//			// Another web clue said to build dll for target ANY, not 64 bit.
//			// No, everybody's clearly saying, do not prefix the path either!
//			// The dll is just expected to be in the same folder as this exe.
//			Assembly a = null;
//			try
//			{
//				a = Assembly.Load("HtmlToText");
//			}
//			catch(Exception ex)
//			{
//				fatal("Assembly.Load [HtmlToTextDll" + "] threw: " + ex.Message);
//				return;
//			}
//			if(a == null)
//			{
//				return;
//			}
//			// Well, that is some progress! It found the .dll in my cur dir.
//			// Now: FATAL: Type HtmlToText not found in HtmlToText
//			// Oh, that requires a full namespace.class
//			Type t = a.GetType("HtmlToText.HtmlToText");
//			if(t == null)
//			{
//				fatal("Type HtmlToText.HtmlToText not found in HtmlToTextDll");
//				return;
//			}
//			Html2Text = t.GetMethod("ConvertHtmlFileToTextFile");
//			if(Html2Text  == null)
//			{
//				fatal("Static Method ConvertHtmlFileToTextFile not found in Type HtmlToText in HtmlToTextDll");
//				return;
//			}
//
//			// Hereafter, the invocation of this static method is as follows:
//			//if(Html2Text != null)
//			//{
//			//    bool result = (bool)Html2Text.Invoke(null, new object[] {htmlFilename, textFilename});
//			//}
//
//		}
//
		static string getMD5Hash(string input)
		{
			using (MD5 md5Hash = MD5.Create())
			{
				byte[] ba = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < ba.Length; i++)
				{
					sb.Append(ba[i].ToString("x2"));
				}
				return sb.ToString();
			}
		}

		static Regex solverImpure = new Regex(@"[^ -~]");
		static Regex solverRidHttpsWww1 = new Regex(@"^https?://(www\d?\.)?", RegexOptions.IgnoreCase); // nor www1.
		static Regex solverRidPath = new Regex(@"/.*$");
		// For debug error message BAD NEXUS, add TLDN ending to this list:
		static Regex solverUsaTld1 = new Regex(@"\.(edu|gov|mil|museum|com|org|run|aero|net|news|info|biz|pro|arpa|name|mobi|int|site|live|church|group|dev|io)$");
		static Regex solverTwoLetterTld1 = new Regex(@"\.([a-z][a-z])$");
		static Regex solverUsaStateTld2 = new Regex(@"\.(dc|al|ak|az|ar|ca|co|ct|de|fl|ga|hi|id|il|in|ia|ks|ky|la|me|md|ma|mi|mn|ms|mo|mt|ne|nv|nh|nj|nm|ny|nc|nd|oh|ok|or|pa|ri|sc|sd|tn|tx|ut|vt|va|wa|wv|wi|wy)$");
		static Regex solverForeignTld2 = new Regex(@"\.(org|com|net|gov|edu|co|mil|blogspot|ac|info|biz|int|name|nom|gob|or|web|tm|pro|go|med|asso|tv|sch|gouv|coop|firm|art|store|rec|priv|ne|museum|mobi|ltd|in|ed|sc|presse|pp|nt|ind|id|arts|shop|school|sa|press|prd|pol|perso|per|pe|ngo|me|inf|i|hotel|gen|fm|c|b)$");
		
		static string SolveHost(string url)
		{
			// returns "" if unacceptable/indigestible.
			if(solverImpure.IsMatch(url))
				return "";
			string lcLine = url.ToLower();
			string domain = solverRidPath.Replace(solverRidHttpsWww1.Replace(lcLine, ""), "");
			string nexus = domain;
			if(solverUsaTld1.IsMatch(domain))
			{
				// Do purely United States domains
				string tail = domain.Substring(domain.LastIndexOf('.'));
				nexus = solverUsaTld1.Replace(domain, "");
				// Strip any leftmost domain dotted parts
				int n = nexus.LastIndexOf('.');
				if(n == -1)
				{
					return nexus + tail;
				}
				else
				{
					return nexus.Substring(n + 1) + tail;
				}
			}
			else
				if(solverTwoLetterTld1.IsMatch(domain))
			{
				// Do 2-char TLDs
				string cc = domain.Substring(domain.Length - 3); // Keep .XX
				string rest = domain.Substring(0, domain.Length - 3); // omit .XX
				// Do .XX.us
				if(cc == ".us" && solverUsaStateTld2.IsMatch(rest)) // only allow 2 char states: .dc.us
				{
					string stateEtc = rest.Substring(rest.Length - 3); // keep . with state
					string earlier = rest.Substring(0, rest.Length - 3); // omit .XX
					// Strip any leftmost domain dotted parts
					int n = earlier.LastIndexOf('.');
					if(n == -1)
					{
						return earlier + stateEtc + ".us";
					}
					else
					{
						return earlier.Substring(n + 1) + stateEtc + ".us";
					}
				}
				else
					if(solverForeignTld2.IsMatch(rest))
				{
					// Do DING.XX, non-USA TLD2s
					// Rid the DING
					int m = rest.LastIndexOf('.');
					string ding = rest.Substring(m); // keeping . with DING
					string prior = rest.Substring(0, m); // before .DING
					// Strip any leftmost domain dotted parts
					int n = prior.LastIndexOf('.');
					if(n == -1)
					{
						return prior + ding + cc;
					}
					else
					{
						return prior.Substring(n + 1) + ding + cc;
					}
				}
				else
				{
					// REST.CC with REST not matching those patterns
					// Strip any leftmost domain dotted parts
					int n = rest.LastIndexOf('.');
					if(n == -1)
					{
						return rest + cc;
					}
					else
					{
						return rest.Substring(n + 1) + cc;
					}
				}
			}
			// else -- Bizare - Ignore
			anote("Bad nexus = " + nexus);
			return "";
		}

		public static bool doWorkQuantum()
		{
			bool QuantumDidWork = false;
			// Each Work Quantum is atomic - rewrite lists, start afresh
			
			// Don't slow user down!
			System.Diagnostics.Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
			
			anote("Quantum started");

			try
			{
				topDirectory = AppDomain.CurrentDomain.BaseDirectory;

				// where to log reports
				// Name it here. Or, Leave logFile empty to not write a debug file:
				logFile = Path.Combine(topDirectory, "debug_Harvest.txt");
				if(logFile != "")
				{
					if(File.Exists(logFile) == false)
					{
						// Everybody else use File.AppendAllLines!
						File.WriteAllLines(logFile, new string []{ logFile + " Created " + DateTime.Now});
					}
					if(File.Exists(logFile) == false)
					{
						fatal("Cannot find/create logfile: " + logFile);
						return false; // to exit, whether or not QuantumDidWork
					}
				}
				// where to work
				
				workDirectory = Path.Combine(topDirectory, "Q");
				if(Directory.Exists(workDirectory) == false)
				{
					Directory.CreateDirectory(workDirectory);
				}
				if(Directory.Exists(workDirectory) == false)
				{
					fatal("Cannot find/create folder: " + workDirectory);
					return false; // to exit, whether or not QuantumDidWork
				}
				
				string[] dirs = Directory.GetDirectories(workDirectory);
				
				// 2019-04-03 As I sit with many scores of folders defined,
				// only the first few folders are getting any work done.
				// I could lengthen the quantum, tweak other parameters.
				// But better, randomize the order of subfolder choices:

				List<string> lsDir = new List<string>();
				for(int i = 0; i < dirs.Length; i++)
				{
					int j = rand.Next(9000) + 1000;
					lsDir.Add(j.ToString() + dirs[i]);
				}
				lsDir.Sort();
				dirs = lsDir.ToArray();
				
				Stopwatch sw = new Stopwatch();
				sw.Start();
				foreach(string each4 in dirs)
				{
					string each = each4.Substring(4); // rid randomizer
					
					eachDirectory = each;
					// Do work in each subfolder of ...\Q\:
					
					// Yes, randomization worked... anote("In " + eachDirectory);

					// outer test
					if(sw.ElapsedMilliseconds > msWorkQuantum)
						break;
					if(! File.Exists("KeepRunning.txt")) // to break from dir in dirs loop, to fetch
						break;
					
					if(Path.GetFileName(eachDirectory).StartsWith("-")) // this -folder name is out of play
						continue;

					// Re-init globals for each subfolder

					string askFile = Path.Combine(eachDirectory, "ask.txt");
					string siteFile = Path.Combine(eachDirectory, "site.txt");
					string wantFile = Path.Combine(eachDirectory, "want.txt");
					string haveFile = Path.Combine(eachDirectory, "have.txt");
					string linkFile = Path.Combine(eachDirectory, "link.txt");
					string addFile = Path.Combine(eachDirectory, "add.txt");

					ask = new string[] {};
					site = new string[] {};
					wantArray = new string[] {};
					haveArray = new string[] {};
					linkArray = new string[] {};
					if(File.Exists(askFile))
						ask = File.ReadAllLines(askFile); // gone Global to test for uniqueness
					if(File.Exists(siteFile))
					{
						// BIG PROBLEM from BLANK LINE(S) AT EOF == Matches all sites!
						string [] sa = File.ReadAllLines(siteFile);
						List<string> ls = new List<string>();
						foreach(string urlLine in sa)
						{
							if(urlLine.StartsWith("http"))
							{
								ls.Add(urlLine);
							}
							else
							{
								// warn:
								anote("Ignoring bad URL line [" + urlLine + "] in " + siteFile);
							}
							
						}
						site = ls.ToArray(); // gone Global like the other lists
					}
					if(File.Exists(wantFile))
						wantArray = File.ReadAllLines(wantFile); // gone Global to test for uniqueness
					if(File.Exists(haveFile))
						haveArray = File.ReadAllLines(haveFile); // gone Global to test for uniqueness
					if(File.Exists(linkFile))
						linkArray = File.ReadAllLines(linkFile); // gone Global to test for uniqueness
					
					nuGet = new List<string>(); // gone Global so doParse can Add to it.
					nuGot = new List<string>(); // gone Global so doFetch can Add to it.
					nuAdd = new List<string>(); // gone Global so doFetch can Add to it.
					nuLink = new List<string>(); // gone Global so doFetch can Add to it.

					// Ensure to append a newline after any final manually editted lines.
					nuGet.Add("");
					nuGot.Add("");
					nuAdd.Add("");
					nuLink.Add("");


					// 2016-11-28 site implies want, which I enact in nuGet!
					// but wait, maybe I already did this. Check in old list.
					foreach(string url in site)
					{
						if(wantArray.Contains(url))
							continue;
						if(nuGet.Contains(url))
							continue;
						nuGet.Add(url);
					}
					// Program does not write back to site.txt.
					
					// Starting 2016-02-16, the want.txt file might have a number+space prefix on line.
					// wantArray and nuGet will keep any number prefix, but I need a bare URL to match:
					foreach(string preitem in wantArray)
					{
						if(preitem.Length > 1 && char.IsDigit(preitem[0]))
						{
							// line shall contain N digits then 1 space then url
							string [] sa = preitem.Split(oneSpace);
							if(sa.Length == 2)
							{
								bareWG.Add(sa[1]); // the bare URL
							}
						}
						
					}
					htmlDir = Path.Combine(eachDirectory, "#");
					textDir = Path.Combine(eachDirectory, "$");
					if(Directory.Exists(htmlDir) == false)
						Directory.CreateDirectory(htmlDir);
					if(Directory.Exists(textDir) == false)
						Directory.CreateDirectory(textDir);
					
					// Phase 1: Processing Ask/Add into need/get.
					if(
						sw.ElapsedMilliseconds < msWorkQuantum
						&&
						File.Exists("KeepRunning.txt")
					)
					{
						foreach(string item in ask)
						{
							string line = item.Trim();
							if(line != "")
							{
								// for HttpUtility, Must add a reference to GAC: System.Web
								string tail = HttpUtility.UrlEncode(line);
								foreach(string head in queryHeads)
								{
									string url = head + tail;
									if(wantArray.Contains(url))
										continue;
									if(nuGet.Contains(url))
										continue;
									nuGet.Add(url);
									QuantumDidWork = true;
									
									//break; // 2017-04-14 temporary - Stop after doing Google
								}
							}
							// inner loop test (part1)
							if(sw.ElapsedMilliseconds > msWorkQuantum)
								break;
							if(! File.Exists("KeepRunning.txt")) // to break from item in want loop
								break;
						}
						
						// outer test (part1)
						if(sw.ElapsedMilliseconds > msWorkQuantum)
							break;
						if(! File.Exists("KeepRunning.txt")) // to break from item in want loop
							break;
					}

					// Phase 2: Processing want/get into have/got.
					if(
						sw.ElapsedMilliseconds < msWorkQuantum
						&&
						File.Exists("KeepRunning.txt")
					)
					{
						foreach(string preitem in wantArray)
						{
							string item = preitem;
							int depth = 1;
							if(preitem.Length > 1 && char.IsDigit(preitem[0]))
							{
								// line shall contain N digits then 1 space then url
								string [] sa = preitem.Split(oneSpace);
								if(sa.Length == 2)
								{
									item = sa[1]; // isolate the URL
									int.TryParse(sa[0], out depth);
								}
								// else
								// continue; // will skip work below; do not avoid loop limit check
							}
							
							if(haveArray.Contains(item))
								continue;
							if(nuGot.Contains(item))
								continue;
							
							// want.txt line should contain http(s)... URLs
							if(item.StartsWith("http", StringComparison.Ordinal))
							{
								if(ILikeThisUrl(item) == false)
								{
									continue; // I don't like youtube, github, etc.
								}
								// Need a general politeness mechanism per host (main domain).
								string host = SolveHost(item);
								if(host == "")
								{
									nuGot.Add(item); // else Harvest gets fixated on unfetched item
									continue; // badly formed domain name.
								}
								// Before invoking the politeness busy work,
								// pre-check if the file exists:
								{
									// cloned from doFetch:
									string mixedFile = Path.Combine(htmlDir, getMD5Hash(item) + ".txt"); // for header, 2NL, raw http content (but call it .txt)
									if(File.Exists(mixedFile))
									{
										anote("preFetch: File Exists: [" + mixedFile + "] for [" + item + "]");
										nuGot.Add(item); // probably good, as commented just above
										continue;
									}
								}
								if(busyUntil.ContainsKey(host) == false)
									busyUntil.Add(host, DateTime.MinValue);
								if(busyUntil[host] < DateTime.Now)
								{
									// I don't think haveArray nor nuGot should every have the number prefix:
									nuGot.Add(item); // whether or not doFetch was able - without any number prefix
									QuantumDidWork = true;
									doFetch(item, depth); // This is the ONLY call
									busyUntil[host] = DateTime.Now.AddSeconds(70 + rand.Next(70));
								}
							}
							// inner test (part2)
							if(sw.ElapsedMilliseconds > msWorkQuantum)
								break;
							if(! File.Exists("KeepRunning.txt")) // to break from item in want loop
								break;
						}
					}
					
					// Phase 3: Processing existing html files into text files.
					// Which may append to the want, have, add, and link files.
					if(
						sw.ElapsedMilliseconds < msWorkQuantum
						&&
						File.Exists("KeepRunning.txt")
					)
					{
						// 1. Do file-to-file work
						if(Directory.Exists(htmlDir))
						{
							if(Directory.Exists(textDir) == false)
								Directory.CreateDirectory(textDir);
							foreach(string file in Directory.GetFiles(htmlDir))
							{
								string textfile = Path.Combine(textDir, Path.GetFileName(file));
								if (File.Exists(textfile) == false)
								{
									QuantumDidWork = true;
									doParse(file, textfile); // call for NOT exists
								}
								else
									if (File.GetCreationTime(file) > File.GetCreationTime(textfile))
								{
									QuantumDidWork = true;
									File.Delete(textfile);
									doParse(file, textfile); // call for Out Of Date
								}
								// inner test
								if(sw.ElapsedMilliseconds > msWorkQuantum)
									break;
								if(! File.Exists("KeepRunning.txt")) // to break file re-processing loop
									break;
							}

							// inner test (part3)
							if(sw.ElapsedMilliseconds > msWorkQuantum)
								break;
							if(! File.Exists("KeepRunning.txt")) // to break from item in want loop
								break;
						}
					}
					
					
					// What did that phase change, need to update?
					if(nuGet.Count > 1)
						File.AppendAllLines(wantFile, nuGet.ToArray());
					if(nuGot.Count > 1)
						File.AppendAllLines(haveFile, nuGot.ToArray());
					if(nuAdd.Count > 1)
						File.AppendAllLines(addFile, nuAdd.ToArray());
					if(nuLink.Count > 1)
						File.AppendAllLines(linkFile, nuLink.ToArray());
				}
			}
			catch(Exception ex)
			{
				fatal("Exception: " + ex.ToString());
			}
			anote("Quantum finished" + (QuantumDidWork ? ", did work" : ", done."));
			return QuantumDidWork;
		}


		// Duplicate method used in both Harvest and HTML2TEXT code:
		public static bool ILikeThisUrl(string url)
		{
			// from moved, or from SERP hits, or from any other anchor references.
			if(url.Contains("youtube.com/"))
				return false;

			// 2022-05-24 I would like to see some github results now...
			// if(url.Contains("github.com/"))
				// return false;
			
			if(url.Contains("video.search.yahoo.com"))
				return false;
			
			if(url.Contains("images.search.yahoo.com"))
				return false;
			
			// rid foreigner links from https://en.wikipedia.org/
			int n = url.IndexOf(".wikipedia.org/");
			if(n > 2)
			{
				string language = url.Substring(n - 2, 2);
				if(language != "en")
					return false;
			}

			// I could add this rule, but they only seemed to come from Twitter
			//if(url.Contains("//fb.me/")) // exclude links into Facebook
			//		return false;
			
			return true; // I like Most URLs
		}
		
		static string doFetch(string url, int depth)
		{
			try
			{
				try
				{
					string url2 = new Uri(url).ToString(); // which does Canonicalize (like / after bare domain) and error check Url
					if(url != url2)
					{
						if(nuGot.Contains(url2) == false
						   && haveArray.Contains(url2) == false)
						{
							nuGot.Add(url2);
						}
						// good enuf - anote("Uri revised [" + url + "] into [" + url2 + "]");
						url = url2;
					}
				}
				catch(Exception ex)
				{
					anote("new URI[] threw " + ex.Message);
					return "";
				}
				// mixed = headers + blank line + raw http content
				string mixedFile = Path.Combine(htmlDir, getMD5Hash(url) + ".txt"); // for header, 2NL, raw http content (but call it .txt)
				anote("Fetching [" + url + "] to [" + mixedFile + "]");
				if(File.Exists(mixedFile))
				{
					anote("doFetch: File Exists: [" + mixedFile + "] for [" + url + "]");
					return "";
				}
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
				request.Credentials = CredentialCache.DefaultCredentials;
				request.AllowAutoRedirect = false; // DIY
				request.MaximumAutomaticRedirections = 10;
				request.Timeout = 60000;
				request.UserAgent = "HDL 1.0 (Harvest_Distill_Learn 2022-07-03 by IneffablePerformativity@gmail.com)";
				HttpWebResponse response = (HttpWebResponse)request.GetResponse ();

				using(FileStream fs = File.Create(mixedFile))
				{
					StringBuilder sb = new StringBuilder();
					// StatusCode is an enum, not like 200, but like HttpStatusCode.ok
					sb.Append("Request-Url: " + url + "\r\n");
					sb.Append("Status: " + response.StatusCode + "; " + response.StatusDescription + "\r\n");
					foreach(string field in interestingHeaders)
					{
						// Prevent CR or LF in hdr
						string hdr = nonPrintable.Replace(response.GetResponseHeader(field), "");
						// Skip if empty - But first 10000 from Alexa have them.
						if(hdr != "")
							sb.Append(field + ": " + hdr + "\r\n"); // first 10K were ":" only
					}
					if(depth > 1)
					{
						sb.Append("Depth: " + depth + "\r\n"); // one Blank Line to terminate headers
					}
					sb.Append("\r\n"); // one Blank Line to terminate headers
					byte[]ba = Encoding.UTF8.GetBytes(sb.ToString());
					fs.Write(ba, 0, ba.Length);

					response.GetResponseStream().CopyTo(fs);
					fs.Close();
				}
			}
			catch(Exception ex)
			{
				anote("doFetch threw [" + ex.Message + "] on [" + url + "]");
			}
			return ""; // no follow action
		}

		static void doParse(string htmlFilename, string textFilename)
		{
			// Was: Away a DLL, invoked thus:
//			if(firstUseofHtmlToTextDll)
//			{
//				firstUseofHtmlToTextDll = false;
//				LoadHtmlToTextDll();
//			}
//			if(Html2Text != null)
//			{
//				string[] results = (string[])Html2Text.Invoke(null, new object[] {htmlFilename, textFilename});
//			}
			
			// Is: compiled together, called thus:
			{
				string[] results = HtmlToText.HtmlToText.ConvertHtmlFileToTextFile(htmlFilename, textFilename);
				foreach(string result in results)
				{					
					if(result.StartsWith("error:", StringComparison.Ordinal)
					   ||
					   result.StartsWith("notes:", StringComparison.Ordinal)
					  )
					{
						anote(Path.GetFileName(htmlFilename) + ": " + result);
					}
					if(
						// These keywords ALL are EXACTLY 5 chars + 1 Colon:
						result.StartsWith("moved:", StringComparison.Ordinal) // either in header, or meta refresh
						||
						result.StartsWith("serp", StringComparison.Ordinal)  // Search Engine Result Page (X):
						||
						result.StartsWith("frame:", StringComparison.Ordinal) // no iframes / framesets are parsed yet
					)
					{
						// Eliminate URLS matching these encoded //:
						// Location: http://www.nytimes.com/glogin?URI=http%3A%2F%2Fwww.nytimes.com%2F2015%2F01%2F22%2Fbusiness%2Fsmallbusiness%2Fon-twitter-best-advertising-practices-include-narrow-targets-videos-and-brevity.html%3F_r%3D0
						// Second time around, they did not re-encode the colon!
						if(result.Contains("%2F%2F")) // Re-encoded //
						{
							// Ignore
							string badUrl = result.Substring(6).TrimStart();
							anote(Path.GetFileName(htmlFilename) + ": iSkip:" + badUrl);
						}
						else
						{
							// OK
							anote(Path.GetFileName(htmlFilename) + ": " + result);
							string preitem = result.Substring(6).TrimStart();
							// I am implementing a follow depth mechanism.
							// Items may be returned with a number prefix:
							string item = preitem;
							int depth = 1;
							if(preitem.Length > 1 && char.IsDigit(preitem[0]))
							{
								// line shall contain N digits then 1 space then url
								string [] sa = preitem.Split(oneSpace);
								if(sa.Length == 2)
								{
									item = sa[1]; // isolate the URL
									int.TryParse(sa[0], out depth);
								}
							}

							// This equality test will always use the BARE url.
							if(nuGet.Contains(item) == false
							   && wantArray.Contains(item) == false
							   && bareWG.Contains(item) == false
							  )
							{
								// now we can apply that redirection limit test:
								if(depth < maxRedirections)
								{
									nuGet.Add(preitem); // Record the addition to wantArray - now with any number prefix
									if(preitem != item)
										bareWG.Add(item); // now also note the URL item without the number prefix.
								}
							}
						}
					}
					if(
						// This keyword is 4 chars + 1 Colon, then a 3-digit priority, then Dbl-quoted string, then the absolute link.
						result.StartsWith("link:", StringComparison.Ordinal) // link: 123 "anchor text" http...
					)
					{
						string item = result.Substring(5).TrimStart();
						if(nuLink.Contains(item) == false
						   && linkArray.Contains(item) == false
						  )
						{
							// This is where ALL anchors from ordinary parsed pages get listed:
							nuLink.Add(item);
							
							// 2016-11-28 If an anchor's domain is found in site list,
							// add the anchor directly to the nuGet list -> want file!

							// So actually, 2 lines come here for every anchor:
							// link: ### http....
							// link: ### "anchor text" http...
							// Therefore ignore lines containing a Dbl Quote:
							// Also eliminate URLS matching these encoded //:
							// Also eliminate all "?" queries, /category/ /tag/
							if(
								item.IndexOf('"') == -1
								&&
								item.IndexOf('?') == -1
								&&
								item.Contains("%2F%2F") == false
								&&
								item.Contains("/tag/") == false
								&&
								item.Contains("/category/") == false
							)
							{
								// now, apply the URL processing that I did above for serp's:
								
								// I am implementing a follow depth mechanism.
								// Items may be returned with a number prefix:
								string preitem = item;
								int depth = 1;
								if(preitem.Length > 1 && char.IsDigit(preitem[0]))
								{
									// line shall contain N digits then 1 space then url
									string [] sa = preitem.Split(oneSpace);
									if(sa.Length == 2)
									{
										item = sa[1]; // isolate the URL
										int.TryParse(sa[0], out depth);
									}
								}

								// Oh, but only if the domain is listed in sites!
								// No wait, not the TLD, but the TLD+Path to URL!
								// verify by now that I have a url here...
								if(item.StartsWith("http"))
								{
									// http://.../...
									// https://.../...
									// 0123456789
									// int nSlash = item.IndexOf('/', 8);
									// // In fact, if no slash, ignore...
									// if(nSlash != -1)
									{
										// Wait, I don't need to extract the TLD any more...
										
										// remove path, keep slash
										// string topLevelDomain = item.Substring(0, nSlash + 1);
										
										// I still have to test the domain!
										// if(site.Contains(topLevelDomain) == true)
										
										// So instead, if the WHOLE URL of ANY
										// line of Site.txt,
										// including the http: or https: method,
										// exists in the URL being considered,
										// go for it...
										bool anyMatch = false;
										foreach(string desireSiteUrlWithPath in site)
										{
											if(item.Contains(desireSiteUrlWithPath))
												anyMatch = true;
										}
										// So: site.txt line must contain one whole URL with optional path:
										// http://www.etc.etc
										// https://www.etc.etc/
										// http(s)://www.etc.etc/optional/path
										// http(s)://www.etc.etc/optional/path/
										if(anyMatch)
										{
											anote("Site Match for preitem:[" + preitem + "]");
											// This equality test will always use the BARE url.
											if(nuGet.Contains(item) == false
											   && wantArray.Contains(item) == false
											   && bareWG.Contains(item) == false
											  )
											{
												// now we can apply that redirection limit test:
												if(depth < maxRedirections)
												{
													nuGet.Add(preitem); // Record the addition to wantArray - now with any number prefix
													if(preitem != item)
														bareWG.Add(item); // now also note the URL item without the number prefix.
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}
	}
}

