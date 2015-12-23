# Frequently Asked Questions #

## General ##

### What is ReviewBoardVsx? ###
ReviewBoardVsx is a Visual Studio Extension that allows Code Reviews via ReviewBoard.

The current mission of this project is:
  1. Replicate as much post-review behavior as possible in Visual Studio.
  1. Leverage/Reuse as much as possible the hard work already put in to post-review.

The above mission statement is the driving force behind many of the design decisions `[`and performance problems`]` mentioned below.

The mission statement may change and re-prioritize some design decisions as user feedback is received.

### What is a Code Review? ###
Are you kidding me? :)

See:
  * http://www.reviewboard.org/docs/manual/1.5/users/getting-started/what-is-code-review/
  * http://code.google.com/p/support/wiki/CodeReviews

### What is ReviewBoard? ###
See http://www.reviewboard.org/docs/manual/1.5/users/

### What is post-review? ###
post-review.exe is a command-line client that "posts" code reviews to a ReviewBoard server.<br>
post-review is part of ReviewBoard's "RBTools" tool set.<br>
<br>
See <a href='http://www.reviewboard.org/docs/manual/1.5/users/tools/post-review/'>http://www.reviewboard.org/docs/manual/1.5/users/tools/post-review/</a>

<h3>How do I install post-review.exe?</h3>
<ol><li>Install Python 2.6: <b>python-2.6.6.msi</b><br><a href='http://www.python.org/download/'>http://www.python.org/download/</a>
</li><li>Install Python 2.6 setuptools: <b>setuptools-0.6c11.win32-py2.6.exe</b><br><a href='http://pypi.python.org/pypi/setuptools#credits'>http://pypi.python.org/pypi/setuptools#credits</a>
</li><li>Install diffutils: <b>diffutils-2.8.7-1.exe</b><br><a href='http://gnuwin32.sourceforge.net/packages/diffutils.htm'>http://gnuwin32.sourceforge.net/packages/diffutils.htm</a>
</li><li>Install an SCM client:<br>
<ul><li>SVN: CollabNet SVN Client: <b>CollabNetSubversion-client-1.6.6-4.win32.exe</b><br><a href='http://www.open.collab.net/downloads/subversion/'>http://www.open.collab.net/downloads/subversion/</a>
</li></ul></li><li>Add the following to your PATH (change the below paths as appropriate for your machine):<br>
<pre><code>C:\python26\scripts<br>
C:\Program Files\GnuWin32\bin<br>
C:\Program Files\CollabNet\Subversion Client<br>
</code></pre>
</li><li>Finally, install RBTools (post-review.exe):<br>
</li></ol><blockquote>Open a fresh command-prompt window and run:<br>
<pre><code>easy_install -U RBTools<br>
</code></pre></blockquote>

Before you use ReviewBoardVsx, make sure that you can post-review.exe from the command-line at least one file in the Visual Studio solution you will be working with.<br>
<br>
<h3>Does ReviewBoardVsx work with XYN SCM?</h3>
The general answer is that if it works in post-review then it should work in ReviewBoardVsx.<br>
<br>
So far only SVN has been confirmed to work. I you use another SCM please contact the developers to let them know of any success or failure. Feel free to open an Issue if you find one.<br>
<br>
<h2>Technical</h2>

<h3>Why is ReviewBoardVsx so slow?</h3>
Because <code>[</code>, for now,<code>]</code> when you launch "ReviewBoard" it checks for file changes by "post-review"ing every individual file in your Visual Studio Solution.<br>
<br>
<h3>Can't you just scan the solution <b>folder</b> instead of individual <b>files</b>?</h3>

I wish. Visual Studio Solutions and C++ Projects <code>[</code>and perhaps other components<code>]</code> let you add items that exist above/outside of the Solution/Project root folder.<br>
<br>
Imagine the following Visual Studio Solution containing a C++ Project:<br>
<pre><code>C:<br>
└───myproject<br>
    │    *license.txt<br>
    │    ...<br>
    ├─── inc<br>
    │    └─── *common.h<br>
    │         ...<br>
    ├─── mysolution<br>
    │    │    mysolution.sln<br>
    │    └─── cppproject<br>
    │         cppproject.vsproj<br>
    └─── ...<br>
</code></pre>
mysolution.sln has references to the modified file license.txt.<br>
cppproject.vcproj has references to the modified file common.h.<br>
<br>
Now, imagine doing the following:<br>
<pre><code>cd c:\myproject\mysolution\<br>
post-review.exe<br>
</code></pre>

