# [ReviewBoard](http://code.google.com/p/reviewboard/) Visual Studio Package/Extension/Plug-In/Whatever #

<table>
<tr>
<td valign='top'>
Please email the admin(s) and let them know if you think that <a href='http://reviewboardvsx.codeplex.com/'>http://reviewboardvsx.codeplex.com/</a> is a more appropriate place to host this project.<br>
<br>
I would also consider hosting this project in the ReviewBoard github along side rbtools.<br>
<br>
Current limitations:<br>
<ul><li>THIS PRODUCT IS STILL ALPHA!<br>
</li><li>There is currently no support for updates; you may need to fully uninstall an old version before installing a newer version.<br>
</li><li>Crawls the <b>ENTIRE</b> Visual Studio <b>Solution</b>, ignoring any selected items.<br />I hope to eventually optimize performance and selection behavior.<br>
</li><li>Requires post-review.exe in your PATH, which also requires GNU diff.exe in your PATH for SVN support.<br>
</li><li>You should verify you can successfully run post-review.exe from the command-line.<br>
</li><li>Server field doesn't work yet (and is thus disabled); you will need to have reviewboard:url defined as an SVN property somewhere up the parent tree.<br>
</li><li>There are still a lot of bugs; please report them.</li></ul>

Complications:<br>
<ul><li>Visual Studio Solutions, especially C++ Projects and their "Filter" folders, can reference files from <b>any</b> directory.<br /><i>(ie: potentially <b>above</b> the solution root folder)</i><br /><b>This means that it is possible for a VS Solution to contain files that all use different SCMs!</b><br />Thus <b>EACH AND EVERY SINGLE FILE</b> must be checked for the SCM supported by the directory that the file is in.<br />This seems like an edge case, but this package supports it.<br>
</li><li>It is also possible that each SCM in the project might reference different ReviewBoard Servers!<br />This seems like an <b>extreme</b> edge case, and I am thinking this package simply will not support it and will display an error dialog if it detects multiple reviewboard servers.</li></ul>

NOTE: I originally had grandiose ideas to replace all use of post-review.exe with a full C# post-review API and an SvnClient (that used SharpSvn).<br>
While looking in to the post-review.py code and the changes needed to make the SVN diff output work with ReviewBoard I was having a very hard time trying to get SharpSvn to call GNU diff (via the "--diff-cmd=diff" argument). This meant that I would also probably have to implement GNU diff in C#.<br>
I felt like this was snowballing in to a major effort...and then I found this 2010/12/20 message from Christian Hammond:<br>
<a href='http://groups.google.com/group/reviewboard/browse_thread/thread/bfb0743ddb35969c'>http://groups.google.com/group/reviewboard/browse_thread/thread/bfb0743ddb35969c</a>

Enough said; I am sticking w/ using post-review.exe exclusively now...until the RB crew comes up w/ a better interface.<br>
</td>
<td valign='top'>
<a href='http://www.youtube.com/watch?feature=player_embedded&v=Nt7yXdmdkHs' target='_blank'><img src='http://img.youtube.com/vi/Nt7yXdmdkHs/0.jpg' width='425' height=344 /></a><br>
</td>
</tr>
</table>