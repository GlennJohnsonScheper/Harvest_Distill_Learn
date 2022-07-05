/*
 * SortLinks.cs
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

//App assumes the Harvest "Q" folder is peer to executable.
//rem this assumes ...\EXE contains SRCS contains Solution:
//Copy both EXE and DLLS to 2 levels above solution folder:
//copy "$(TargetPath)" "$(SolutionDir)\..\..\$(TargetFilename)"
//copy "$(TargetDir)\*.dll" "$(SolutionDir)\..\..\"

namespace SortLinks
{
	class Program
	{
		/* code from Harvest, for reference,
		 * incorporated into code below...
		 * 
		static string[] queryHeads = {
			// Omit, gone Full Javascript -- "https://www.google.com/search?q=",
			"https://www.bing.com/search?q=",
			"https://search.yahoo.com/search?p=",
			"https://html.duckduckgo.com/html/?q=", // 2020-11-09 adding html. prefix
			"https://searx.run/search?q=",
			// "https://twitter.com/search?q=",
		};
		*/

		public static void Main(string[] args)
		{
			Console.WriteLine("SortLinks...");
			
			// TODO: Implement Functionality Here
			
			try
			{
				// Set Cur Dir just above Q folder.
				Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
				if(Directory.Exists("Q") == false)
				{
					Console.WriteLine("Found no Directory Q adjacent to executable.");
				}
				else
				{
					string[] dirs = Directory.GetDirectories("Q");
					foreach(string dir in dirs)
					{
						// Look for link.txt to process:
						string linkFilename = Path.Combine(dir, "link.txt");
						string sortFilename = Path.Combine(dir, "sort.txt");

                        if (File.Exists(linkFilename) == false)
                            continue;

						string[] lines = File.ReadAllLines(linkFilename);
						List<string> outLines = new List<string>();
						
						foreach(string line in lines)
						{
							if(line.StartsWith("http") == false)
								continue;
							if(line.Contains("/tag/"))
								continue;
							if(line.Contains("/category/"))
								continue;
							if(line.Contains("google.com/search"))
								continue;
							if(line.Contains("bing.com/search"))
								continue;
							if(line.Contains("yahoo.com/search"))
								continue;
							if(line.Contains("duckduckgo.com/search"))
								continue;
							if (line.Contains("searx.run/search"))
								continue;
							if (line.Contains("twitter.com/search"))
								continue;
							outLines.Add(line);
						}
						string[] outArray = outLines.ToArray();
						Array.Sort(outArray);
						List<string>spaced = new List<string>();
						string prior = "";
						foreach(string s in outArray)
						{
							int n1 = s.IndexOf("//");
							if(n1 > 0)
							{
								string s2 = s.Substring(n1 + 2);
								int n2 = s2.IndexOf("/");
								if(n2 > 0)
								{
									string s3 = s2.Substring(0, n2);
									if(prior != s3)
									{
										prior = s3;
										spaced.Add("");
									}
									spaced.Add(s); // original s, whole URL
								}
							}
						}
						File.WriteAllLines(sortFilename, spaced.ToArray());
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
//			Console.Write("Press any key to continue . . . ");
//			Console.ReadKey(true);
		}
	}
}