post-review.exe will only see items that exist in c:\myproject\mysolution\ <b>and below</b>; it will <b>not</b> see license.txt or common.h.<br>
<br>
Also keep in mind that a Visual Studio Solution/Project can <b>exclude</b> files that are in the SCM. This means that post-review <b>will see</b> files that are <b>excluded</b> from the Solution/Project.<br>
<br>
post-reviewing from c:\myproject would pull in <b>every</b> file, not just the few referenced by the Solution.<br>
<br>
It is important to realize that <b>this is a very desirable feature</b> of Visual Studio, but it causes problems with post-review and the SCM tools that do not know about the internal dependencies of a Visual Studio Solution/Project.<br>It is almost unrealistic to expect the problem to be solved by improving post-review or the SCM tools.<br>
<br>
Put another way, the reason for scanning individual files is quite simply this:<br>
<ul><li>Visual Studio can reference individual files outside of the known SCM tree.<br>
</li><li>Any solution to this problem must be able to operate at the individual file level.</li></ul>

The trick is, how to do this fast?<br>
<br>
There are really two orthogonal problems here:<br>
<ol><li>Be able to find changes in individual Solution/Project files<br>
</li><li>Do it fast</li></ol>

Below are some possible workarounds, but so far none of them seem desirable or doable in the immediate future:<br>
<ol><li>Background post-review the solution and cache the detected changes, refreshing the cache when a file is saved.<br>This seems the most promising and is similar to how AnhkSVN works.<br>
</li><li>Since many popular Project types (C# and VB) don't support outside references, only do the time consuming post-review of individual files in Solutions and Projects that do support outside references.<br>For all other Projects just call the SCM diff tool directly.<br>This doesn't actually solve the problem.<br>
</li><li>Utilize multi-select to only post-review the selected items.<br>This doesn't solve the most common use case of wanting to code review the entire solution.<br>
</li><li>Rewrite post-review.exe in C#, starting with SVN.<br>This also doesn't really solve the problem.<br>
</li><li>Use some crazy smart map/reduce shortest-path concept that finds all solution changes using the smallest combination of folders and files.</li></ol>

All the solutions would still eventually need to pass the paths to post-review <code>[</code>or the underlying SCM tool or some C# implementation of the SCM tool<code>]</code> to detect the changes. Worst case, <b>all</b> files will still need to be individually tested for changes and no performance gain will be result.<br>
<br>
#1 is the most promising, but one of the  most complex; I will start investigating this soon.<br>
<br>
<h3>Why does ReviewBoardVsx use post-review.exe?</h3>
Because ReviewBoardVsx is intended to support every SCM that post-review.exe supports.<br>
<br>
Post-Review is a mildly complicated 2800+ line program that supports <b>many</b> SCMs, each having their own quirks. The ReviewBoardVsx project does not currently have enough development resource to rewrite/port, test, and maintain a full C# "copy" of everything post-review.exe does. Over time C# code can be added to replace certain post-review.exe requirements, but until 100% of the post-review features are implemented in C#, ReviewBoardVsx will always have <b>some</b> dependency on post-review.<br>
<br>
Improvements to post-review itself are also in the works; perhaps those improvements to help solve ReviewBoardVsx's performance problem.<br>
<br>
<h3>Why don't you use SharpSvn?</h3>
Because ReviewBoardVsx is intended to support every SCM that post-review.exe supports...not just SVN.<br>
<br>
Writing a C# version of post-review starting with using SharpSvn sound nice, but this was attempted and hit a dead-end.<br>
<br>
The real reason ReviewBoardVsx doesn't use SharpSvn is because the diff output of "svn stat" needs to be massaged a bit before it can be uploaded to ReviewBoard.<br>
<br>
post-review.exe accomplishes this by calling "svn diff --diff-cmd=diff" which uses the GNU diff tool in your PATH. I cannot find a way to get SharpSvn to honor the "diff-cmd" argument and use the GNU diff tool. I even considered porting GNU diff to C#, but that probably wouldn't work since GNU diff needs to be called <b>by</b> "svn diff", not <b>after</b>. So far I have been unable to come up with a<br>
<br>
SharpSvn solution that outputs a diff format compatible with ReviewBoard.<br>
<br>
Solve this and you can implement a a nice full C# SharpSvn based ReviewBoard solution...but you will only be supporting <b>one</b> of SCMs that post-review supports.<br>
<br>
<h3>Why don't you just post-review the <a href='multi.md'>multi</a> selected items?</h3>
Riddle me this: If a folder has 3 files in it, and I select both the folder and a one file, should I post-review just the single file, or <b>every</b> file in and below that folder?<br>
<br>
I understand the desire of such a feature, and was in fact what I was hoping to achieve, but the above dilemma exploded in to a more complicated problem that I would rather solve later after I fix bigger issues.<br>
<br>
<h2>You stole some code from AnhkSVN!</h2>
I shamelessly did pick and choose a few concepts and actual files from AnkhSVN, but AnkhSVN is Open Source and all licensing headers remain intact in the few copied files used by this project.