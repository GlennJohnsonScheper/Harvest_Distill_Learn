# Harvest_Distill_Learn

Every day, I harvest vast queries and websites, distill intelligence, and learn quickly with smooth scrolling.

- https://github.com/GlennJohnsonScheper/Harvest_Distill_Learn.git

- HARVEST - queries search engines, fetches web pages, sites to folder.

- DISTILL - processes folders of saved web pages each into a text file.

- LEARN - smooth scrolls such results or any texts with minimal motion.

All the .exe and .dll files reside in the top folder, peers to folders.

This whole folder structure can reside anywhere, even on a flash drive.

I have included example search engine queries and site harvest results.

----- HARVEST -----

Harvest assumes/creates the Harvest "Q" (QUEUE) folder, peer to executable.

Harvest processes each subfolder of Q, unless it starts with hyphen = Not In Service.

Within each subfolder of Q:

- Harvest processes input files (ask.txt, want.txt, site.txt) to search and fetch web information.
- - File ask.txt contains queries, like you would type into a search engine.
- - File want.txt contains URLs, which will be fetched leisurely per domain.
- - File site.txt contains URL parts to match, to add any new links to want.
- - Harvest writes RAW HTTP DATA, engine and hit pages into subfolder "#",
- - Harvest immediately saves VALUABLE .txt, .pdf, etc into subfolder "$".
- - Harvest writes a file link.txt of (most) all URLs encountered in pages.

Harvest writes a file in the top cur dir, named "KeepRunning.txt".
If user deletes "KeepRunning.txt", Harvest will soon quit and exit.

Harvest fetches http(s) requests using this following request UserAgent header:
HDL 1.0 (Harvest_Distill_Learn 2022-07-03 by IneffablePerformativity@gmail.com

----- DISTILL -----

Distill assumes the Harvest "Q" folder is peer to executable.

Distill creates the Reading "R" folder as peer to executable.

Distill processes each subfolder of Q, unless it starts with hyphen = Not In Service.

Distill processes many .txt files of any subfolder "$" thereunder as input data.

Distill creates one text file in R from each subfolder\$ as smart reading data.

- Extracts redundant phrases across pages to top section.
- Separates files by headers with "asdf#", url, title.
- Highest scoring files first, generally the largest.
- Ignores the query SERP pages fetched by Harvest.

Distill also copies any non-.txt (.pdf) files of $ into R\Other for user to open.

----- LEARN -----

Please consult the source code to verify exact keystroke functionality.

CONTROL+O opens a file to read

CONTROL+V pastes clipboard to read

Starting the LEARN app will immediately paste from the clipboard:
Handy when browsing a website, ^A, ^C, run learn, begin learning.

ARROW UP starts smooth scrolling

ARROW RIGHT goes faster

ARROW LEFT goes slower

ARROW DOWN stops scrolling, and minimizes app.

A Key cannot cause app to rise up from minimized state!
-- Instead, please assign a Windows hotkey (Ctl-Alt-L).

And on left-hand, TAB stops scrolling, and minimizes app.

Also, INSERT starts smooth scrolling

Also, DELETE stops smooth scrolling

ESCAPE - exits program

Also, CONTROL+Q - exits program
Also, CONTROL+X - exits program
Also, CONTROL+Z - exits program

CONTROL+F - searches forward. By default, pattern is "asdf".

CONTROL-R - searches backward.

HOME, END, PAGE UP, PAGE DOWN - work as expected

Home and End also set current per-mille location before moving.

CONTROL+A - Anywhere (random jump)

CONTROL+B - Brown Background

CONTROL+G - Green Background (default)

And also,

CONTROL+G - GO!, jump to a per-mille location in document

Last numeric keystrokes accumulate to determine a ###/1000.
Form caption updates to per-mille location upon keystrokes.

CONTROL+J - Jump, similar to G, older code, I don't use it.
I think J did an adaptive range, like #/10, or ##/100, etc.

CONTROL+W - toggles handling of multiple whitespaces in line

CONTROL+L - reopens Last input file

CONTROL++ (or CONTROL+=) - Bigger font

CONTROL+- (or CONTROL+_) - Smaller font

Other keystrokes accumulate to determine the search pattern.

Search pattern only matches whole words (no letter adjacent)
unless the pattern starts with and/or ends with an asterisk.

Any CONTROL KEY restarts the ^F/^R/^J/^G accumulator index.


----- SORTLINKS -----

Utility app SortLinks.exe processes each subfolder of Q.

SORTLINKS sorts all the lines of link.txt into sort.txt.



Good Reading! Bonne Lecture! Gutes Lesen!

