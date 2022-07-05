/*
 * HtmlToText.cs
 * Changed from a DLL to direct calls.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web; // Must add a reference to GAC: System.Web
//using System.Windows.Forms;
using System.Xml.XPath;
using HtmlAgilityPack;

// This requires the HtmlAgilityPack (used to parse HTML result pages).
// In SharpDevelop, use Project, PackageManager, to obtain and ADD it.

// Then I copied and referenced "System.Xml.XPath.dll" right in my solution/project folder.
// It was found at: C:\Program Files (x86)\Microsoft SDKs\Silverlight\v5.0\Libraries\Client
// Totally lacking Silverlight and System.Xml.XPath.dll? Download Silverlight here:
// https://www.microsoft.com/en-us/download/details.aspx?id=7335

//App assumes the Harvest "Q" folder is peer to executable.
//rem this assumes ...\EXE contains SRCS contains Solution:
//Copy both EXE and DLLS to 2 levels above solution folder:
//copy "$(TargetPath)" "$(SolutionDir)\..\..\$(TargetFilename)"
//copy "$(TargetDir)\*.dll" "$(SolutionDir)\..\..\"

//2015-04-15 added a parse of Google SRP company phone numbers
//based on:
//<td id="rhs_block"...
//<div class="g"...
//<div class="_uXc hp-xpdbox"...


namespace HtmlToText
{
	/// <summary>
	/// Description of MyClass.
	/// </summary>
	public static class HtmlToText
	{
		static bool logHtmlStatistics = false;
		static string logHtmlStatisticsFilename = @"debug_HtmlToText.txt";
		static SortedDictionary<string,int> htmlStatisticsDict = new SortedDictionary<string,int>();

		static bool logCompanyPhones = true;
		static string logCompanyPhonesFilename = @"Company_Phones.txt";

		
		// ===============================================================
		// Another program which would invoke this dll might do as follows:
		// ===============================================================
		//
		//static bool firstUseofHtmlToTextDll = true;
		//static MethodInfo Html2Text = null;
		//
		//public static void LoadHtmlToTextDll()
		//{
		//	// Total cost of loading dll from SDCard was just eight (8) ms!
		//
		//	//filename HtmlToText.dll
		//	//namespace HtmlToText
		//	//public static class HtmlToText
		//	//public static string ConvertHtmlFileToTextFile(string htmlPath, string textPath)
		//
		//	// One web clue said no path !?!?, also to omit the .dll on path:
		//	// Another web clue said to build dll for target ANY, not 64 bit.
		//	// No, everybody's clearly saying, do not prefix the path either!
		//	// The dll is just expected to be in the same folder as this exe.
		//	Assembly a = null;
		//	try
		//	{
		//		a = Assembly.Load("HtmlToText");
		//	}
		//	catch(Exception ex)
		//	{
		//		fatal("Assembly.Load [HtmlToTextDll" + "] threw: " + ex.Message);
		//		return;
		//	}
		//	if(a == null)
		//	{
		//		return;
		//	}
		//	// Well, that is some progress! It found the .dll in my cur dir.
		//	// Now: FATAL: Type HtmlToText not found in HtmlToText
		//	// Oh, that requires a full namespace.class
		//	Type t = a.GetType("HtmlToText.HtmlToText");
		//	if(t == null)
		//	{
		//		fatal("Type HtmlToText.HtmlToText not found in HtmlToTextDll");
		//		return;
		//	}
		//	Html2Text = t.GetMethod("ConvertHtmlFileToTextFile");
		//	if(Html2Text  == null)
		//	{
		//		fatal("Static Method ConvertHtmlFileToTextFile not found in Type HtmlToText in HtmlToTextDll");
		//		return;
		//	}
		//
		//	// Hereafter, the invocation of this static method is as follows:
		//
		//	// Once:
		//
		//	//if(firstUseofHtmlToTextDll)
		//	//{
		//	//	firstUseofHtmlToTextDll = false;
		//	//	LoadHtmlToTextDll();
		//	//}
		//	//if(Html2Text == null)
		//	//	return;
		//
		//	// Once per file:
		//
		//	//if(Html2Text != null)
		//	//{
		//	//    string[] result = (string[])Html2Text.Invoke(null, new object[] {htmlFilename, textFilename});
		//	//}
		//}
		// ===============================================================

		static char[] aLinefeed = { '\n' };
		static char[] aColon = { ':' };
		static char[] aSemicolon = { ';' };
		static char[] aEqual = { '=' };
		static char[] aLessThan = { '<' };
		static char[] aGreaterThan = { '>' };
		static char[] anyQuoteOrSpace = { '\'', '"', ' ' };
		static char[] anyEndsCharset = {'\'', '"', ' ', '/', ',', ';', '>'}; // for a coarse parse of meta charset tag
		static char[] anyEndsUrl = {'\'', '"', ' ', ',', ';', '>'}; // omitting / for URL - for a coarse parse of meta refresh tag

		static char[] aComma = { ',' };
		static char[] caCrLfSp = {'\r', '\n', ' '};
		
		static string HexToUnicode(Match m)
		{
			// reHexEntities passes a hex string like &#....;
			// I must return a single Unicode char in string.
			// I can just do the hex to char math mindlessly,
			// next use of reNonCrLfUsAscii will revise to ascii.
			string matchString = m.ToString(); // ToString returns the actual captured string
			string hexChars = matchString.Substring(2, matchString.Length - 3); // trim 2 atop and 1 end
			uint val = 0;
			if(uint.TryParse(hexChars, System.Globalization.NumberStyles.AllowHexSpecifier, null, out val) == true
			   && val < 0x10000)
			{
				return "" + (char)(val);
			}
			return "";
		}
		
		static string Translator(Match m)
		{
			// reNonCrLfUsAscii passes me a single char outside [Printable | CR | LF].
			switch(m.ToString()) // ToString returns the actual captured string
			{
				case "\t":
					return " "; // TABS become SPACES

				case "\u2018": // Opening Single Quote
				case "\u2019": // Closing Single Quote
				case "\u201A": // Single Low Quote
				case "\u2032": // Math Prime
				case "\u0301": // COMBINING ACUTE ACCENT
				case "\u0300": // COMBINING GRAVE ACCENT
					return "'"; // Various become Ascii APOSTRAPHE

				case "\u201C": // LEFT DOUBLE QUOTATION MARK
				case "\u201D": // RIGHT DOUBLE QUOTATION MARK
				case "\u201E": // Double Low Quote
				case "\u2033": // Math DOUBLE PRIME
				case "\u3003": // DITTO MARK
				case "\u00AB": // Left Guillemot
				case "\u00BB": // Right Guillemot
					return "\""; // Various become Ascii Double Quote

				case "\u2010": // HYPHEN
				case "\u2013": // EN DASH
				case "\u2014": // EM DASH
				case "\u2212": // MINUS SIGN
				case "\u00AD": // SOFT HYPHEN
				case "\u2011": // NON-// breakING HYPHEN
				case "\u2043": // HYPHEN BULLET
					return "-"; // Various become Ascii Hyphen

				case "\u00A0": // NO-BREAK SPACE
				case "\u2003": // EM SPACE
				case "\u2002": // EN SPACE
				case "\u2007": // figure space
				case "\u200B": // . zero width space
				case "\u202f": // narrow no-break space
				case "\u2060": // word joiner
				case "\ufeff": // zero width no-break space
					return " "; // Various become Ascii Space

				case "\u203a": // for oft-seen &rsaquo; SINGLE RIGHT-POINTING ANGLE QUOTATION
					return ">"; // becomes GT

				case "\u2039": // balancing above
					return "<"; // becomes LT

					// Getting exhaustive: These few were frequent and missed up to now:
					//      291 [202c]
					//      288 [202a]
					//      221 [203a]
					//      210 [200e]
					//      107 [200f]
					//       29 [200a]
					//       19 [2028]
					//       18 [2192]
					//        9 [2039]
					//        3 [202b]
					//        2 [2020]
					//        2 [2015]
					//        2 [2009]
					//        1 [2023]
					//        1 [2021]
				case "\u2009":
				case "\u200a":
					return " ";
				case "\u200e": // LTR mark
				case "\u200f": // RTL
					return "";
				case "\u2015":
					return "_";
				case "\u2020": // Dagger
					return "*";
				case "\u2021": // Dbl Dagr
					return "**";
				case "\u2023": // Triang Bullet
					return ">";
				case "\u2028": // Line Separator
					return "\r\n";
				case "\u2029": // Parag Separator
					return "\r\n\r\n";
				case "\u202a": // directionality
				case "\u202b":
				case "\u202c":
					return "";
				case "\u2192": // Rtward arrow
					return ">";
					
					// Now let's adapt the LATIN 1 section:
					// Replace about 48 accented characters with look-alikes.
					// 0x8x, 0x9x, 0xax...
					// Wait, these are inappropriate - must be the OEM chart.
					
					// In fact, I already have Latin1 0xc0-0xff below...

					
					// case "\u00A0": // . NO-BREAK SPACE - appeared above.
					//	return " ";
				case "\u00A1": // ¡ INVERTED EXCLAMATION MARK
					return "!";
				case "\u00A2": // ¢ CENT SIGN
					return "cent";
				case "\u00A3": // £ POUND SIGN
					return "Pound";
				case "\u00A4": // ¤ CURRENCY SIGN
					return "$";
				case "\u00A5": // ¥ YEN SIGN
					return "Yen";
				case "\u00A6": // ¦ BROKEN BAR
					return "|";
				case "\u00A7": // § SECTION SIGN
					return "Sect";
				case "\u00A8": // ¨ DIAERESIS
					return "..";
				case "\u00A9": // © COPYRIGHT SIGN
					return "(c)";
				case "\u00AA": // ª FEMININE ORDINAL INDICATOR
					return "a";
					//case "\u00AB": // « LEFT-POINTING DOUBLE ANGLE QUOTATION
					//	return "<<"
				case "\u00AC": // ¬ NOT SIGN
					return "NOT";
					//case "\u00AD": // . SOFT HYPHEN
					//	return "-";

				case "\u00AE": // ® REGISTERED SIGN
					return "(R)";
				case "\u00AF": // ¯ MACRON
					return "_";
				case "\u00B0": // ° DEGREE SIGN
					return "degree";
				case "\u00B1": // ± PLUS-MINUS SIGN
					return "+/-";
				case "\u00B2": // ² SUPERSCRIPT TWO
					return "2";
				case "\u00B3": // ³ SUPERSCRIPT THREE
					return "3";
				case "\u00B4": // ´ ACUTE ACCENT
					return "'";
				case "\u00B5": // µ MICRO SIGN
					return "mu";
					
					// Modified 2019-07-15: omit Pilcrow which leaves lots of 'P' chars after headings
//
//				case "\u00B6": // ¶ PILCROW SIGN
//					return "P";
//
					
					
				case "\u00B7": // · MIDDLE DOT
					return ".";
					
				case "\u00B8": // ¸ CEDILLA
					return "";
				case "\u00B9": // ¹ SUPERSCRIPT ONE
					return "1";
				case "\u00BA": // º MASCULINE ORDINAL INDICATOR
					return "o";
					//case "\u00BB": // » RIGHT-POINTING DOUBLE ANGLE QUOTATION
					//	return ">>";
				case "\u00BC": // ¼ VULGAR FRACTION ONE QUARTER
					return "1/4";
				case "\u00BD": // ½ VULGAR FRACTION ONE HALF
					return "1/2";
				case "\u00BE": // ¾ VULGAR FRACTION THREE QUARTERS
					return "3/4";
				case "\u00BF": // ¿ INVERTED QUESTION MARK
					return "?";
				case "\u00C0": // À LATIN CAPITAL LETTER A WITH GRAVE
					return "A";
				case "\u00C1": // Á LATIN CAPITAL LETTER A WITH ACUTE
					return "A";
				case "\u00C2": // Â LATIN CAPITAL LETTER A WITH CIRCUMFLEX
					return "A";
				case "\u00C3": // Ã LATIN CAPITAL LETTER A WITH TILDE
					return "A";
				case "\u00C4": // Ä LATIN CAPITAL LETTER A WITH DIAERESIS
					return "A";
				case "\u00C5": // Å LATIN CAPITAL LETTER A WITH RING ABOVE
					return "A";
				case "\u00C6": // Æ LATIN CAPITAL LETTER AE
					return "AE";
				case "\u00C7": // Ç LATIN CAPITAL LETTER C WITH CEDILLA
					return "C";
				case "\u00C8": // È LATIN CAPITAL LETTER E WITH GRAVE
					return "E";
				case "\u00C9": // É LATIN CAPITAL LETTER E WITH ACUTE
					return "E";
				case "\u00CA": // Ê LATIN CAPITAL LETTER E WITH CIRCUMFLEX
					return "E";
				case "\u00CB": // Ë LATIN CAPITAL LETTER E WITH DIAERESIS
					return "E";
				case "\u00CC": // Ì LATIN CAPITAL LETTER I WITH GRAVE
					return "I";
					
				case "\u00CD": // Í LATIN CAPITAL LETTER I WITH ACUTE
					return "I";
				case "\u00CE": // Î LATIN CAPITAL LETTER I WITH CIRCUMFLEX
					return "I";
				case "\u00CF": // Ï LATIN CAPITAL LETTER I WITH DIAERESIS
					return "I";
				case "\u00D0": // Ð LATIN CAPITAL LETTER ETH
					return "O";
				case "\u00D1": // Ñ LATIN CAPITAL LETTER N WITH TILDE
					return "N";
				case "\u00D2": // Ò LATIN CAPITAL LETTER O WITH GRAVE
					return "O";
				case "\u00D3": // Ó LATIN CAPITAL LETTER O WITH ACUTE
					return "O";
				case "\u00D4": // Ô LATIN CAPITAL LETTER O WITH CIRCUMFLEX
					return "O";
				case "\u00D5": // Õ LATIN CAPITAL LETTER O WITH TILDE
					return "O";
				case "\u00D6": // Ö LATIN CAPITAL LETTER O WITH DIAERESIS
					return "O";
				case "\u00D7": // × MULTIPLICATION SIGN
					return "X";
				case "\u00D8": // Ø LATIN CAPITAL LETTER O WITH STROKE
					return "O";
				case "\u00D9": // Ù LATIN CAPITAL LETTER U WITH GRAVE
					return "U";
				case "\u00DA": // Ú LATIN CAPITAL LETTER U WITH ACUTE
					return "U";
				case "\u00DB": // Û LATIN CAPITAL LETTER U WITH CIRCUMFLEX
					return "U";
				case "\u00DC": // Ü LATIN CAPITAL LETTER U WITH DIAERESIS
					return "U";
				case "\u00DD": // Ý LATIN CAPITAL LETTER Y WITH ACUTE
					return "Y";
				case "\u00DE": // Þ LATIN CAPITAL LETTER THORN
					return "TH";
				case "\u00DF": // ß LATIN SMALL LETTER SHARP S
					return "ss";

				case "\u00E0": // à LATIN SMALL LETTER A WITH GRAVE
					return "a";
				case "\u00E1": // á LATIN SMALL LETTER A WITH ACUTE
					return "a";
				case "\u00E2": // â LATIN SMALL LETTER A WITH CIRCUMFLEX
					return "a";
				case "\u00E3": // ã LATIN SMALL LETTER A WITH TILDE
					return "a";
					
				case "\u00E4": // ä LATIN SMALL LETTER A WITH DIAERESIS
					return "a";
				case "\u00E5": // å LATIN SMALL LETTER A WITH RING ABOVE
					return "a";
				case "\u00E6": // æ LATIN SMALL LETTER AE
					return "ae";
				case "\u00E7": // ç LATIN SMALL LETTER C WITH CEDILLA
					return "c";
				case "\u00E8": // è LATIN SMALL LETTER E WITH GRAVE
					return "e";
				case "\u00E9": // é LATIN SMALL LETTER E WITH ACUTE
					return "e";
				case "\u00EA": // ê LATIN SMALL LETTER E WITH CIRCUMFLEX
					return "e";
				case "\u00EB": // ë LATIN SMALL LETTER E WITH DIAERESIS
					return "e";
				case "\u00EC": // ì LATIN SMALL LETTER I WITH GRAVE
					return "i";
				case "\u00ED": // í LATIN SMALL LETTER I WITH ACUTE
					return "i";
				case "\u00EE": // î LATIN SMALL LETTER I WITH CIRCUMFLEX
					return "i";
				case "\u00EF": // ï LATIN SMALL LETTER I WITH DIAERESIS
					return "i";
				case "\u00F0": // ð LATIN SMALL LETTER ETH
					return "o";
				case "\u00F1": // ñ LATIN SMALL LETTER N WITH TILDE
					return "n";
				case "\u00F2": // ò LATIN SMALL LETTER O WITH GRAVE
					return "o";
				case "\u00F3": // ó LATIN SMALL LETTER O WITH ACUTE
					return "o";
				case "\u00F4": // ô LATIN SMALL LETTER O WITH CIRCUMFLEX
					return "o";
				case "\u00F5": // õ LATIN SMALL LETTER O WITH TILDE
					return "o";
				case "\u00F6": // ö LATIN SMALL LETTER O WITH DIAERESIS
					return "o";
				case "\u00F7": // ÷ DIVISION SIGN
					return "/";
				case "\u00F8": // ø LATIN SMALL LETTER O WITH STROKE
					return "o";
				case "\u00F9": // ù LATIN SMALL LETTER U WITH GRAVE
					return "u";
				case "\u00FA": // ú LATIN SMALL LETTER U WITH ACUTE
					return "u";
				case "\u00FB": // û LATIN SMALL LETTER U WITH CIRCUMFLEX
					return "u";
				case "\u00FC": // ü LATIN SMALL LETTER U WITH DIAERESIS
					return "u";
				case "\u00FD": // ý LATIN SMALL LETTER Y WITH ACUTE
					return "y";
				case "\u00FE": // þ LATIN SMALL LETTER THORN
					return "th";
				case "\u00FF": // ÿ LATIN SMALL LETTER Y WITH DIAERESIS
					return "y";
					
					// Now whatever else...
				case "\u2026": // HORIZONTAL ELLIPSIS
					return "...";

				case "\u2022": // Bullet
					return "*";
				case "\u20AC": // Euro
					return "Euro";
				case "\u2122": // Trademark
					return "(tm)";
				case "\u2248": // Almost Equal
					return "~";
				case "\u2260": // Not equal
					return "!=";
				case "\u2264": // Less or equal
					return "<=";
				case "\u2265": // Greater or equal
					return ">=";
			}
			char c = m.ToString()[0];
			if( c < 0x0100 && c > '~' // some missed Latin1
			   || c > 0x2000 && c < 0x22ff) // other puncts?
				return "[" + ((uint)c).ToString("x2") + "]"; // Wow! Hexdumped!
			return "^"; // A non-interesting foreign character in later text analysis
		}
		static Regex reTab = new Regex("\t"); // to change to spaces
		static Regex reHexEntities = new Regex("&#\\[0-9a-fA-F]+;"); // matches html hex entities -- do reHexEntities before reNonCrLfUsAscii
		static Regex reNonCrLfUsAscii = new Regex("[^ -~\r\n]"); // matches anything outside \r, \n, and space to tilde
		static Regex reCRLFs = new Regex("(\r\n|\r|\n)"); // to standardize all kinds of newlines
		static Regex reWhiteLines = new Regex("^ +$", RegexOptions.Multiline); // to clean all-whitespace lines
		static Regex reManyLines = new Regex("\n\n+"); // to reduce all newlines to 1 or 2 only
		static Regex reFatSpaces = new Regex("  +"); // to compress 2+ continuous spaces into 1
		static Regex reFirstSpace = new Regex("\n "); // to rid first space after newline
		static Regex reNonWord = new Regex("\\W+"); // to rid all non-word chars from Titles

		static Regex reTrimDuckDuckGoHitUrls = new Regex("&amp;rut=.*$"); // Finally noticed and rehabilitated 2022-07-04



		static string badCharsToHex(string input)
		{
			StringBuilder sb = new StringBuilder();
			foreach(char c in input)
			{
				if(c >= ' ' && c <= '~')
					sb.Append(c);
				//else if(c == '\r' || c == '\n') // uncomment these to preserve newlines
				//	sb.Append(c);				// uncomment these to preserve newlines
				else
					sb.Append("[" + ((int)c).ToString("x2") + "]");
			}
			return sb.ToString();
		}
		
		static void setEncodingPerString(string name, ref Encoding useEncoding, ref string rejectedEncoding, ref bool useBOM)
		{
			// How can I be reporting a non-match to "utf-8"?
			// Oh, because I am blind to their Double Quotes!
			string CharsetValue = name.Trim(anyQuoteOrSpace).ToLower();
			switch(CharsetValue)
			{
					// IANA has hundreds of CharSet aliases.
					// Add any new names here as encountered.
					
				case "utf8": // empircally without hyphen from web
				case "utf-8":
				case "csutf8":
					useEncoding = Encoding.UTF8;
					break;
					
				case "us-ascii":
				case "iso-ir-6":
				case "ansi_x3.4-1968":
				case "ansi_x3.4-1986":
				case "iso_646.irv:1991":
				case "iso646-us":
				case "us":
				case "ibm367":
				case "cp367":
				case "csascii":
					useEncoding = Encoding.ASCII;
					break;

				case "iso-8859-1":
				case "iso-ir-100":
				case "iso_8859-1":
				case "latin1":
				case "l1":
				case "ibm819":
				case "cp819":
				case "csisolatin1":
					// Wiki - Windows-1252 is a superset of ISO-8859-1:
					useEncoding = System.Text.Encoding.GetEncoding(1252);
					break;

					// I don't know if I care, but allow the other few:
				case "windows-1250":
				case "win-1250":
				case "win1250":
					useEncoding = System.Text.Encoding.GetEncoding(1250);
					break;
				case "windows-1251":
				case "win-1251":
				case "win1251":
					useEncoding = System.Text.Encoding.GetEncoding(1251);
					break;
				case "windows-1252":
				case "win-1252":
				case "win1252":
					useEncoding = System.Text.Encoding.GetEncoding(1252);
					break;
				case "windows-1253":
				case "win-1253":
				case "win1253":
					useEncoding = System.Text.Encoding.GetEncoding(1253);
					break;
				case "windows-1254":
				case "win-1254":
				case "win1254":
					useEncoding = System.Text.Encoding.GetEncoding(1254);
					break;
				case "windows-1255":
				case "win-1255":
				case "win1255":
					useEncoding = System.Text.Encoding.GetEncoding(1255);
					break;
				case "windows-1256":
				case "win-1256":
				case "win1256":
					useEncoding = System.Text.Encoding.GetEncoding(1256);
					break;
				case "windows-1257":
				case "win-1257":
				case "win1257":
					useEncoding = System.Text.Encoding.GetEncoding(1257);
					break;
				case "windows-1258":
				case "win-1258":
				case "win1258":
					useEncoding = System.Text.Encoding.GetEncoding(1258);
					break;
				case "windows-1259":
				case "wins-1259":
				case "win-1259":
					useEncoding = System.Text.Encoding.GetEncoding(1259);
					break;
					
					//Microsoft has assigned code page 28605 aka Windows-28605 to ISO-8859-15.
				case "iso-8859-15":
				case "iso_8859-15":
				case "windows-28605":
				case "wins-28605":
				case "win-28605":
					useEncoding = System.Text.Encoding.GetEncoding(28605);
					break;
					
					//a few others in ISO series seem similar:

				case "iso-8859-2":
				case "iso_8859-2":
					useEncoding = System.Text.Encoding.GetEncoding(28592);
					break;

				case "iso-8859-3":
				case "iso_8859-3":
					useEncoding = System.Text.Encoding.GetEncoding(28593);
					break;

				case "iso-8859-4":
				case "iso_8859-4":
					useEncoding = System.Text.Encoding.GetEncoding(28594);
					break;

				case "iso-8859-5":
				case "iso_8859-5":
					useEncoding = System.Text.Encoding.GetEncoding(28595);
					break;

				case "iso-8859-6":
				case "iso_8859-6":
					useEncoding = System.Text.Encoding.GetEncoding(28596);
					break;

				case "iso-8859-7":
				case "iso_8859-7":
					useEncoding = System.Text.Encoding.GetEncoding(28597);
					break;

				case "iso-8859-8":
				case "iso_8859-8":
					useEncoding = System.Text.Encoding.GetEncoding(28598);
					break;

				case "iso-8859-9":
				case "iso_8859-9":
					useEncoding = System.Text.Encoding.GetEncoding(28599);
					break;

				case "utf-7":
				case "csutf7":
					useEncoding = Encoding.UTF7;
					break;

				case "utf-16be":
				case "csutf16be":
					useEncoding = Encoding.BigEndianUnicode;
					break;

				case "utf-16":
				case "csutf16":
					useEncoding = Encoding.BigEndianUnicode; // Unicode says default is BE
					useBOM = true;
					break;

				case "utf-16le":
				case "csutf16le":
					useEncoding = Encoding.Unicode;
					break;

				default:
					rejectedEncoding = CharsetValue;
					break;
			}
		}
		
		public static void EnsureOutputFile(string textPath, string prefixOutput, List<string> results)
		{
			// Else, PIPE keeps reparsing erroneous input files without ceasing!
			List<string> toWrite = new List<string>();
			
			// It's funny, I totally turned NOTEPAD.EXE into a quivering mess
			// just because of 0d 0d 0a in the output file. So rectify these:
			StringBuilder sb = new StringBuilder();
			sb.Append(prefixOutput); // Any URL, TITLE (which lines end with CR,LF)
			sb.Append("\r\n");

			foreach(string line in results) // Errors, Moved, etc. (not the HTML to TEXT body)
			{
				sb.Append(line + "\r\n");
			}
			File.WriteAllText(textPath, sb.ToString());
		}
		
		public static string[] ConvertHtmlFileToTextFile(string htmlPath, string textPath)
		{
			// My caller already determined file datetimes, to gate off if desired.
			
			// I will now accept and parse a mixed file such as Pipe produces:
			// Header lines,
			// CRLFCRLF,
			// HttpContent.
			
			// I now return an array of strings, one of:
			// "okay" if no error and no redirection -- ACTUALLY, FOR NOW, A COPY LINE
			// "Request-Url: ..." When parsing the first line saying that.
			// "error: ..." to report any error message.  (And yet, I must make output file.)
			// "moved: ..." to queue up one redirection Uri. (And yet, I must make output file.)

			List<string> results = new List<string>();
			
			// Re-init these per file!
			bool parsedHeaderContentType = false;
			bool foundTextHtml = false;
			string whatContentType = "";
			string rejectedEncoding = "";
			Encoding useEncoding = Encoding.UTF8;
			string foundLanguage = "";
			bool useBOM = false;
			// HtmlToText does not receive depth from Pipe directly, but as a header line "Depth: 2" in Pipe's html files.
			int depth = 1; // 2016-02-16 added a depth-of-redirection concept
			
			string prefixOutput = ""; // get URL, nl, title, nl, ..., final nl

			try
			{
				byte[] ba = File.ReadAllBytes(htmlPath);
				int nHeaderBytes = 0;
				string baseUrl = "";
				string locnUrl = ""; // in headers
				bool useLocn = false;
				string refreshUrl = ""; // <meta refresh...
				
				// Pipe fetched files start with Request-Url: and have headers until first CRLFCRLF
				if(ba.Length > 20
				   && ba[0] == 'R'
				   && ba[7] == '-'
				   && ba[8] == 'U'
				   && ba[11] == ':')
				{

					// Find end of headers
					for(int i = 0; i < ba.Length; i++)
					{
						if(ba[i] == (byte)'\n'
						   // sure... && i > 2
						   && ba[i-2] == (byte)'\n')
						{
							nHeaderBytes = i + 1;
							break;
						}
					}
					
					// Process headers
					{
						string headers = Encoding.UTF8.GetString(ba, 0, nHeaderBytes);
						string[] lines = headers.Split(aLinefeed);
						foreach(string line in lines)
						{
							// results.Add("header line = [" + line + "]"); // 2019-08-26 not anote() in here

							// I cannot do line.Split(aColon), that splits on http: too!
							int nColon = line.IndexOf(':');
							if(nColon == -1)
								continue; // pgm err
							string[] parts = new string[2];
							parts[0] = line.Substring(0, nColon).Trim();
							parts[1] = line.Substring(nColon + 1).Trim();
							string key = parts[0];
							string wholeValue = "";
							string[] valueParts = {};
							if(parts.Length > 1) // Any value?
							{
								wholeValue = parts[1].Trim(); // the whole value right of colon
								valueParts = wholeValue.Split(aSemicolon);
								if(valueParts.Length > 1)
								{
									valueParts[0] = valueParts[0].Trim().ToLower();
									valueParts[1] = valueParts[1].Trim().ToLower();
								}
							}
							switch(key)
							{
								case "Request-Url":
									results.Add(line);
									prefixOutput += wholeValue + "\r\n";
									baseUrl = wholeValue;
									break;
								case "Status":
									// E.g.,
									//Status: OK; OK
									switch(valueParts[0])
									{
											// These are good, got data:
											//HttpStatusCode.NonAuthoritativeInformation;
											//HttpStatusCode.OK;
										case "nonauthoritativeinformation":
										case "ok":
											break;
											
											// These are all some kind of new location:
											//HttpStatusCode.Ambiguous;
											//HttpStatusCode.Found;
											//HttpStatusCode.Moved;
											//HttpStatusCode.MovedPermanently;
											//HttpStatusCode.MultipleChoices;
											//HttpStatusCode.Redirect;
											//HttpStatusCode.RedirectKeepVerb;
											//HttpStatusCode.RedirectMethod;
											//HttpStatusCode.SeeOther;
											//HttpStatusCode.TemporaryRedirect;
											//HttpStatusCode.Unused;
											//HttpStatusCode.UseProxy;

										case "ambiguous":
										case "found":
										case "moved":
										case "movedpermanently":
										case "multiplechoices":
										case "redirect":
										case "redirectkeepverb":
										case "redirectmethod":
										case "seeother":
										case "temporaryredirect":
										case "unused":
										case "useproxy":
											useLocn = true;
											break;
									}
									break;
									
									
								case "Content-Type":
									// E.g.,
									//Content-Type: text/html; charset=utf-8
									// I see Content-Type: may be empty if URL moved.
									// also Content-Type: text/plain
									// So do not complain just yet
									if(valueParts.Length > 0)
									{
										parsedHeaderContentType = true;
										if(valueParts[0] == "text/html")
										{
											// Lovely, it is text/html!
											foundTextHtml = true;
										}
										else
										{
											whatContentType = valueParts[0];
											// results.Add("whatContentType = [" + whatContentType + "]"); // 2019-08-26 not anote() in here
										}
									}
									if(valueParts.Length > 1)
									{
										// valueParts[1] being, e.g.,
										// charset=utf-8
										string parameter = valueParts[1];
										string[] csEqVal = parameter.Split(aEqual);
										if(csEqVal[0].Trim() != "charset" || csEqVal.Length != 2)
										{
											results.Add( "error: No charset [" + parameter + "]" );
											EnsureOutputFile(textPath, prefixOutput, results);
											return results.ToArray();
										}
										else
										{
											setEncodingPerString(csEqVal[1], ref useEncoding, ref rejectedEncoding, ref useBOM);
										}
									}
									break;
								case "Content-Language":
									foundLanguage = wholeValue.ToLower();
									break;
								case "Date":
									break;
								case "Last-Modified":
									break;
								case "Location":
									locnUrl = wholeValue;
									break;
								case "Server":
									break;
								case "Warning":
									break;
								case "WWW-Authenticate":
									break;
								case "Depth": // 2016-02-16 new concept: levels of indirection to this page
									int.TryParse(wholeValue, out depth);
									break;
							}
						}
					}
					
					// After doing headers,
					// Jerk the football
					// I'll have to repeat this for meta refresh later...
					if(useLocn && locnUrl != "")
					{
						try
						{
							if(locnUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
							{
								Uri r = new Uri(locnUrl);
								// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
								results.Add( "moved:" + (depth+1).ToString() + " " + r.ToString() );
								
								// Wait a minute... If I do not write some output file,
								// then every iteration of PIPE will reparse input file.
								// Aha, fixed it everywhere thus:
								EnsureOutputFile(textPath, prefixOutput, results);
								return results.ToArray(); // RelocationUrl
							}
							else
							{
								// I need to solve the FQN
								Uri b = new Uri(baseUrl);
								Uri r = new Uri(b, locnUrl);
								// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
								results.Add( "moved:" + (depth+1).ToString() + " " + r.ToString() );
								EnsureOutputFile(textPath, prefixOutput, results);
								return results.ToArray();// RelocationUrl
							}
						}
						catch (Exception)
						{
							results.Add( "error: Cannot make Uri of [" + baseUrl + "] and [" + useLocn + "]" );
							EnsureOutputFile(textPath, prefixOutput, results);
							return results.ToArray();
						}
					}

					if(parsedHeaderContentType)
					{
						if(foundTextHtml)
						{
							// good to go...
						}
						else
						{
							if(whatContentType != "")
							{
								results.Add( "error: Unsupported Content-Type [" + whatContentType + "]" );
								EnsureOutputFile(textPath, prefixOutput, results);
								
								// It occurs to me to want to save a couple types:
								// text/plain
								// application/pdf
								
								// Since I have just written the .txt file (and must do so)
								// I could also do a .text or .pdf file in the same folder.
								if(whatContentType == "text/plain")
								{
									string file2name = textPath.Substring(0, textPath.Length - 3) + "text";
									// find the first CRLFCRLF ending PIPE's header:
									for(int i = 0; i < ba.Length - 3; i++)
									{
										if(
											ba[i] == '\r' && ba[i+1] == '\n'
											&& ba[i+2] == '\r' && ba[i+3] == '\n'
										)
										{
											int nSkip = i + 3;
											byte[] ba2 = new byte[ba.Length-nSkip];
											Array.Copy(ba, nSkip, ba2, 0, ba2.Length);
											File.WriteAllBytes(file2name, ba2);
											break;
										}
									}
								}

								if(whatContentType == "application/pdf")
								{
									string file2name = textPath.Substring(0, textPath.Length - 3) + "pdf";
									// find the first CRLFCRLF ending PIPE's header:
									for(int i = 0; i < ba.Length - 3; i++)
									{
										if(
											ba[i] == '\r' && ba[i+1] == '\n'
											&& ba[i+2] == '\r' && ba[i+3] == '\n'
										)
										{
											int nSkip = i + 3;
											byte[] ba2 = new byte[ba.Length-nSkip];
											Array.Copy(ba, nSkip, ba2, 0, ba2.Length);
											File.WriteAllBytes(file2name, ba2);
											break;
										}
									}
								}
								
								return results.ToArray(); // do not parse non-HTML
							}
						}
					}
				}

				if(ba.Length - nHeaderBytes < 40) // "<html><head></head><body></body></html>"
				{
					results.Add( "error: empty html" );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				// Even if I parsed header encoding,
				// do a slight sanity check for BOM:
				//00 00 FE FF 	UTF-32, big-endian
				//FF FE 00 00 	UTF-32, little-endian
				//FE FF 	UTF-16, big-endian
				//FF FE 	UTF-16, little-endian
				//EF BB BF 	UTF-8

				// Let's semi-parse the HTML myself, to search for a content-encoding
				// lest HtmlAgilityPack complain about the wrong stream encoding and not parse:
				// ParseError: 15.72: Encoding mismatch between StreamEncoding: Windows-1252 and DeclaredEncoding: utf-8: [<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">]

				// Oh, I also want further redirection / refresh clues, and language.
				//<html lang='fr' xml:lang='fr' dir='ltr' xmlns="http://www.w3.org/1999/xhtml" >
				//<meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
				//<meta http-equiv="refresh" content="0; url=http://www.jne.co.id/home.php"/>
				//<meta http-equiv=refresh content="240; url=http://www.hurriyet.com.tr/" />

				
				if(ba[nHeaderBytes] < 0xfe // avoiding UTF-16 or 32
				   && ba[nHeaderBytes] != 0x00
				   && ba[nHeaderBytes + 1] != 0x00) // to also catch any small ascii char with high byte 0x00
				{
					for(int i= nHeaderBytes; i < ba.Length; i++)
					{
						// find <body... Yeah, well, perhaps more accurate to find </head... ?
						// Well, I must be case insensitive
						if(     (ba[i] | ' ') == 'd'
						   && (ba[i-1] | ' ') == 'a'
						   && (ba[i-2] | ' ') == 'e'
						   && (ba[i-3] | ' ') == 'h'
						   && (ba[i-4] | ' ') == '/'
						   && (ba[i-5] | ' ') == '<'
						  )
						{
							// I have to be specific to not throw up on non-ascii:
							Encoding ae = Encoding.GetEncoding(
								"us-ascii",
								new EncoderReplacementFallback("?"),
								new DecoderReplacementFallback("?"));
							string head = ae.GetString(ba, nHeaderBytes, i - nHeaderBytes);
							// this is a VERY coarse parse
							string[] saLT = head.Split(aLessThan); // puts tag names atop each part
							foreach(string s in saLT)
							{
								// Now process <html>
								
								if(s.StartsWith("html", StringComparison.OrdinalIgnoreCase))
								{
									int n = s.IndexOf("lang", StringComparison.OrdinalIgnoreCase);
									if(n != -1)
									{
										string sLang = s.Substring(n);
										string[] saEQ = sLang.Split(aEqual);
										if(saEQ.Length > 1)
										{
											string pastEQ = saEQ[1].TrimStart(anyQuoteOrSpace);
											string[] saEnd = pastEQ.Split(anyEndsCharset);
											if(saEnd.Length > 1)
											{
												foundLanguage = saEnd[0].ToLower();
											}
										}
									}
								}
								
								// Now process <meta>
								
								if(s.StartsWith("meta", StringComparison.OrdinalIgnoreCase))
								{
									// Can assume html/text by now...
									
									// How could I be missing some of these charsets?
									//error: Bad Parse: 4.73: Encoding mismatch between StreamEncoding: utf-8 and DeclaredEncoding: iso-8859-1: [<meta http-equiv="Content-Type" content="text/html; charset=iso-8859-1">]
									// didn't I lowercase early match? -- Got that now
									//error: Bad Parse: 3.71: Encoding mismatch between StreamEncoding: utf-8 and DeclaredEncoding: shift_jis: [<meta http-equiv="content-type" content="text/html;charset=Shift_JIS">]
									// I see a semicolon must end this too...
									//error: Bad Parse: 7.84: Encoding mismatch between StreamEncoding: Windows-1252 and DeclaredEncoding: utf-8: [<meta http-equiv="Content-Type" content="text/html; charset=UTF-8;charset=utf-8">]
									
									// Here are some remaining cases that I did not process correctly (leaving Utf-8):
									//<meta http-equiv="Content-Type" content="text/html; charset=windows-1252">]
									//<meta http-equiv="Content-Type" content="text/html; charset=utf-8">]
									//<meta http-equiv='Content-Type' content='text/html; charset=big5'>]
									//<meta http-equiv="Content-Type" content="text/html; charset=gb2312">]
									//<meta http-equiv="Content-Type" content="text/html; charset=UTF-8">]
									//<meta http-equiv="Content-Type" content="text/html;charset=utf-8">]
									//<meta http-equiv="CONTENT-Type" content="text/html; charset=iso-8859-1">]
									//<meta http-equiv="Content-Type" content="text/html; charset=utf-8">]
									//<meta http-equiv="Content-Type" content="text/html; charset=windows-1252">]
									//<meta http-equiv="Content-Type" content="text/html; charset=iso-8859-1">]
									//<meta http-equiv="Content-Type" content="text/html; charset=windows-1252">]
									//<meta http-equiv="Content-Type" content="text/html; charset=utf-8">]
									//<meta http-equiv="CONTENT-TYPE" content="text/html; charset=ISO-8859-1">]
									//<meta http-equiv="content-type" content="text/html; charset=ISO-8859-1">]
									//<meta http-equiv="Content-Type" content="Text/Html; Charset=Gb2312">]
									//<meta http-equiv="Content-Type" content="text/html;charset=gb2312">]
									//<meta http-equiv="Content-Type" content="text/html; charset=ISO-8859-1">]
									
									int nCS = s.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
									if(nCS != -1)
									{
										string sCS = s.Substring(nCS);
										string[] saEQ = sCS.Split(aEqual);
										if(saEQ.Length > 1)
										{
											string pastEQ = saEQ[1].TrimStart(anyQuoteOrSpace);
											// now work on past the equals (and quotes):
											string[] saEnd = pastEQ.Split(anyEndsCharset);
											if(saEnd.Length > 1)
											{
												setEncodingPerString(saEnd[0], ref useEncoding, ref rejectedEncoding, ref useBOM);
											}
										}
									}
									
									// Look for another meta item:
									int nRf = s.IndexOf("refresh", StringComparison.OrdinalIgnoreCase);
									if(nRf != -1)
									{
										string sRF = s.Substring(nRf);
										// I have to further search for url= (or url = ?)
										int nUrl = sRF.IndexOf("url", StringComparison.OrdinalIgnoreCase);
										if(nUrl != -1)
										{
											string sUrl = sRF.Substring(nUrl);
											string[] saEQ = sUrl.Split(aEqual);
											if(saEQ.Length > 1)
											{
												string pastEQ = saEQ[1].TrimStart(anyQuoteOrSpace);
												// now work on past the equals (and quotes):
												string[] saEnd = pastEQ.Split(anyEndsUrl);
												if(saEnd.Length > 1)
												{
													refreshUrl = saEnd[0];
												}
											}
										}
									}
									
									
								}
							}
						}
					}
					
				}
				
				// Follow any refresh redirect without parsing page
				// <meta http-equiv=refresh content="240; url=http://www.hurriyet.com.tr/" />
				if(refreshUrl != "")
				{
					try
					{
						if(refreshUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
						{
							Uri r = new Uri(refreshUrl);
							// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
							results.Add( "moved:" + (depth+1).ToString() + " " + r.ToString() );
							EnsureOutputFile(textPath, prefixOutput, results);
							return results.ToArray(); // RelocationUrl
						}
						else
						{
							// I need to solve the FQN
							Uri b = new Uri(baseUrl);
							Uri r = new Uri(b, refreshUrl);
							// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
							results.Add( "moved:" + (depth+1).ToString() + " " + r.ToString() );
							EnsureOutputFile(textPath, prefixOutput, results);
							return results.ToArray(); // RelocationUrl
						}
					}
					catch (Exception)
					{
						results.Add( "error: Cannot make Uri of [" + baseUrl + "] and [" + useLocn + "]" );
						EnsureOutputFile(textPath, prefixOutput, results);
						return results.ToArray();
					}
				}


				if(rejectedEncoding != "")
				{
					results.Add( "error: Unsupported encoding [" + rejectedEncoding + "]" );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				if(foundLanguage != "")
				{
					if(foundLanguage.StartsWith("en") == false)
					{
						results.Add( "error: Unsupported language [" + foundLanguage + "]" );
						EnsureOutputFile(textPath, prefixOutput, results);
						return results.ToArray();
					}
				}

				
				// If all that did not stop us,
				// try to process the rest of file using HtmlAgilityPack
				
				MemoryStream ms = null;
				HtmlDocument doc = null;
				int step = 0;
				try
				{
					step = 1;
					
					doc = new HtmlDocument();
					// Was:
					// MemoryStream ms = new MemoryStream(ba, nHeaderBytes, ba.Length - nHeaderBytes);
					
					// I have a problem how HtmlAgilityPack counts lines to report parse errors:
					//
					//E:\RESULTS\1main\PRO3\Serial Log
					//1/7/2016 2:00:55 PM error: Bad Parse: 11.6708: End tag </> is not required: [<]
					//
					// Let's do a FIXNL on the remaining text before I give it to HtmlAgilityPack.
					// Easiest way is to make a string, use the same RegEx as later, convert back.
					// Ahh but damn! That has to cross the Encoding boundary again! Make a filter.
					// Certain parts about my days in Paradise make mew curse: OO!, Unicode!, etc.

					// While you are at this, solve another problem:
					// The persistent logs and serial logs contain bad HTML
					// because of either >>>... or >>>... in lines like this:
					// >>>>>> Hardware watchdog started <<<<<
					// So rid runs of consecutive < or > bytes.
					// HTML can never use 2 consecutive < or > (but surely >< together is okay).
					step = 2;
					int nMax = (ba.Length - nHeaderBytes) * 2;
					byte[] ba2 = new byte[nMax];
					int j = 0;
					int nGT = 0;
					int nLT = 0;
					for(int i = nHeaderBytes; i < ba.Length; i++)
					{
						switch(ba[i])
						{
							case (byte)'\r':
								ba2[j++] = (byte)'\r';
								if(i == ba.Length - 1 || ba[i+1] != (byte)'\n')
									ba2[j++] = (byte)'\n';
								break;
							case (byte)'\n':
								if(i == 0 || ba[i-1] != (byte)'\r')
									ba2[j++] = (byte)'\r';
								ba2[j++] = (byte)'\n';
								break;
							case (byte) '>':
								nGT++;
								if(nGT > 1)
								{
									ba2[j-1] = (byte)'='; // substitution of prior
									ba2[j++] = (byte)'='; // substitution of this
								}
								else
								{
									ba2[j++] = ba[i];
								}
								break;
							case (byte) '<':
								nLT++;
								if(nLT > 1)
								{
									ba2[j-1] = (byte)'='; // substitution of prior
									ba2[j++] = (byte)'='; // substitution of this
								}
								else
								{
									ba2[j++] = ba[i];
								}
								break;
							default:
								nGT = 0;
								nLT = 0;
								ba2[j++] = ba[i];
								break;
						}
					}
					
					step = 3;
					ms = new MemoryStream(ba2, 0, j);
				}
				catch (Exception ex)
				{
					results.Add( "error: FixNl Step " + step + " threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}

				
				// Maybe during Load?
				try
				{
					doc.Load(ms, useEncoding, useBOM); // utf-8 is default; true = look for encoding from BOM
				}
				catch (Exception ex)
				{
					results.Add( "error: doc.Load threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				foreach(HtmlParseError he in doc.ParseErrors)
				{
					HtmlParseErrorCode ec = he.Code;
					int ln = he.Line;
					int lp = he.LinePosition;
					string re = badCharsToHex(he.Reason);
					if(re.Length > 80)
						re = re.Substring(0, 80);
					string st = badCharsToHex(he.SourceText);
					if(st.Length > 80)
						st = st.Substring(0, 80);
					// stop on first:
					string mess = ln + "." + lp + ": " + re + ": [" + st + "]";
					
					// I see so many of these, I may just omit the tag start/end mismatches:
					if(mess.Contains("tag <") &&
					   (mess.Contains("not found")
					    || mess.Contains("not required")
					   )
					  )
					{
						// ignore
						//Start tag <li> was not found: [</li>]
						//End tag </option> is not required: [</option>]
						//End tag </html> was not found: []
					}
					else
					{
						results.Add( "error: Bad Parse: " + mess );
					}
					// Hmmm. Is a Bad Parse error fatal or not...? No!
					// Errors are NOT fatal --- return results.ToArray();
				}

				//XPath cheatsheat
				// Expression 	Description
				// nodename 	Selects all nodes with the name "nodename"
				// / 	Selects from the root node
				// // 	Selects nodes in the document from the current node that match the selection no matter where they are
				// . 	Selects the current node
				// .. 	Selects the parent of the current node
				// @ 	Selects attributes
				
				// xpath negation:
				// not() is a function in xpath (as opposed to an operator), so
				// for example: //a[not(contains(@id, 'xx'))]
				
				// Xpath node selection - how to select 2 different elements - htmlagilitypack
				// for " or " you need to use " | "
				// "//div[@class='breadcrumbs']//li[@class='product'] | //div[@class='breadcrumbs']//a";
				
				
				// Prefix a ridding of head, script, style and comment() nodes:
				// Somehow this routine needs to check if collection is null...
				//error: Rid nodes threw Value cannot be null.
				//Parameter name: collection
				
				try
				{
					// Self or Descendents "//"
					// Match any "node()" -- which includes comment(), not like "*",
					// satisfying various tests of "self:: ..."
					// SelectNodes("//node()[self::head or self::script or self::style or self::comment()]");
					// What if I removed a node that contains another on my list?
					// I'd best treat each type separately...
					// rid HTML comments
					// Ridding comments also rids the <...Doctype...> atop file.
					
					// Perhaps ridding comments is introducing parse errors,
					// due to HTML conditional comments, like:
					//<!--[if gt IE 6]><!-->
					//This code displays on non-IE browsers and on IE 7 or higher.
					//<!--<![endif]-->
					
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::comment()]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string ridName = "!comment"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}
					
					
					// rid HTML script
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::script]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string ridName = "!script"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}

					// rid HTML style
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::style]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string ridName = "!style"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}

					// rid HTML svg
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::svg]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string ridName = "!style"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}
					
					// rid HTML del
					// The <del> tag defines text that has been deleted from a document.
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::del]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string ridName = "!style"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}
					
					// Can I validly remove HEAD?
					// It should contain no text.
					// Harvest, but capture any TITLE
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()[self::head]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								// However, while I have HEAD in my HAND,
								// grab the TITLE to adorn text results.
								{
									HtmlNode titl = node.SelectSingleNode("//title");
									if(titl != null)
									{
										// I'll need to purify that text a bit:
										string titl0 = titl.InnerText;
										string titl1 = HttpUtility.HtmlDecode(titl0); // Hmmm. It missed a lot of &#82xx;
										string titl2 = reTab.Replace(titl1, " ");
										string titlPre3 = reHexEntities.Replace(titl2, new MatchEvaluator(HexToUnicode));
										string titl3 = reNonCrLfUsAscii.Replace(titlPre3, new MatchEvaluator(Translator)); // A non-interesting char
										string titl4 = reCRLFs.Replace(titl3, " "); // no CR nor LF either
										string titl5 = reFatSpaces.Replace(titl4, " "); // max of 1 space together
										if(titl5.Length > 80)
											titl5 = titl5.Substring(0, 80) + "...";
										
										prefixOutput += "TITLE: " + titl5 + "\r\n";
									}
								}
								if(logHtmlStatistics)
								{
									string ridName = "!head"; // Did node.Name give me garbage HERE, or...?
									if(htmlStatisticsDict.ContainsKey(ridName) == false)
										htmlStatisticsDict.Add(ridName, 0);
									htmlStatisticsDict[ridName] ++;
								}
								node.Remove();
							}
						}
					}
				}
				catch (Exception ex)
				{
					results.Add( "error: Rid nodes threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}

				// before the parse that re-organizes the document into text,
				// let's parse to return well-known search engine hit anchors.

				// ==========================================================
				// ==========================================================
				// 						SERP Parse
				// ==========================================================
				// ==========================================================
				
				// 2016-02-11 Realized duckduckgo needs /html/ for non-Javascript version
				
				//static string[] queryHeads = {
				//	"https://www.google.com/search?q=",
				//	"https://www.bing.com/search?q=",
				//	"https://search.yahoo.com/search?p=",
				//	"https://duckduckgo.com/html/?q=",
				//	"https://twitter.com/search?q=",
				//};

				// Google and Bing parse started identically,
				// but duplicated code expecting differences.
				
				// Scrape Google.com

				try
				{
					if(
						baseUrl.StartsWith("https://www.google.com/search?q=")
					)
					{
						// Same algorithm: find last anchor tag prior to cite tag.
						
						// Both Google and Bing have an HtmlElement "cite" next
						// after each HtmlElement "a" containing good hit URLs.
						
						// This selects all nodes that contain an "href" attribute:
						// HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//@href");

						// ==========================================================
						// ==========================================================
						// 2017-04-15 Google SRP Company Phone Number parse
						// ==========================================================
						// ==========================================================

						if(logCompanyPhones)
						{
							// So select all nodes 'td' which contain attribute 'id' with value 'rhs_block';
							// And within those 'td' nodes, any depth, select all 'div' nodes which contain attribute 'class' with value 'rhs_block'
							// and rather than nested code blocks, select all text, any depth, therein...
							// This got me a nice collection: "//td[@id='rhs_block']//div[@class='_uXc hp-xpdbox']//text()"
							// Now I want a more surgical strike:
							HtmlNode hn1 = doc.DocumentNode.SelectSingleNode("//td[@id='rhs_block']//div[@class='_uXc hp-xpdbox']");
							if(hn1 != null)
							{
								// I don't want to fiddle the DOM as below before the real parse....
								try
								{
									StringBuilder sb = new StringBuilder();
									//foreach(var hn4 in hnc2)
									//{
									//	sb.Append(hn4.InnerText + " ");
									//}
									// File.AppendAllText(logCompanyPhonesFilename, "\r\n------\r\n" + sb.ToString() + "\r\n======\r\n");

									// I see that 2 of the nodes I want follow labels in text:
									// Shall I struggle with an exotic node+1 XPath select...?
									// No struggle,
									// StackOverflow has the code pony:
									// tr[td[@class='name'] ='Brand']/td[@class='desc']
									
									//Another case says: You need to change this to //*[text()[contains(.,'ABC')]]
									//1. * is a selector that matches any element (i.e. tag) -- it returns a node-set.
									//2. The outer [] are a conditional that operates on each individual node in that node set -- here it operates on each element in the document.
									//3. text() is a selector that matches all of the text nodes that are children of the context node -- it returns a node set.
									//4. The inner [] are a conditional that operates on each node in that node set -- here each individual text node. Each individual text node is the starting point for any path in the brackets, and can also be referred to explicitly as . within the brackets. It matches if any of the individual nodes it operates on match the conditions inside the brackets.
									//5. contains is a function that operates on a string. Here it is passed an individual text node (.). Since it is passed the second text node in the <Comment> tag individually, it will see the 'ABC' string and be able to match it.

									//Yet another case: I often use "contains", but there are more. Here are some examples:
									//- multiple condition: //div[@class='bubble-title' and contains(text(), 'Cover')]
									//- partial match: //span[contains(text(), 'Assign Rate')]
									//- starts-with: //input[starts-with(@id,'reportcombo')
									//- value has spaces: //div[./div/div[normalize-space(.)='More Actions...']]
									//- sibling: //td[.='LoadType']/following-sibling::td[1]/select"
									

									//<div class="_gF">
									//<span class="_gS">Address:&nbsp;</span>
									//<span class="_tA">1 Old Country Rd, Carle Place, NY 11514</span>
									//</div>
									
									//<div class="_gF">
									//<span class="_gS">Phone: </span>
									//<span class="_tA">(516) 739-3083</span>
									//</div>
									
									// <div class="_B5d">1-800-Flowers Carle Place Florist</div>
									
									HtmlNode hnName = hn1.SelectSingleNode("//div[@class='_B5d']");
									HtmlNode hnAddr = hn1.SelectSingleNode("//div[span[@class='_gS' and contains(text(), 'Address:')]]/span[@class='_tA']");
									HtmlNode hnCall = hn1.SelectSingleNode("//div[span[@class='_gS' and contains(text(), 'Phone:')]]/span[@class='_tA']");
									
									if(hnCall != null)
										sb.Append(hnCall.InnerText.Replace(',', ' '));
									sb.Append(',');
									if(hnName != null)
										sb.Append(hnName.InnerText.Replace(',', ' '));
									sb.Append(',');
									if(hnAddr != null)
										sb.Append(hnAddr.InnerText.Replace(',', ' '));
									sb.Append(',');
									File.AppendAllText(logCompanyPhonesFilename, sb.ToString() + "\r\n");
								}
								catch (Exception ex)
								{
									results.Add( "error: parse phones threw " + ex.Message );
									EnsureOutputFile(textPath, prefixOutput, results);
									return results.ToArray();
								}
								
							}
						}
						
						// Rather, select all nodes that are "a" or nodes that are "cite",
						// then pick every anchor which came just-prior-to a next cite.
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//a|//cite");
						if(hnc != null)
						{
							//int ordinal = 0;
							HtmlNode hn = null;
							foreach(HtmlNode hnAll in hnc)
							{
								if(hnAll.Name == "cite" && hn != null)
								{
									// Some of these are absolute, others relative:
									string href = hn.GetAttributeValue("href", "none").ToString();
									if(href != "none")
									{
										// Apparently, all the Google absolute URLs are rejectable AD urls:
										
										// All the Google REAL hits come into these relative URL:
										// I can further parse these to avoid redirection:
										// /url?q=http://www.investopedia.com/terms/r/risk.asp&amp;sa=U&amp;ved=0ahUKEwit2Mmygp7KAhWEeT4KHfQmAAkQFghaMA0&amp;usg=AFQjCNEbHz-qefLoJv7kCwA5KyCMfe2ImA
										if(
											href.StartsWith("/url?q=http://") // allow this; exclude google hits to absolute URLs
											||
											href.StartsWith("/url?q=https://") // allow this; exclude google hits to absolute URLs
										)
										{

											int n = href.IndexOf("&amp;sa=");
											if(n != -1)
											{
												string part = href.Substring(0, n);
												// /url?q=http://
												// 01234567
												href = part.Substring(7); // This 7 is /url?q=, not over http or https parts
												if(ILikeThisUrl(href))
												{
													// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
													results.Add("serpg:" + (depth+1).ToString() + " " + href ); // Search Engine Result Page Hit
												}
											}
										}
									}
								}
								hn = hnAll;
							}
						}
					}
					else
					{
						// 2017-04-15 temporary:
						// skip ALL OTHER PAGES while I develop the Google Company Phone XPaths
						//results.Add( "error: skipping all non-Google QRP" );// 2017-04-15 temporary:
						//EnsureOutputFile(textPath, prefixOutput, results);// 2017-04-15 temporary:
						//return results.ToArray();// 2017-04-15 temporary:
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse G hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				// Scrape Bing.com

				try
				{
					if(
						baseUrl.StartsWith("https://www.bing.com/search?q=")
					)
					{
						// Same algorithm: find last anchor tag prior to cite tag.
						
						// Both Google and Bing have an HtmlElement "cite" next
						// after each HtmlElement "a" containing good hit URLs.
						
						// This selects all nodes that contain an "href" attribute:
						// HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//@href");
						
						// Rather, select all nodes that are "a" or nodes that are "cite",
						// then pick every anchor which came just-prior-to a next cite.
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//a|//cite");
						if(hnc != null)
						{
							//int ordinal = 0;
							HtmlNode hn = null;
							foreach(HtmlNode hnAll in hnc)
							{
								if(hnAll.Name == "cite" && hn != null)
								{
									// Some of these are absolute, others relative:
									string href = hn.GetAttributeValue("href", "none").ToString();
									if(href != "none")
									{
										if(
											href.StartsWith("http://") // allow this; exclude relative urls
											||
											href.StartsWith("https://") // I later added https for generality
										)
										{
											// absolute - bing hits, but eliminate hits containing r.msn.com:
											// http://2527555.r.msn.com/?ld=d3q2o6C1CdvQy2HdB2eXrAQjVUCUzlnJs3a6TQBUJYNvUBOufrRCEZAJTaYb7XMe72XRf1X44Nhk1VgNT05cCsFcxsao3ay3xLcVhgWyc3uqJyBaBulMVDB_Ao8wwPIEmRVHkja-GGd4PWxXWealPTRh7hNY9aYfkO8a3_lT_8QFjSGHjJ&amp;u=http%3a%2f%2f10.xg4ken.com%2fmedia%2fredir.php%3fprof%3d362%26camp%3d14642%26affcode%3dcr55883%26k_inner_url_encoded%3d1%26cid%3d5892005076%7c732266%7crisk%26mType%3de%26queryStr%3drisk%26url%5b%5d%3dhttp%253A%252F%252Fm.xp1.ru4.com%252Fsclick%253Fredirect%253Dhttp%3a%252F%252Fwww.pogo.com%252Fgames%252Frisk%253F_o%253D42032312%2526_t%253D55704756%2526ssv_knsh_tid%253D_kenshoo_clickid_%2526ssv_knsh_agid%253D3389%2526ssv_knsh_cid%253D14642%2526ssv_knsh_crid%253D5892005076%2526ssv_knsh_affid%253D55883%2526ssv_knsh_sen%253DBING%2526ssv_knsh_nwk%253Dsearch%2526utm_campaign%253Drisk-search-na-pbm-b-games-pogo-e%2526utm_medium%253Dcpc%2526utm_source%253Dbing%2526utm_term%253Drisk%2526sourceid%253Drisk-search-na-pbm-b-games-pogo-e
											
											// Also exclude bing hits containing r.bat.bing.com, 22 out of 24 such pages were empty html
											if(
												href.Contains("r.msn.com") // exclude such bing hits
												||
												href.Contains("r.bat.bing.com") // exclude such bing hits - often empty html
											)
											{
												// ignore
											}
											else
											{
												if(ILikeThisUrl(href))
												{
													// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
													results.Add("serpb:" + (depth+1).ToString() + " " + href ); // Search Engine Result Page Hit
												}
											}
										}
									}
								}
								hn = hnAll;
							}
						}
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse B hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				// 2020-11-11 adding new SEARX search engine scraper here:
				
				// Scrape searX.run

				try
				{
					if(
						baseUrl.StartsWith("https://searx.run/search?q=") // 2020-11-11 new search engine; 2022-07-04 added 'search' in path
					)
					{
						// 2022-07-04 Yet Another adaptation required. This old idea no longer works:

						/*
						// Curiously, I think to pull the clear-text url on display on the serp page:
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//div[@class='result result-default']//div[@class='external-link']");
						if(hnc != null)
						{
							foreach(HtmlNode hn in hnc)
							{
								string href = hn.InnerText;

								if(
									href.StartsWith("http") // sanity test
									&&
									! href.Contains("[...]") // must omit defective long URLs with ellipses.
								)
								{
									if(ILikeThisUrl(href))
									{
										// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
										results.Add("serps:" + (depth+1).ToString() + " " + href ); // Search Engine Result Page Hit
									}
								}
							}
						}
						*/

						// 2022-07-04 Adapting to latest searX SERP HTML:
						// This looks totally easy: <h4 class="result_header" id="result-28"><a href="https://www.ibm.com/cloud/blog/openshift-vs-kubernetes" ...
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//h4[@class='result_header']/a");
						if (hnc != null)
						{
							foreach (HtmlNode hn in hnc)
							{
								string href = hn.GetAttributeValue("href", "none").ToString();

								if (
									href.StartsWith("http") // sanity test
								)
								{
									if (ILikeThisUrl(href))
									{
										// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
										results.Add("serps:" + (depth + 1).ToString() + " " + href); // Search Engine Result Page Hit
									}
								}
							}
						}


					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse S hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				// Scrape DuckDuckGo.com

				try
				{
					if(
						baseUrl.StartsWith("https://html.duckduckgo.com/html/?q=") // 2020-11-09 adding html. prefix
					)
					{
						// 2020-11-09 This is the new format of DUCKDUCKGO hit urls:
						// <a class="result__url" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.facebook.com%2FDonaldTrump">www.facebook.com/DonaldTrump</a>
						
						// So select all nodes 'a' which contain attribute 'class' with value 'result__url':
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//a[@class='result__url']");
						if(hnc != null)
						{
							foreach(HtmlNode hn in hnc)
							{
								string href1 = hn.GetAttributeValue("href", "none").ToString();
								// if(href1 != "none")...

								// For starters, just fetch that relative-and-via-DDG format...
								// No, my later logic will report iSkip:2 as in this result url:
								// iSkip:2 //duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.amazon.com%2FGot%2DVideo%2DOnline%2DPromote%2DBusiness%2Fdp%2F0615486495
								// 2020-11-09 Well then, let's parse out the target URL goodies:
								if(
									href1.StartsWith("//duckduckgo.com/l/?uddg=")
								)
								{
									string href2a = href1.Substring("//duckduckgo.com/l/?uddg=".Length);
									// fix the two obvious '%' encodes and any others.
									// Empirically, HtmlDecode could not handle the :// being decoded,
									// leaving the url like "https%3A%2F%2Fwww." so I will preconvert:
									// Yes, that worked okay... No, not yet...
									string href2b = href2a.Replace("%3A%2F%2F", "://");

									// 2022-07-04 This car has been out of alignment a long time now:
									//
									// As I prepare to open-source this, I notice that
									// duckduckgo hit URLs are all postfixed with some
									// suffix spoiling url, starting with &amp;rut=...
									//
									// Reading thus directly from the raw HTML page:
									// <a rel="nofollow" class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.trio.dev%2Fblog%2Fwhat%2Dis%2Dgolang%2Dused%2Dfor&amp;rut=5e6fa8c66c5184677e246cac5ce1a665d86c3ff06d71a694d8575f4753238ffd">What Is Golang Used For? 7 Examples of Go Applications</a>

									string href2c = reTrimDuckDuckGoHitUrls.Replace(href2b, ""); // added 2022-07-04

									string href3 = HttpUtility.UrlDecode(href2c);
									if(ILikeThisUrl(href3))
									{
										// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
										results.Add("serpd:" + (depth+1).ToString() + " " + href3 ); // Search Engine Result Page Hit
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse D hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				
				
				// Scrape Yahoo.com

				try
				{
					if(
						baseUrl.StartsWith("https://search.yahoo.com/search?p=")
					)
					{
						// The following H3 tag surrounds the Hit Links.
						// However, it also surrounds some ad links to rid,
						// which ad hits contain r.search.yahoo.com

						//<h3 class="title">
						//<a class=" td-n" href="http://r.search.yahoo.com/
						
						// So select all nodes 'h3' which contain attribute 'class' with value 'title';
						// And within those 'h3' nodes, select all 'a' nodes:
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//h3[@class='title']/a");
						if(hnc != null)
						{
							foreach(HtmlNode hn in hnc)
							{
								string href = hn.GetAttributeValue("href", "none").ToString();
								//if(href != "none")...
								if(href.Contains("r.search.yahoo.com") == false) // exclude such Yahoo hit URLs
								{
									if(ILikeThisUrl(href))
									{
										// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
										results.Add("serpy:" + (depth+1).ToString() + " " + href ); // Search Engine Result Page Hit
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse Y hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				
				// Scrape Twitter.com

				try
				{
					if(
						baseUrl.StartsWith("https://twitter.com/search?q=")
					)
					{
						// The following class name marks the Hit Links.
						// <a class="twitter-timeline-link"
						// and they ALL redirect through t.co;
						// although one might avoid that, and parse from
						// data-expanded-url
						// instead of href...?

						// <a href="https://t.co/CRRvX1S9GA" rel="nofollow" dir="ltr"
						// data-expanded-url="http://twishort.com/vfOjc"
						// class="twitter-timeline-link" target="_blank"
						// title="http://twishort.com/vfOjc" >
						// <span class="tco-ellipsis"></span>
						// <span class="invisible">http://</span>
						// <span class="js-display-url">twishort.com/vfOjc</span>
						// <span class="invisible"></span>
						// <span class="tco-ellipsis">
						// <span class="invisible">&nbsp;</span>
						// </span></a>
						
						
						// So select all nodes 'a' which contain attribute 'class' with value 'large':
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//a[@class='twitter-timeline-link']");
						if(hnc != null)
						{
							foreach(HtmlNode hn in hnc)
							{
								string href = hn.GetAttributeValue("data-expanded-url", "none").ToString();

								if(href == "none") // In case of no data-expanded-url attribute, use href:
									href = hn.GetAttributeValue("href", "none").ToString();
								
								if(href != "none") // Neither attr?
								{
									if(href.Contains("//fb.me/") == false) // exclude links into Facebook
									{
										if(ILikeThisUrl(href))
										{
											// 2016-02-16: Add 1 to the current depth, prefix number+space to URL:
											results.Add("serpt:" + (depth+1).ToString() + " " + href ); // Search Engine Result Page Hit
										}
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse T hits threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				

				// ==========================================================
				// ==========================================================
				// 						LINK Parse
				// ==========================================================
				// ==========================================================

				// Before next Text Parse spoils the document,
				// Extract all Anchors to report back to pipe.


				try
				{
					// select all 'a' nodes:
					HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//a");
					if(hnc != null)
					{
						Uri b = new Uri(baseUrl);
						
						foreach(HtmlNode hn in hnc)
						{
							Uri FqUrl = null;
							string href = hn.GetAttributeValue("href", "none").ToString();
							if(href != "none"
							   &&
							   Uri.TryCreate(b, href, out FqUrl)
							   &&
							   FqUrl.IsAbsoluteUri
							  )
							{
								// Day 0.1 of parsing looks like this:
								// I must collect anchor text,
								// join (base + Relative)
								// strip # fragments
								// assign some priorities,
								// perhaps I might also list all higher paths?
								//999 "title..." /
								//999 "title..." #
								//999 "title..." /download/
								//999 "title..." /docs/
								//999 "title..." http://www.dotnetfoundation.org
								//999 "title..." http://www.microsoft.com
								//999 "title..." /docs/about-mono/languages/ecma/
								//999 "title..." /atom.xml
								//999 "title..." https://github.com/mono/website
								//999 "title..." https://github.com/mono/website#contributing-to-the-website
								//999 "title..." #getting-started
								
								// Strip off any #fragment
								string pageUrl = FqUrl.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.SafeUnescaped);
								// int priority = 999;
								{
									// Getting any alternate title is easy.
									string title1 = hn.GetAttributeValue("title", "none").ToString();
									if(title1 != "none")
									{
										title1 = straightlyCleanAnchorText(title1);
										string dblQuotedAnchorText = "\"[" + title1 + "]\"";
										// Nice work, but just give me a block of URLs:
										// results.Add("link:" + priority.ToString("d3") + " " + dblQuotedAnchorText + " " + pageUrl );
										string aLink = "link:" + pageUrl;
										if(results.Contains(aLink) == false)
										{
											results.Add(aLink);
											// Actually, I do need the anchor text to make good next-generation choices. Add both:
											results.Add("link:" + " " + dblQuotedAnchorText + " " + pageUrl );
										}
									}
								}

								// I need to gather the anchor text.
								// Web buzz says it is this easy:
								{
									StringBuilder sb = new StringBuilder();
									// but wait, is this where my null reference arose?
									//foreach (HtmlNode node in hn.SelectNodes(".//text()"))
									HtmlNodeCollection hnc2 = hn.SelectNodes(".//text()");
									if(hnc2 != null)
									{
										foreach (HtmlNode node in hnc2)
										{
											sb.Append(node.InnerText + " ");
										}
										string title2 = straightlyCleanAnchorText(sb.ToString());
										string dblQuotedAnchorText = "\"" + title2 + "\"";
										// Nice work, but just give me a block of URLs:
										// results.Add("link:" + priority.ToString("d3") + " " + dblQuotedAnchorText + " " + pageUrl );
										string aLink = "link:" + pageUrl;
										if(results.Contains(aLink) == false)
											results.Add(aLink);
									}
								}
							}
						}
					}
				}
				catch(Exception ex)
				{
					results.Add( "error: parse anchors threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}



				// Areas for more research:
				// You can also use the <object> tag to embed another webpage into your HTML document!
				// - object, wherein data = The URL of the object to embed.
				// - iframe, wherein src = The URL of the page to embed.
				// - framesets

				
				
				// ==========================================================
				// ==========================================================
				// 						TEXT Riddance
				// ==========================================================
				// ==========================================================

				// Before the text parse, let's throw away NAV, FORM, etc...
				
				
				try
				{
					// rid various HTML tags that are not main text:
					
					// Optional Xpath function not() gives negation -- but nothing was left...
					// HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//*[not(self::nav or self::form or self::header or self::footer or self::aside)]");
					// I am going to split this up again, to name nodes myself:
					// Well, later for that idea...
					// HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//*[self::nav or self::form or self::header or self::footer or self::aside]");

					//if(false)... // I notice I get no MENU links without NAV or HEADER etc!
					{
						//HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//*[self::nav or self::form or self::header or self::footer or self::aside]");
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//*[self::form or self::aside]");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								if(logHtmlStatistics)
								{
									string tagName = node.Name; // I have been seeing junk when using node.Name
									if(string.IsNullOrEmpty(tagName) == false
									   && reNonWord.IsMatch(tagName) == false)
									{
										string ridName = "!" + tagName; // All have node.Name, but only tags have node.tagName;
										if(htmlStatisticsDict.ContainsKey(ridName) == false)
											htmlStatisticsDict.Add(ridName, 0);
										htmlStatisticsDict[ridName] ++;
									}
								}
								node.Remove();
							}
						}
					}
				}
				catch (Exception ex)
				{
					results.Add( "error: Rid text threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}


				// ==========================================================
				// ==========================================================
				// 						TEXT Parse
				// ==========================================================
				// ==========================================================
				

				// Now extract and process the TEXT of HTML documents:


				// Prior to other HTML TAG processing, scour all of the tags'
				// text holdings, to replace any CR or LF chars with spaces.
				// except, not within <PRE> tags, as found by "//pre" like:
				// SelectNodes("//node()[not( self::pre )]");
				// But wait, that would also select tags with PRE ancestors.
				// I should instead WALK the DOM, skipping over PRE tags.

				try
				{
					// I can use the queue technique from below to walk the DOM,
					// but instead of replacing parents, I will refine any text.
					// Select the top layer of children, including any top text:
					HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("./*|./text()");
					if(hnc != null)
					{
						var nodes = new Queue<HtmlNode>(hnc);
						// Process all the children...
						// Queuing up their children, except nodes == PRE,
						// And removing CR LFs from any nodes == text().
						while (nodes.Count > 0)
						{
							var node = nodes.Dequeue();
							var parentnode = node.ParentNode;

							if (node.Name == "#text")
							{
								// Note, you want .Text here, not .innerText which is read only/generated.
								// No, that's an XML idea. I will have to REPLACE the node with a new one.
								if(reCRLFs.IsMatch(node.InnerText))
								{
									node.ParentNode.ReplaceChild(HtmlTextNode.CreateNode(reCRLFs.Replace(node.InnerText, " ")), node);
								}
							}
							else if (node.Name == "pre")
							{
								// skip PRE tags, skip all their children
							}
							else
							{
								// tail recursion
								var childnodes = node.SelectNodes("./*|./text()");
								if (childnodes != null)
								{
									foreach (var child in childnodes)
									{
										nodes.Enqueue(child);
									}
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					results.Add( "error: scouring newlines threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}
				
				// First, process just TAGs that are worthy of inserting newlines:
				// And how will I do that? Perhaps insert a child text() node?

				// I basically see 2 kinds:
				// Those worthy of delimiting a paragraph: prefix and suffix with 2 CRLF each.
				// Those worthy of delimiting a line: prefix with 1 CRLF each.
				
				// So for me, multiple tag selection looks like this:
				// "//tag | //tag ..."
				
				try
				{
					// These are all BLOCK delimiters, worthy of double CRLF before and behind:
					// Tackle <div> ... </div> nodes:
					// Tackle <p> ... </p> nodes:
					// Tackle <table> ... </table> nodes:
					// Tackle <tr> ... </tr> nodes:
					// Tackle HR and all H1 - H6
					// address, blockquote, canvas, caption
					// dl, ul, ol
					// PRE -- I could do even more with this!
					// section
					// legend
					// iframe
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//div | //p | //table | //tr | //hr | //h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //address | //blockquote | //canvas | //caption | //dl | //ul | //ol | //pre | //section | // legend | //iframe");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								node.PrependChild(doc.CreateTextNode("\r\n\r\n"));
								node.AppendChild(doc.CreateTextNode("\r\n\r\n"));
							}
						}
					}
					// Tackle <br> tags
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//br");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								node.PrependChild(doc.CreateTextNode("\r\n"));
							}
						}
					}
					// Tackle <li> tags
					// Prefix with "+ " as a bullet.
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//li");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								node.PrependChild(doc.CreateTextNode("\r\n+ "));
							}
						}
					}
					// Tackle TABLE DATA cell tags differently
					// <td>
					// Also do <th> -- is like <td> but in head.
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//td | //th");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								// Create |\ col1 /||\ col2 /||\ col3 /| appearance:
								// Nah... Use squiggly braces...
								//node.PrependChild(doc.CreateTextNode(" |\\ "));
								//node.AppendChild(doc.CreateTextNode(" /| "));
								// So:
								node.PrependChild(doc.CreateTextNode("{"));
								node.AppendChild(doc.CreateTextNode("}"));
							}
						}
					}
					// Another two-part variation to tailor definition lists.
					// dt
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//dt");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								node.PrependChild(doc.CreateTextNode("\r\n"));
								node.AppendChild(doc.CreateTextNode(" :")); // likely joins with next colon space
							}
						}
					}
					// Another two-part variation to tailor definition lists.
					// dd
					{
						HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//dd");
						if(hnc != null)
						{
							foreach(HtmlNode node in hnc)
							{
								node.PrependChild(doc.CreateTextNode(": ")); // likely joins with prior space colon
								node.AppendChild(doc.CreateTextNode("\r\n"));
							}
						}
					}
					
					
				}
				catch (Exception ex)
				{
					results.Add( "error: inserting newlines threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}

				
				try
				{
					// This restoration of the whole document was found on the web:
					// Come to think of it, why didn't I just read in an HTML file?
					// Bite'cha! Everything nullable eventually becomes null!
					HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("./*|./text()");
					if(hnc != null)
					{
						var nodes = new Queue<HtmlNode>(hnc);
						while (nodes.Count > 0)
						{
							var node = nodes.Dequeue();
							var parentnode = node.ParentNode;
							if(logHtmlStatistics)
							{
								string tagName = node.Name; // I have been seeing junk when using node.Name
								if(string.IsNullOrEmpty(tagName) == false
								   && reNonWord.IsMatch(tagName) == false)
								{
									if(htmlStatisticsDict.ContainsKey(tagName) == false)
										htmlStatisticsDict.Add(node.Name, 0);
									htmlStatisticsDict[node.Name] ++;
								}
							}

							if (node.Name != "#text")
							{
								var childnodes = node.SelectNodes("./*|./text()");
								if (childnodes != null)
								{
									foreach (var child in childnodes)
									{
										nodes.Enqueue(child);
										parentnode.InsertBefore(child, node);
									}
									parentnode.RemoveChild(node);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					results.Add( "error: selecting text threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}

				
				try
				{
					HtmlNodeCollection hnc = doc.DocumentNode.SelectNodes("//node()");
					if(hnc != null)
					{
						StringBuilder sb = new StringBuilder();
						foreach (HtmlNode node in hnc)
						{
							sb.Append(node.InnerText);
						}
						// HtmlDecode did not handle some of these Windows puncts:
						//tips &#8211; best
						//LOS ANGELES &#8212; For big,
						//pop &#8230; and
						//That&#8217;s
						//itself: &#8220;You&#8217;re pregnant.&#8221;
						// So finally, I must rid remaining like &#x0027; or &#39;
						
						string body0 = sb.ToString();
						string body1 = HttpUtility.HtmlDecode(body0); // Hmmm. It missed a lot of &#82xx;
						string body2 = reTab.Replace(body1, " ");
						string bodyPre3 = reHexEntities.Replace(body2, new MatchEvaluator(HexToUnicode));
						string body3 = reNonCrLfUsAscii.Replace(bodyPre3, new MatchEvaluator(Translator)); // A non-interesting char
						string body4 = reCRLFs.Replace(body3, "\n"); // Must omit \r for next Regexs r
						string body5 = reWhiteLines.Replace(body4, ""); // empty any Whitspace lines
						string body6 = reManyLines.Replace(body5, "\n\n"); // max of 2 newlines together
						string body7 = reFatSpaces.Replace(body6, " "); // max of 1 space together
						string body8 = reFirstSpace.Replace(body7, "\n"); // rid first space on line
						string body9 = reCRLFs.Replace(body8, "\r\n"); // Regain Windows CR+LF
						string bodya = body9.Trim(caCrLfSp); // remedy initial newline count
						// do not forget to update the last name in this path, here:
						File.WriteAllText(textPath, prefixOutput + "\r\n" + bodya + "\r\n" );
					}
					else
					{
						results.Add( "error: empty html" );
						EnsureOutputFile(textPath, prefixOutput, results);
						return results.ToArray();
					}
				}
				catch (Exception ex)
				{
					results.Add( "error: Entities/Regexs threw " + ex.Message );
					EnsureOutputFile(textPath, prefixOutput, results);
					return results.ToArray();
				}

			}
			catch(Exception ex)
			{
				results.Add( "error: ConvertHtmlFileToTextFile threw " + ex.Message );
				EnsureOutputFile(textPath, prefixOutput, results);
				return results.ToArray();
			}
			if(logHtmlStatistics)
			{
				// It was too big to edit when I wrote one tag per line.
				// Still too confusing when I wrote as "tag,count" line.
				// I must read in existing file to re-tally orderly, so:
				// tagname,filecount,tagcount
				if(File.Exists(logHtmlStatisticsFilename))
				{
					string[] prior = File.ReadAllLines(logHtmlStatisticsFilename);
					foreach(string s in prior)
					{
						string[] kv = s.Split(aComma);
						int val = 0;
						if(kv.Length == 2 && int.TryParse(kv[1], out val))
						{
							string nodeName = kv[0];
							if(htmlStatisticsDict.ContainsKey(nodeName) == false)
								htmlStatisticsDict.Add(nodeName, 0);
							htmlStatisticsDict[nodeName] ++;
						}
					}
				}
				List<string> lines = new List<string>();
				foreach(KeyValuePair<string, int> kvp in htmlStatisticsDict)
				{
					lines.Add(kvp.Key + "," + kvp.Value);
				}
				File.WriteAllLines(logHtmlStatisticsFilename, lines);
			}
			// Only return not needing a -- EnsureOutputFile(textPath, prefixOutput, results);
			return results.ToArray(); // full success
		}


		// Duplicate method used in both PIPE and HTML2TEXT code:
		public static bool ILikeThisUrl(string url)
		{
			// from moved, or from SERP hits, or from any other anchor references.
			if(url.Contains("youtube.com/"))
				return false;

			if(url.Contains("github.com/"))
				return false;
			
			if(url.Contains("video.search.yahoo.com"))
				return false;
			
			if(url.Contains("images.search.yahoo.com"))
				return false;
			
			// rid foreign-language Wiki links versus https://en.wikipedia.org/
			int n = url.IndexOf(".wikipedia.org/");
			if(n > 2)
			{
				string language = url.Substring(n - 2, 2);
				if(language != "en")
					return false;
			}
			
			// The remote server returned an error: (999) Request denied.
			// on [https://www.linkedin.com/in/...]
			if(url.Contains("linkedin.com/"))
				return false;
			

			// I could add this rule, but they only seemed to come from Twitter
			//if(url.Contains("//fb.me/")) // exclude links into Facebook
			//		return false;
			
			return true; // I like Most URLs
		}
		
		static string straightlyCleanAnchorText(string title)
		{
			string s = reNonWord.Replace(title, " ").Trim();
			if(s.Length > 72)
			{
				s = s.Substring(0, 72);
				int n = s.LastIndexOf(' ');
				if(n > 0)
				{
					s = s.Substring(0, n) + "...";
				}
			}
			return s;
		}

	}
}