# Context

Existing plugins focus on their areas of operation - e.g. Browser Bookmarks on bookmarks only, Browser Tabs on tabs only.  
Why not join these two worlds to create synergy?  
Especially if one works with **tens or hundreds of bookmarks and open tabs** (I do and I constantly struggle finding the correct tab. It is underestimated mental cost of finding, clicking several times, etc.).  

That's where "Reuse tabs" setting in Browser Bookmarks plugin makes sense.  

I believe it is in line with why Flow Launcher was created in the first place.  
I strongly believe in a higher-level concept of **"just take me to THIS place - as fast as possible, as easy as possible"**.  
Thus making bridges between plugins may sometimes produce huge value! BTW wouldn't it be nice to allow inter-plugin communication to create this kind of "bridges" more easily?  

# How it works

The core is Browser Bookmarks plugin, unchanged by default.  
You may enable "Reuse tabs" in the plugin settings.  
Then, whenever one opens a bookmark, it also registers a new tab in its cache.  
Next, each time the bookmark is triggered again, it just switches to the existing tab instead of launching a new one.  
**It takes milliseconds instead of long seconds** (or sometimes close to half a minute in corporate environments where all is slow even if you have a high-end laptop - you won't believe it until you live it!).  

# Alternatives

Reading URLs of existing tabs was tried. It would make mapping of bookmarks to tabs more reliable.  
However due to security reasons it has several limitations:  

- different browsers expose internals differently
- it is not easily accessible (e.g. you cannot make Chrome expose internal details on a dev TCP port from default profile so user would have to take care about special settings).
