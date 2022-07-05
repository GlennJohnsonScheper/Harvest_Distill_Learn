/*
 * Distill.cs
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
//using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
// using System.Threading;
using System.Web; // Must add a reference to GAC: System.Web
//using System.Windows.Forms;
//using System.Xml.XPath;
//using HtmlAgilityPack;

//Regarding another app, Harvest:
//Harvest assumes the Harvest "Q" (QUEUE) folder is peer to executable.
//Harvest processes each subfolder of Q, unless starts with hyphen = Not In Service.
//Within each subfolder of Q:
//- Harvest processes input files (ask.txt, want.txt, site.txt) to search and fetch web information.
//- Harvest writes RAW HTTP DATA into subfolder "#" and VALUABLE .txt, .pdf, etc into subfolder "$".


//Distill assumes the Harvest "Q" folder is peer to executable.
//Distill creates the Reading "R" folder as peer to executable.
//Distill processes each subfolder of Q, unless starts with hyphen = Not In Service.
//Distill processes many .txt files of any subfolder "$" therein as input data.
//Distill creates one text file in R for each $ as intelligent reading data.
//Distill also copies any non-.txt files of $ into R\Other for user to open.

//rem this assumes ...\EXE contains SRCS contains Solution:
//Copy both EXE and DLLS to 2 levels above solution folder:
//copy "$(TargetPath)" "$(SolutionDir)\..\..\$(TargetFilename)"
//copy "$(TargetDir)\*.dll" "$(SolutionDir)\..\..\"

namespace Distill
{
	class Program
	{
		// These folders are right in the "EXE" folder containing this app,
		// by whatever name, and wherever it may be, on local or far drive:
		
		static string inputTopFolder = @"Q"; // as in Queue
		static string outputFolder = @"R"; // as in Reading
		static string nonTextOutputFolder = @"R\Other";

		// Distill will process "*.txt" input files (containing Harvest's output).
		// Distill will simply copy over any other input file extensions (.pdf, etc.).

		static string outputFilenameTop = Path.Combine(outputFolder, @"i"); // as in Intelligence
		static string outputFilenameEnd = @".txt";

		static Regex threeWords = new Regex(@"\w \w+ \w");
		public static void Main(string[] args)
		{
			Console.WriteLine("Transforming Q into R.");
			
			// TODO: Implement Functionality Here
			try
			{
				Distill();
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine("Distill: " + ex.Message);
				Console.Write("Press any key to continue . . . ");
				Console.ReadKey(true);
			}

			// uncomment while debugging...
			//Console.Write("Press any key to continue . . . ");
			//Console.ReadKey(true);
		}
		
		// If I make this a Sorted Dict, it will present output files in some perhaps sensible URL order:
		static SortedDictionary<string, List<string>> fileParagraphs = new SortedDictionary<string, List<string>>();
		
		static Dictionary<string, int> paragraphCounts = new Dictionary<string, int>();
		
		static void Distill()
		{
			// Set Cur Dir = the folder containing this application:
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
			
			if(Directory.Exists(inputTopFolder) == false)
			{
				Directory.CreateDirectory(inputTopFolder);
			}
			if (Directory.Exists(outputFolder) == false)
			{
				Directory.CreateDirectory(outputFolder);
			}
			if (Directory.Exists(nonTextOutputFolder) == false)
			{
				Directory.CreateDirectory(nonTextOutputFolder);
			}
			
			string[] dirs = Directory.GetDirectories(inputTopFolder); // on "Current" Drive (pbly E or F for USB)
			foreach(string dir in dirs)
			{
				string dirFilename = Path.GetFileName(dir);
				if(dirFilename.StartsWith("-")) // Skip over subfolders starting with hyphen = Not In Service
					continue;
				Console.WriteLine("Processing " + dir);
				string prefixFile = Path.Combine(dir, "ask.txt");
				string inputDir = Path.Combine(dir, "$"); // Harvest did write RAW WEB DATA into # and TEXT/PDF/ETC into $ subfolder
				if(Directory.Exists(inputDir) == false)
					continue;
				
				// Oops, gotta clear these Accumulators of Intelligence!
				fileParagraphs = new SortedDictionary<string, List<string>>();
				paragraphCounts = new Dictionary<string, int>();

				// Was *.txt, but now process all files, copying others.
				string[] files = Directory.GetFiles(inputDir);
				foreach(string filepath in files)
				{
					if(filepath.EndsWith(".txt") == false)
					{
						// copy non-.txt files
						string filename = Path.GetFileName(filepath);
						string destname = Path.Combine(nonTextOutputFolder, filename);
						if(File.Exists(destname) == false)
							File.Copy(filepath, destname);
						continue;
					}
					// Distill .txt files
					string[] lines = File.ReadAllLines(filepath);
					if(lines.Length < 10)
					{
						// Console.WriteLine("Skipping " + filename);
						continue; // typical of redirection
					}
					
					// 2016-09-27 Added a rule to exclude SERP:
					// 2022-07-04 added html. subdomain to duckduckgo:
					if(lines[0].StartsWith("https://www.google.com/search?q=")
					   || lines[0].StartsWith("https://www.bing.com/search?q=")
					   || lines[0].StartsWith("https://search.yahoo.com/search?p=")
					   || lines[0].StartsWith("https://duckduckgo.com/html/?q=")
					   || lines[0].StartsWith("https://html.duckduckgo.com/html/?q=")
					  )
					{
						continue; // Do not Distill Harvest's 4 Search Engine Result Pages
					}

					// 2017-04-04 Adding an heuristic to exclude GARBAGE "text" files.
					// Otherwise the viewer app, LEARN, runs the system out of memory.
					// Oh, this file is mostly binary, but the fetch/html2text process
					// changed all unprintables into '^' characters, which often stand
					// for foreign language characters in valid english text web pages.

					//=======asdf9======== Q\cwe1\$\d2af6ba840de36553edfb5277450942b.txt
					//http://code.gnucash.org/docs/C/gnucash-guide.mobi

					//GnuCash_Tutorial_and_Concepts_G^^^^^X^^,X^^,^^^^^^^^^^^^^^^^BOOKMOBI^^^^^^^^^_^^^H^^^^^^. ^^^^^^2^^^^^^^7%^^^^^^;^^^^^^^?^^^^ ^^Dr^^^^^^H^^^^^^^M^^^^^^^Q^^^^^^^U^^^^^^^Zl^^^^^^^^^^^^^^

					int oks = 0;
					int ngs = 0;
					for (int i = 0; i < 10; i++) // CYA 10 checked above
					{
						foreach(char c in lines[i])
						{
							if (c >= 'a' && c <= 'z') // explicit, ASCII lowercase
								oks++;
							else
								ngs++;
						}
					}
					int percentLowercase = 100 * oks / (oks + ngs + 1); // CYA/0

					// My garbage files had 11 Percent; Good RFCs are down to 45 %...
					if (percentLowercase < 30)
					{
						Console.WriteLine("Skipping " + percentLowercase + " % LC file: " + filepath);
						continue; // typical of garbage
						
						// 2020-05-05 I think to recover .MP3 files also.
						// Here is a # file top:
						// Oh, that won't work here,
						// as Harvest has already converted binaries to ^.
					}

					// TMI if you want to see all the skip above...
					Console.WriteLine("Processing " + filepath);
					
					// Group blocks of non-empty lines as "paragraphs"
					// Shall I assemble them, inserting CR LF or what?

					// Keep one list of all paragraphs per input file,
					// to be able to output desired parts in sequence.
					
					List<string> paragraphs = new List<string>();
					fileParagraphs.Add(filepath, paragraphs);
					
					// Keep a global pool of all paragraphs with count,
					// to exclude much navigation text, often repeated.
					
					string accu = "";
					
					// 2016-10-21 because I have some output w/o URL,
					// Always pass the top URL and any TITLE lines...
					
					// Here's a typical file output by html2text.dll:
					// ================
					//http://stackoverflow.com/questions/846994/how-to-use-html-agility-pack
					//TITLE: c# - How to use HTML Agility pack - Stack Overflow
					//
					//current community
					// ================
					
					// Alternatively, here's a file with no TITLE:
					// ================
					//https://www.openssl.org/docs/manmaster/apps/crl.html
					//
					///docs/manmaster/apps/crl.html
					//
					//NAME
					//...
					// ================
					
					// Oh, wait! I forgot how this all works!
					// I must force to accu into a paragraph.
					
					// There WILL be a blank line after URL and any TITLE.
					
					int lineCount = 0;
					foreach(string line in lines)
					{
						switch(++lineCount)
						{
							case 1:
								if(line.StartsWith("http"))
								{
									// append line to paragraph, adding one CRLF newline per line.
									accu += line + "\r\n";
									continue;
								}
								// otherwise, an error, but...?
								break;
							case 2:
								if(line.StartsWith("TITLE"))
								{
									// append line to paragraph, adding one CRLF newline per line.
									accu += line + "\r\n";
									continue;
								}
								// otherwise, no title...
								break;
						}
						// maybe, preserve whitespace...?
						if(line.Trim() == "")
						{
							// empty line finishes any paragraph
							if(accu != "")
							{
								// process previous paragraph, adding second CRLF after paragraph
								accu += "\r\n";
								// Console.WriteLine(accu);
								paragraphs.Add(accu);
								if(paragraphCounts.ContainsKey(accu) == false)
									paragraphCounts.Add(accu, 0);
								paragraphCounts[accu]++;
							}
							// empty line starts a new paragraph
							accu = "";
						}
						else
						{
							// append line to paragraph, adding one CRLF newline per line.
							accu += line + "\r\n";
						}
					}
					if(accu != "")
					{
						// process final paragraph, adding second CRLF after paragraph
						accu += "\r\n";
						// Console.WriteLine(accu);
						paragraphs.Add(accu);
						if(paragraphCounts.ContainsKey(accu) == false)
							paragraphCounts.Add(accu, 0);
						paragraphCounts[accu]++;
					}
					
				}

				// Having input all files, analyze the global pool.

				List<string> priorityQueue = new List<string>();
				foreach(KeyValuePair<string, int> kvp in paragraphCounts)
				{
					// Often a website will have 2 identical copies of all files. (at www. and unadorned)
					// Output as a block all items counted > 2 ...? times:
					if(kvp.Value > 4)
					{
						// But wait, skip garbage blocks under three words.
						if(threeWords.IsMatch(kvp.Key))
						{
							// sort by paragraph length before frequency count:
							string item = "" +
								kvp.Key.Length.ToString("d4") + ": " +
								kvp.Value.ToString("d4") + ": " +
								kvp.Key;
							priorityQueue.Add(item);
						}
					}
				}
				priorityQueue.Sort();
				priorityQueue.Reverse();
				
				List<string> generalities = new List<string>();
				generalities.Add("Generalities with Redundancy\r\n\r\n");
				foreach(string priorityParagraph in priorityQueue)
				{
					generalities.Add(priorityParagraph);
				}
				
				// Next, let's append each file, shorn of generalities:
				
				// Actually, let's prepare them into a priority queue:
				// Not a dict, a list, as they may have equal metrics.
				List<string> valuedBodies = new List<string>();
				
				// Oops. Too early to serialize; Yet to resort...
				// int fileOrdinal = 0;
				foreach(KeyValuePair<string, List<string>> kvp in fileParagraphs)
				{
					int metric = 0;
					StringBuilder sb = new StringBuilder();
					// later...
					// sb.Append("\r\n=======asdf" + (++fileOrdinal) + "======== "); // a quick unique ^F grep mark
					sb.Append(kvp.Key); // key = filename (not URL)
					sb.Append("\r\n");
					
					StringBuilder shunt = new StringBuilder();

					bool atopFile = true;
					bool hadPriorContent = false;
					
					foreach(string paragraph in kvp.Value)
					{
						// 2016-10-21 Like I forced the first 1-2 lines above,
						// now I must force the first URL+any Title paragraph.
						
						// Oh, here's the problem, some files do NOT
						// contain any TITLE at all/in top paragraph.
						// if(atopFile && paragraph.Contains("TITLE:"))
						// So just always do it:
						if(atopFile)
						{
							sb.Append(paragraph); // URL and (any) TITLE block
							atopFile = false;
							continue;
						}
						// Skip really redundant boilerplate
						if(paragraphCounts[paragraph] > 5)
							continue;
						
						// Shunt non-sentence material, and only add
						// if more real sentence material follows it,
						// but do not add prior to any real material.
						
						int nSents = CountSentencesInBlock(paragraph);
						metric += nSents; // paragraph.Length or nSents
						// Console.WriteLine("" + nSents);
						if(nSents > 0)
						{
							// found "real" material,
							if(shunt.Length > 0)
							{
								// add any shunted lines,
								if(hadPriorContent)
								{
									// but not above the top block.
									sb.Append(shunt);
								}
								shunt.Clear();
							}
							sb.Append(paragraph);
							hadPriorContent = true;
						}
						else
						{
							// top/end navigation, or inter-paragraph drivel.
							shunt.Append(paragraph);
						}
					}
					// final shunt of file gets dropped.
					// Save this file with its metric for sorting:
					valuedBodies.Add(metric.ToString("d8").PadLeft(8) + sb.ToString()); // 8 to strip...
				}
				valuedBodies.Sort();
				valuedBodies.Reverse();

				// I think File.WriteAllLines re-expands my CRLF's:
				// No... File.WriteAllLines(outputFilename, output);
				// DIY:
				{
					int fileOrdinal = 0;
					StringBuilder sb = new StringBuilder();
					
					if(File.Exists(prefixFile))
					{
						sb.Append(File.ReadAllText(prefixFile)); // The ASK.TXT contents
					}
					sb.Append("\r\n=======asdf" + (++fileOrdinal) + "======== "); // a quick unique ^F grep mark
					sb.Append(dirFilename); // key = directory filename (not URL)
					sb.Append("\r\n");
					foreach(string s in generalities)
					{
						sb.Append(s);
					}
					foreach(string s in valuedBodies)
					{
						sb.Append("\r\n=======asdf" + (++fileOrdinal) + "======== "); // a quick unique ^F grep mark
						sb.Append(s.Substring(8)); // 8 stripped.
					}
					File.WriteAllText(outputFilenameTop + dirFilename + outputFilenameEnd, sb.ToString());
				}
			}
		}
		
		static int CountSentencesInBlock(string block)
		{
			// block may have embedded CRLF between lines.
			int sum = 0;
			string[] lines = Regex.Split(block, "\r\n");
			foreach(string s in lines)
				sum += CountSentencesInString(s);
			return sum;
		}
		
		// Find sentence terminators with certain constraints.
		// Not if alnum follows directly; (Viz., require a space)
		// Allow certain other puncts to mix in the sequence.
		// Any prior letter is not lowercase; (else, ^)
		// Any next letter is not uppercase; (else, $)
		// Can I use '$' in an alternation?
		// Google says: bcat(\w+|$) Here we use the end-of-string anchor $ in an alternation.
		// Allow optional colon, :?, at end of string, $.
		static Regex clearTerminator = new Regex(
			"[a-z][.?!\")']*[.?!][.?!\")']*(:?$| [.?!\"(']*[A-Z])",
			RegexOptions.Compiled);
		
		static int CountSentencesInString(string paragraph) // , List<string> back)
		{
			int nSentences = 0;
			// Instead of .Split, use .Matches, and traverse each .Index,
			// find space from there, (any more analysis), split on space.
			MatchCollection matches = clearTerminator.Matches(paragraph);
			if(matches.Count > 0)
			{
				int lastTop = 0;
				foreach(Match m in matches)
				{
					int topOfMatch = m.Index; // but the space character is further yet.
					int spaceMatch = paragraph.IndexOf(' ', topOfMatch);
					// There will be spaceMatch == -1 at end of line!
					string sentence = "";
					if(spaceMatch == -1)
					{
						sentence = paragraph.Substring(lastTop);
						lastTop = paragraph.Length;
					}
					else
					{
						sentence = paragraph.Substring(lastTop, spaceMatch - lastTop);
						lastTop = spaceMatch + 1;
					}
					// Almost there:
					// Vet that, after any certain puncts, first letter is uppercase:
					foreach(char c in sentence)
					{
						if(char.IsUpper(c))
						{
							// back.Add(sentence + "\r\n");
							// report.Add("SENTENCE: //" + sentence +"//");
							nSentences++;
							break;
						}
						// Also allow a prior "+ " from my Html2Text:
						if(c == '(' || c == '\'' || c == '"' || c == '+' || c == ' ')
							continue;
						// back.Add("+ " + sentence + "\r\n");
						// report.Add("BASTARD: //" + sentence +"//");
						break;
					}
				}
				if(lastTop != paragraph.Length)
				{
					// back.Add("+ " + paragraph.Substring(lastTop) + "\r\n");
					// report.Add("REMNANT: //" + paragraph.Substring(lastTop) +"//");
				}
			}
			else
			{
				// back.Add("+ " + paragraph + "\r\n");
				// report.Add("NONSENSE: //" + paragraph +"//");
			}
			return nSentences;
		}
		
	}
}