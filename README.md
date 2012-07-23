JeBot Help
==========

**NOTE:** JeBot was recently re-written. Many of the features listed here are not complete.

JeBot is a bot written by SirCmpwn. You can email him at sir@cmpwn.com. JeBot hangs out in #minecraft.

If you wish to keep up on development of JeBot, his sister bot is IAmABotAMA. IAmABotAMA hangs out in #IAmABotAMA and has the latest features. IAmABotAMA is unstable.

Note that JeBot is open source. We use a white-hat policy with bug reporting. Anyone who finds a bug that will allow use of manager commands or bestow operatorship upon non-managers
and *uses* it in #minecraft will earn a permenant ban from #minecraft. However, it is encouraged that you quietly notify a manager, or SirCmpwn, that this bug exists.

Upcoming features for JeBot are shown in *italic*.

Commands
--------

JeBot's commands are all prefixed by ".", for example: ".ping". IAmABotAMA uses "@".  If you send either of these bots a private message, omit the control character. You may also use C-style comments: the bot sees ".ping // comment" as ".ping".

* **.down \[address]:** Shows whether or not the web page at \[address] is down or not.
* **.hug \[user]:** Gives \[user] a hug. \[user] is optional, and the bot will hug you if omitted.
* **.karma \[user]:** Shows \[user]'s [Reddit](http://reddit.com) karma.
* **.lwjgl:** Shows information about updating lwjgl on Linux.
* **.mwiki \[terms]:** Searches the [Minecraft Wiki](http://minecraftwiki.net) for \[terms] and returns the first result.
* *.op:* Mentions, or "pings", an active channel operator for assistance.
* **.owner:** Shows information about the bot operator.
* **.ping:** Responds with "pong"
* **.ping \[address]:** Pings the Minecraft server at \[address]. Remember that server advertising is bannable - use at your own risk.
* **.readers \[subreddit]:** Shows the number of subscribers (or readers) of the given subreddit. "subreddit", "r/subreddit", and "/r/subreddit" are all acceptable values for \[subreddit]
* **.search \[terms]:** Shows the first Google result for \[terms]. Alias: .lucky
* **.servers:** Shows the address and status of each official channel server.
* **.title \[page]:** Looks up \[page] and shows the title of it. \[page] must lead to an HTML document, or this command will fail.
* **.status:** Shows the status of minecraft.net services.
* **.tw \[user]:** Shows the latest tweet from \[user]'s [Twitter](http://twitter.com) account.
* **.weather \[area]:** Shows the weather for \[area].
* **.whoami:** Repeats your nick to you.
* **.wiki \[terms]:** Searches [Wikipedia](http://en.wikipedia.org) for \[terms] and returns the first result.
* **.youtube \[video]:** Looks up and shows information about \[video]. Valid options for \[video] include "dQw4w9WgXcQ", "http://www.youtube.com/watch?v=dQw4w9WgXcQ", and "http://youtu.be/dQw4w9WgXcQ", and variations on that theme. Alias: .yt

Manager-Only Commands
----------------------

**Currently disabled**

Managers are distinct from channel operators. Managers are folks who are able to administrate the bot.  Here are the commands that only they may use:

* **.ban \[user]:** Bans \[user] from the channel. *Planned: .ban \[user] \[time] Alias: .kban*
* *.bans \[user]:* Fetches information about each time [user] has been banned while JeBot was present.
* **.deop \[user]:** Sets -o on \[user]. If \[user] is omitted, you will receive -o.
* **.devoice \[user]:** Sets -v on \[user]. If \[user] is omitted, you will receive -v.
* **.echo \[text]:** The bot will show \[text].
* **.cs \[command]:** The bot will message ChanServ \[command].
* **.invite \[user]:** Sends a channel invite to \[user].
* **.join \[channel]:** The bot will join \[channel]. This is not permanent. Permanent channel additions must be set in config.xml. Ask SirCmpwn.
* **.kick \[user]:** Kicks \[user] from the channel.
* **.mute:** Sets the channel to +m.
* **.mute \[user]:** Sets +q on \[user]. Alias: .quiet
* *.note \[user]:* Fetches notes about the given user.
* *.note \[user] \[message]:* Records a note about the given user.
* **.ns \[command]:** The bot will message NickServ \[command].
* **.op \[user]:** Sets +o on \[user]. If \[user] is omitted, you will receive +o.
* **.part:** The bot will leave the channel.
* **.redirect \[user] \[channel]:** Sets a ban on \[user] that redirects them to \[channel].
* **.silent:** Toggles silent mode. When on, the bot will not respond to any non-managers.
* **.stab \[user]:** Mutes \[user] for 5 minutes.
* **.topic \[newtopic]:** Changes the topic to \[newtopic].
* **.topic s/regex/replacement/:** Runs the specified regex on the topic.
* **.topicappend \[text]:** Appends [text] to the topic.
* **.topicprepend \[text]:** Prepends [text] to the topic.
* **.topicrestore:** If you accidentally the topic, this will undo it.
* **.unban \[ban]:** Removes the ban on \[ban].
* **.unmute:** Sets the channel to -m.
* **.unmute \[user]:** Sets -q on \[user]. Alias: .unquiet
* **.voice \[user]:** Sets +v on \[user]. If \[user] is omitted, you will receive +v.

Be aware - the manager-only commands have less protection from doing stupid things than the global commands. Double check that the commands you provide are not erroneous.

Output Redirection
------------------

**Currently disabled**

You are able to redirect the bot's output however you like.  For example, say you use this command, and are logged in as "User":

    .ping

The default output would be:

    User: pong

However, you can redirect it:

    .ping @OtherUser

Which outputs:

    OtherUser: pong

Bot managers are able to redirect to private message and other channels as well.

    .ping > OtherUser

This redirects the output to OtherUser via query. Anyone is able to redirect to themselves, only managers may redirect to anyone.

    .ping > #otherchannel

This will redirect the output to any channel the bot is in. This will not work unless the bot is already present in that channel. Only managers may use this feature.

Notes
-----

JeBot also has spam protection and caps lock protection. JeBot will issue a kick to any user that submits more than 5 messages per second, or any user who submits a message whose length is greater than 30 and is 80% or more capital letters. *Additionally, JeBot will automatically set a temporary redirect on users who spam part/join messages to ##fix_your_connection. JeBot will also notify users who incorrectly set their cloaks upon joining.*