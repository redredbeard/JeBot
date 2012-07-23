using System;
using System.Collections.Generic;
using System.Linq;
using IrcBotFramework;
using System.Xml.Linq;
using LibMinecraft.Model;
using System.Net;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using System.Globalization;
using System.Web;
using System.Threading;

namespace JeBot
{
    public class JeBotMain : IrcBot
    {
        public List<string> VerifiedUsers, Managers, NotifyVerification;
        private Queue<CommandPendingID> IdentificationQueue;
        private object IdentificationQueueLock = new object();
        private IrcUser ChangingHostNotify;

        public JeBotMain(string ServerAddress, IrcUser User) : base(ServerAddress, User)
        {
            // Event handlers
            this.ChannelListRecieved += new EventHandler<ChannelListEventArgs>(JeBotMain_ChannelListRecieved);
            this.UserJoinedChannel += new EventHandler<UserJoinEventArgs>(JeBotMain_UserJoinedChannel);
            this.UserPartedChannel += new EventHandler<UserPartEventArgs>(JeBotMain_UserPartedChannel);
            this.UserQuit += new EventHandler<UserQuitEventArgs>(JeBotMain_UserQuit);
            this.UserMessageRecieved += new EventHandler<UserMessageEventArgs>(JeBotMain_UserMessageRecieved);
            this.NoticeRecieved += new EventHandler<NoticeEventArgs>(JeBotMain_NoticeRecieved);

            VerifiedUsers = new List<string>();
            NotifyVerification = new List<string>();
            Managers = new List<string>();
            IdentificationQueue = new Queue<CommandPendingID>();
            ManagerCommands = new Dictionary<string, CommandHandler>();

            // Userspace commands
            RegisterCommand("down", DoDown);
            RegisterCommand("docs", (command) => "https://github.com/irc-bot-framework/JeBot/README.md");
            RegisterCommand("documentation", (command) => "https://github.com/irc-bot-framework/JeBot/README.md"); // alias
            RegisterCommand("help", (command) => "https://github.com/irc-bot-framework/JeBot/README.md"); // alias
            RegisterCommand("man", (command) => "https://github.com/irc-bot-framework/JeBot/README.md"); // alias
            RegisterCommand("hug", DoHug);
            RegisterCommand("identify", DoIdentify);
            RegisterCommand("karma", DoKarma);
            RegisterCommand("lwjgl", (command) => "Can you use git? Use this script: https://gist.github.com/2086385 No? Read this: http://www.minecraftwiki.net/wiki/Tutorials/Update_LWJGL");
            RegisterCommand("mwiki", DoMwiki);
            RegisterCommand("owner", (command) => "I was created and am maintained by SirCmpwn (sir@cmpwn.com). Please send him an email if you find a bug or want a new feature.");
            RegisterCommand("ping", DoPing);
            RegisterCommand("readers", DoReaders);
            RegisterCommand("search", DoSearch);
            RegisterCommand("lucky", DoSearch); // alias
            RegisterCommand("servers", DoServers);
            RegisterCommand("source", (command) => "https://github.com/irc-bot-framework/JeBot");
            RegisterCommand("status", DoStatus);
            RegisterCommand("title", DoTitle);
            RegisterCommand("twitter", DoTwitter);
            RegisterCommand("tw", DoTwitter); // alias
            RegisterCommand("weather", (command) => FetchWeather(command.Message) ?? "Unable to fetch weather.");
            RegisterCommand("whoami", (command) => command.Source.Nick);
            RegisterCommand("wikipedia", DoWikipedia);
            RegisterCommand("wiki", DoWikipedia); // alias
            RegisterCommand("youtube", DoYoutube);
            RegisterCommand("yt", DoYoutube); // alias
            RegisterCommand("xkcd", DoXkcd);

            // Manager commands
            RegisterManagerCommand("ban", DoBan);
            RegisterManagerCommand("deop", DoDeop);
            RegisterManagerCommand("devoice", DoDevoice);
            RegisterManagerCommand("echo", DoEcho);
            RegisterManagerCommand("invite", DoInvite);
            RegisterManagerCommand("join", DoJoin);
            RegisterManagerCommand("kick", DoKick);
            RegisterManagerCommand("op", DoOp);
            RegisterManagerCommand("topic", DoTopic);
        }

        #region Event Handlers

        void JeBotMain_NoticeRecieved(object sender, NoticeEventArgs e)
        {
            if (!(e.Notice.Source.Contains("@") && e.Notice.Source.Contains("!"))) // verify that this came from a user
                return;
            var source = new IrcUser(e.Notice.Source);
            if (source.Nick == "NickServ" && e.Notice.Message.Contains("ACC")) // Do user identification
            {
                var parts = e.Notice.Message.Split(' ');
                lock (IdentificationQueueLock)
                {
                    CommandPendingID callback = null;
                    if (IdentificationQueue.Count != 0)
                        callback = IdentificationQueue.Dequeue();
                    if (parts[2] == "3") // verified
                    {
                        VerifiedUsers.Add(parts[0]);
                        if (callback != null)
                            callback.Callback(callback.Command);
                        if (NotifyVerification.Contains(parts[0]))
                        {
                            this.SendNotice(parts[0], "You have been successfully identified.");
                            NotifyVerification.Remove(parts[0]);
                        }
                    }
                }
            }
        }

        void JeBotMain_UserQuit(object sender, UserQuitEventArgs e)
        {
            // Send users a notice when they change hosts
            if (e.Reason == "Changing host")
                ChangingHostNotify = e.User;
        }

        void JeBotMain_UserMessageRecieved(object sender, UserMessageEventArgs e)
        {
            // TODO: Spam detection, caps detection
        }

        void JeBotMain_UserPartedChannel(object sender, UserPartEventArgs e)
        {
            // Remove the user from the verified list if needed
            if (VerifiedUsers.Contains(e.User.Nick))
                VerifiedUsers.Remove(e.User.Nick);
        }

        void JeBotMain_UserJoinedChannel(object sender, UserJoinEventArgs e)
        {
            if (ChangingHostNotify != null)
            {
                // Notify the user if they need to reconfigure their client for changing hosts
                if (e.User.Nick == ChangingHostNotify.Nick)
                {
                    this.SendNotice(e.User.Nick, "Please reconfigure your client to use your NickServ password as your server password to avoid spamming when you join. See http://freenode.net/faq.shtml#nocloakonjoin");
                    ChangingHostNotify = null;
                }
            }
            // Identify this user if they are included in the manager list
            if (Managers.Contains(e.User.Nick))
                this.SendMessage("NickServ", "ACC " + e.User.Nick);
        }

        void JeBotMain_ChannelListRecieved(object sender, ChannelListEventArgs e)
        {
            // Browse the channel list for managers and identify each
            return;
            foreach (var nick in e.Channel.Users)
            {
                if (Managers.Contains(nick))
                    IdentifyUser(nick, null);
            }
        }

        #endregion

        #region Commands

        private string DoPing(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                return "pong";
            else if (command.Parameters.Length == 1)
            {
                try
                {
                    var server = MinecraftServer.GetServer(command.Parameters[0]);
                    if (server.IsOnline)
                    {
                        return "\u000303ONLINE\u000F [" + server.ConnectedPlayers +
                            "/" + server.MaxPlayers + "]: " + server.MotD;
                    }
                    else
                        return "\u000305OFFLINE";
                }
                catch
                {
                    return "\u000305OFFLINE";
                }
            }
            else
                return null;
        }

        private string DoDown(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            try
            {
                HttpWebRequest hwr = (HttpWebRequest)WebRequest.Create(
                    new Uri(FormatHttpUri(command.Parameters[0])));
                hwr.Timeout = 3000;
                var response = (HttpWebResponse)hwr.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                    return "Looks up to me";
                else
                    return "Looks down to me";
            }
            catch
            {
                return "Looks down to me";
            }
        }

        private string DoHug(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                SendAction(command.Destination, "hugs " + command.Source.Nick);
            else if (command.Parameters.Length == 1)
                SendAction(command.Destination, "hugs " + command.Parameters[0]);
            return null;
        }

        private string DoIdentify(IrcCommand command)
        {
            NotifyVerification.Add(command.Source.Nick);
            this.SendMessage("NickServ", "ACC " + command.Source.Nick);
            return null;
        }

        private string DoKarma(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            var karma = GetUserKarma(command.Parameters[0]);
            return command.Parameters[0] + " has " +
                karma.LinkKarma.ToString("N0", CultureInfo.InvariantCulture) +
                " link karma and " + karma.CommentKarma.ToString("N0", CultureInfo.InvariantCulture) + " comment karma after " +
                GetDuration(karma.Created) + ". http://reddit.com/u/" + command.Parameters[0];
        }

        private string DoMwiki(IrcCommand command)
        {
            if (string.IsNullOrEmpty(command.Message))
                return null;
            var results = DoGoogleSearch("site:minecraftwiki.net " + command.Message);
            if (results.Count == 0)
                return "No results found.";
            else
                return results.First();
        }

        private string DoReaders(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            string subreddit = command.Parameters[0];
            if (subreddit.StartsWith("r/"))
                subreddit = subreddit.Substring(2);
            if (subreddit.StartsWith("/r/"))
                subreddit = subreddit.Substring(3);
            var result = GetSubreddit(subreddit);
            return "/r/" + subreddit + " has " + result.Readers.ToString("N0", CultureInfo.InvariantCulture) +
                            " readers. http://reddit.com/r/" + subreddit + (result.NSFW ? " \u000305NSFW" : "");
        }

        private string DoSearch(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                return null;
            var results = DoGoogleSearch(command.Message);
            if (results.Count == 0)
                return "No results found.";
            else
                return results.First();
        }

        private string DoServers(IrcCommand command)
        {
            if (command.Parameters.Length != 0)
                return null;
            // TODO: These might not work on the live server
            var factions = MinecraftServer.GetServer("216.185.144.243");
            var hardsurvival = MinecraftServer.GetServer("216.185.144.245");
            var creative = MinecraftServer.GetServer("216.185.144.245");
            return "hardsurvival.badass-gaming.com: " + (hardsurvival.IsOnline ? "\u000303ONLINE \u000f[" + hardsurvival.ConnectedPlayers + "/" + hardsurvival.MaxPlayers + "]" : "\u000305OFFLINE") +
                    "\u000f | factions.badass-gaming.com: " + (factions.IsOnline ? "\u000303ONLINE \u000f[" + factions.ConnectedPlayers + "/" + factions.MaxPlayers + "]" : "\u000305OFFLINE") +
                    "\u000f | creative.badass-gaming.com: " + (creative.IsOnline ? "\u000303ONLINE \u000f[" + creative.ConnectedPlayers + "/" + creative.MaxPlayers + "]" : "\u000305OFFLINE");
        }

        private string DoStatus(IrcCommand command)
        {
            var status = GetMojangStatus();
            return "\u000fWebsite: " + (status.Website ? "\u000303ONLINE" : "\u000305OFFLINE") +
                "\u000f | Login: " + (status.Login ? "\u000303ONLINE" : "\u000305OFFLINE") +
                "\u000f | Session: " + (status.Session ? "\u000303ONLINE" : "\u000305OFFLINE") +
                "\u000f | Accounts: " + (status.Account ? "\u000303ONLINE" : "\u000305OFFLINE") +
                "\u000f | Authentication: " + (status.Authentication ? "\u000303ONLINE" : "\u000305OFFLINE");
        }

        private string DoTitle(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            return FetchPageTitle(FormatHttpUri(command.Message)) ?? "Unable to fetch title.";
        }

        private string DoTwitter(IrcCommand command)
        {
            if (command.Parameters.Length != 1) // TODO: Expand this function
                return null;
            return GetTweet(command.Parameters[0]);
        }

        private string DoWikipedia(IrcCommand command)
        {
            if (string.IsNullOrEmpty(command.Message))
                return null;
            var results = DoGoogleSearch("site:en.wikipedia.org " + command.Message);
            if (results.Count == 0)
                return "No results found.";
            else
                return results.First();
        }

        private string DoYoutube(IrcCommand command)
        {
            string vid;
            if (command.Parameters.Length != 1)
            {
                var results = DoGoogleSearch("site:youtube.com " + command.Message);
                if (results.Count == 0)
                    return "No results found.";
                else
                    vid = results.First().Substring(results.First().LastIndexOf("http://"));
            }
            else
                vid = command.Parameters[0];
            if (Uri.IsWellFormedUriString(vid, UriKind.Absolute))
            {
                Uri uri = new Uri(vid);
                var query = HttpUtility.ParseQueryString(uri.Query);
                vid = query["v"];
                if (vid == null)
                    return "Video not found.";
            }
            var video = GetYoutubeVideo(vid);
            if (video == null)
                return "Video not found.";

            string partOne = "\"\u0002" + video.Title + "\u000f\" [" +
                video.Duration.ToString("m\\:ss") + "] (\u000312" + video.Author + "\u000f)\u000303 " +
                (video.HD ? "HD" : "SD");

            string partTwo = video.Views.ToString("N0", CultureInfo.InvariantCulture) + " views";
            if (video.RatingsEnabled)
                partTwo += ", " +
                    "(+\u000303" + video.Likes.ToString("N0", CultureInfo.InvariantCulture) +
                    "\u000f|-\u000304" + video.Dislikes.ToString("N0", CultureInfo.InvariantCulture) + "\u000f) [" + video.Stars + "]";

            if (video.RegionLocked | !video.CommentsEnabled || !video.RatingsEnabled)
            {
                partTwo += " ";
                if (video.RegionLocked)
                    partTwo += "\u000304Region locked\u000f, ";
                if (!video.CommentsEnabled)
                    partTwo += "\u000304Comments disabled\u000f, ";
                if (!video.RatingsEnabled)
                    partTwo += "\u000304Ratings disabled\u000f, ";
                partTwo = partTwo.Remove(partTwo.Length - 3);
            }

            if (partOne.Length < partTwo.Length)
                partOne += "\u000f " + video.VideoUri.ToString();
            else
                partTwo += "\u000f " + video.VideoUri.ToString();

            SendMessage(command.Destination, partOne);
            SendMessage(command.Destination, partTwo);

            return null;
        }

        private string DoXkcd(IrcCommand command)
        {
            if (string.IsNullOrEmpty(command.Message))
                return null;
            var results = DoGoogleSearch("site:xkcd.com " + command.Message);
            if (results.Count == 0)
                return "No results found.";
            else
                return results.First();
        }

        #endregion

        #region Manager Commands

        private string DoBan(IrcCommand command)
        {
            var channel = GetChannel(command.Destination);
            if (command.Parameters.Length == 1)
            {
                if (channel.Users.Contains(command.Parameters[0]))
                {
                    this.WhoIs(command.Parameters[0], (user) =>
                        {
                            this.ChanServ.TempOp(channel.Name, () =>
                                {
                                    // TODO: Log
                                    this.ChangeMode(channel.Name, "+b *!*@*" + user.Hostmask + "*");
                                    this.Kick(channel.Name, user.Nick, "Banned from " + channel.Name);
                                });
                        });
                }
                else
                    return "No such user.";
            }
            return null;
        }

        private string DoDeop(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                this.ChanServ.DeOp(command.Destination, command.Source.Nick);
            else if (command.Parameters.Length == 1)
                this.ChanServ.DeOp(command.Destination, command.Parameters[0]);
            return null;
        }

        private string DoDevoice(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                this.ChanServ.DeVoice(command.Destination, command.Source.Nick);
            else if (command.Parameters.Length == 1)
                this.ChanServ.DeVoice(command.Destination, command.Parameters[0]);
            return null;
        }

        private string DoEcho(IrcCommand command)
        {
            this.SendMessage(command.Destination, command.Message);
            return null;
        }

        private string DoInvite(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            this.ChanServ.TempOp(command.Destination, () =>
                {
                    this.Invite(command.Destination, command.Parameters[0]);
                });
            return null;
        }

        private string DoJoin(IrcCommand command)
        {
            if (command.Parameters.Length != 1)
                return null;
            if (!command.Parameters[0].StartsWith("#"))
                return null;
            JoinChannel(command.Parameters[0]);
            return null;
        }

        private string DoKick(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                return null;
            this.ChanServ.TempOp(command.Destination, () =>
                {
                    if (command.Parameters.Length == 1)
                        this.Kick(command.Destination, command.Parameters[0], "Requested by " + command.Source.Nick);
                    else
                        this.Kick(command.Destination, command.Parameters[0],
                            command.Message.Substring(command.Message.IndexOf(' ') + 1));
                });
            return null;
        }

        private string DoOp(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                this.ChanServ.Op(command.Destination, command.Source.Nick);
            else if (command.Parameters.Length == 1)
                this.ChanServ.Op(command.Destination, command.Parameters[0]);
            return null;
        }

        private string DoTopic(IrcCommand command)
        {
            if (command.Parameters.Length == 0)
                return null;
            this.ChanServ.TempOp(command.Destination, () =>
                {
                    this.Topic(command.Destination, command.Message);
                });
            return null;
        }

        #endregion

        #region Manager Utilities

        public void RegisterManagerCommand(string Command, IrcBot.CommandHandler CommandHandler)
        {
            ManagerCommands.Add(Command, CommandHandler);
            RegisterCommand(Command, HandleManagerCommand);
        }

        private Dictionary<string, IrcBot.CommandHandler> ManagerCommands;
        private string HandleManagerCommand(IrcCommand command)
        {
            if (!Managers.Contains(command.Source.Nick)) // Check for manager
                return null;
            if (!VerifiedUsers.Contains(command.Source.Nick)) // Check for verification
            {
                // Verify and postpone command if needed
                IdentifyUser(command.Source.Nick, new CommandPendingID()
                {
                    Callback = HandleManagerCommand,
                    Command = command
                });
                return null;
            }
            // Execute command
            var resp = ManagerCommands[command.Command](command);
            if (resp != null)
                this.SendMessage(command.Destination, command.Prefix + resp);
            return null;
        }

        private void IdentifyUser(string nick, CommandPendingID callback)
        {
            lock (IdentificationQueueLock)
            {
                IdentificationQueue.Enqueue(callback);
                this.SendMessage("NickServ", "ACC " + nick);
            }
        }

        class CommandPendingID
        {
            public IrcCommand Command;
            public IrcBot.CommandHandler Callback;
        }

        #endregion

        #region Utility Methods

        static string FormatHttpUri(string uri)
        {
            if (!uri.StartsWith("http://") && !uri.StartsWith("https://"))
                return "http://" + uri;
            return uri;
        }

        class MojangStatus
        {
            public bool Website, Login, Session, Account, Authentication;
        }

        private static MojangStatus GetMojangStatus()
        {
            WebClient client = new WebClient();
            var sr = new StreamReader(client.OpenRead("http://status.mojang.com/check"));
            string json = sr.ReadToEnd().Replace("{", "").Replace("}", "");
            json = "{" + json + "}"; // clean up the json a little
            json = json.Replace("[", "").Replace("]", "");
            sr.Close();
            var value = JObject.Parse(json);
            MojangStatus status = new MojangStatus();
            status.Website = value["minecraft.net"].Value<string>() == "green";
            status.Login = value["login.minecraft.net"].Value<string>() == "green";
            status.Session = value["session.minecraft.net"].Value<string>() == "green";
            status.Account = value["account.mojang.com"].Value<string>() == "green";
            status.Authentication = value["auth.mojang.com"].Value<string>() == "green";
            return status;
        }

        class Video
        {
            public string Title, Author;
            public int Views, Likes, Dislikes;
            public TimeSpan Duration;
            public bool RegionLocked, HD, CommentsEnabled, RatingsEnabled;
            public string Stars;
            public Uri VideoUri;
        }

        private static Video GetYoutubeVideo(string vid)
        {
            try
            {
                WebClient client = new WebClient();
                var sr = new StreamReader(client.OpenRead(string.Format("http://gdata.youtube.com/feeds/api/videos/{0}?v=2", vid)));
                string xml = sr.ReadToEnd();
                XDocument document = XDocument.Parse(xml);
                XNamespace media = XNamespace.Get("http://search.yahoo.com/mrss/");
                XNamespace youtube = XNamespace.Get("http://gdata.youtube.com/schemas/2007");
                XNamespace root = XNamespace.Get("http://www.w3.org/2005/Atom");
                XNamespace googleData = XNamespace.Get("http://schemas.google.com/g/2005");
                Video video = new Video();
                video.Title = document.Root.Element(root + "title").Value;
                video.Author = document.Root.Element(root + "author").Element(root + "name").Value;

                video.CommentsEnabled = document.Root.Elements(youtube + "accessControl").Where(e =>
                    e.Attribute("action").Value == "comment").First().Attribute("permission").Value == "allowed";
                video.RatingsEnabled = document.Root.Elements(youtube + "accessControl").Where(e =>
                    e.Attribute("action").Value == "rate").First().Attribute("permission").Value == "allowed";
                if (video.RatingsEnabled)
                {
                    video.Likes = int.Parse(document.Root.Element(youtube + "rating").Attribute("numLikes").Value);
                    video.Dislikes = int.Parse(document.Root.Element(youtube + "rating").Attribute("numDislikes").Value);
                }
                video.Views = int.Parse(document.Root.Element(youtube + "statistics").Attribute("viewCount").Value);
                video.Duration = TimeSpan.FromSeconds(
                    double.Parse(document.Root.Element(media + "group").Element(youtube + "duration").Attribute("seconds").Value));
                video.RegionLocked = document.Root.Element(media + "group").Element(media + "restriction") != null;
                video.VideoUri = new Uri("http://youtu.be/" + vid);
                video.HD = document.Root.Element(youtube + "hd") != null;
                if (video.RatingsEnabled)
                {
                    video.Stars = "\u000303";
                    int starCount = (int)Math.Round(double.Parse(document.Root.Element(googleData + "rating").Attribute("average").Value));
                    for (int i = 0; i < 5; i++)
                    {
                        if (i < starCount)
                            video.Stars += "★";
                        else if (i == starCount)
                            video.Stars += "\u000315☆";
                        else
                            video.Stars += "☆";
                    }
                    video.Stars += "\u000f";
                }
                return video;
            }
            catch
            {
                return null;
            }
        }

        private static string GetDuration(DateTime date)
        {
            var span = DateTime.Now - date;
            if (span.TotalDays < 30)
            {
                if ((int)span.TotalDays != 1)
                    return ((int)span.TotalDays).ToString() + " days";
                else
                    return ((int)span.TotalDays).ToString() + " day";
            }
            if (span.GetMonths() < 12)
            {
                if (span.GetMonths() != 1)
                    return span.GetMonths().ToString() + " months";
                else
                    return span.GetMonths().ToString() + " month";
            }
            if (span.GetYears() != 1)
                return span.GetYears().ToString() + " years";
            else
                return span.GetYears().ToString() + " year";
        }

        private static string GetBanTime(string Command)
        {
            try
            {
                string[] parts = Command.Split(' ');
                int timeInMinutes = 0;
                if (parts.Length == 2)
                    return "60m";
                else
                {
                    string time = parts[2];
                    string workingTime = "";
                    foreach (char c in time)
                    {
                        if (c == 'm')
                        {
                            // workingTime is in minutes
                            timeInMinutes += int.Parse(workingTime);
                            workingTime = "";
                        }
                        else if (c == 'h')
                        {
                            // workingTime is in minutes
                            timeInMinutes += int.Parse(workingTime) * 60;
                            workingTime = "";
                        }
                        else if (c == 'd')
                        {
                            // workingTime is in minutes
                            timeInMinutes += int.Parse(workingTime) * 60 * 24;
                            workingTime = "";
                        }
                        else if (c == 'w')
                        {
                            // workingTime is in minutes
                            timeInMinutes += int.Parse(workingTime) * 60 * 24 * 7;
                            workingTime = "";
                        }
                        else
                        {
                            workingTime += c;
                        }
                    }
                    return timeInMinutes.ToString() + "m";
                }
            }
            catch { return "60m"; }
        }

        static string FetchWeather(string terms)
        {
            try
            {
                WebClient wc = new WebClient();
                StreamReader sr = new StreamReader(wc.OpenRead("http://www.google.com/ig/api?weather=" +
                                                               Uri.EscapeUriString(terms)));
                string data = sr.ReadToEnd();
                sr.Close();
                XDocument document = XDocument.Parse(data);
                var cond = document.Root.Element("weather").Element("current_conditions");
                var location = document.Root.Element("weather").Element("forecast_information");
                string result = "Weather in " + location.Element("city").Attribute("data").Value + ": ";
                result += cond.Element("condition").Attribute("data").Value + ", ";
                result += cond.Element("temp_f").Attribute("data").Value + "°F (";
                result += cond.Element("temp_c").Attribute("data").Value + "°C)";
                return result;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Console.WriteLine(e.ToString());
            }
            return null;
        }

        static string FetchPageTitle(string url)
        {
            try
            {
                WebClient wc = new WebClient(); // I'm sorry, okay?
                StreamReader sr = new StreamReader(wc.OpenRead(url));
                string data = sr.ReadToEnd();
                sr.Close();
                HtmlDocument hDocument = new HtmlDocument();
                hDocument.LoadHtml(data);
                var title = hDocument.DocumentNode.Descendants("title");
                if (title != null)
                {
                    if (title.Count() > 0)
                    {
                        string text = title.First().InnerText;
                        text = text.Replace("\n", "").Replace("\r", "").Trim();
                        if (text.Length < 100)
                            return WebUtility.HtmlDecode(HtmlRemoval.StripTagsRegexCompiled(text));
                    }
                }
            }
            catch { return null; }
            return null;
        }

        class Karma
        {
            public int LinkKarma { get; set; }
            public int CommentKarma { get; set; }
            public DateTime Created { get; set; }
        }

        static Karma GetUserKarma(string username)
        {
            try
            {
                WebClient wc = new WebClient();
                StreamReader sr = new StreamReader(wc.OpenRead("http://reddit.com/user/" + username + "/about.json"));
                string data = sr.ReadToEnd();
                sr.Close();
                JObject json = JObject.Parse(data);
                Karma karma = new Karma();
                karma.CommentKarma = json["data"]["comment_karma"].Value<int>();
                karma.LinkKarma = json["data"]["link_karma"].Value<int>();
                karma.Created = TimeSpanExtensions.UnixTimeStampToDateTime(json["data"]["created_utc"].Value<double>());
                return karma;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Console.WriteLine(e.ToString());
            }
            return null;
        }

        static string GetTweet(string user)
        {
            try
            {
                WebClient wc = new WebClient();
                StreamReader sr = new StreamReader(wc.OpenRead("http://api.twitter.com/1/statuses/user_timeline.xml?screen_name=" + user));
                string data = sr.ReadToEnd();
                sr.Close();
                XDocument document = XDocument.Parse(data);
                return "@" + user + ": \"" + document.Root.Element("status").Element("text").Value +
                    "\" https://twitter.com/" + user + "/status/" + document.Root.Element("status").Element("id").Value;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Console.WriteLine(e.ToString());
            }
            return null;
        }

        class Subreddit
        {
            public string Name { get; set; }
            public int Readers { get; set; }
            public bool NSFW { get; set; }
        }

        static Subreddit GetSubreddit(string subreddit)
        {
            try
            {
                WebClient wc = new WebClient();
                StreamReader sr = new StreamReader(wc.OpenRead("http://reddit.com/r/" + subreddit + "/about.json"));
                string data = sr.ReadToEnd();
                sr.Close();
                JObject json = JObject.Parse(data);
                Subreddit reddit = new Subreddit();
                reddit.Readers = json["data"]["subscribers"].Value<int>();
                reddit.Name = json["data"]["display_name"].Value<string>();
                reddit.NSFW = json["data"]["over18"].Value<bool>();
                return reddit;
            }
            catch { }
            return null;
        }

        static List<string> DoGoogleSearch(string terms)
        {
            List<string> results = new List<string>();
            try
            {
                WebClient client = new WebClient();
                StreamReader sr = new StreamReader(client.OpenRead("http://ajax.googleapis.com/ajax/services/search/web?v=1.0&q=" + Uri.EscapeUriString(terms)));
                string json = sr.ReadToEnd();
                sr.Close();
                JObject jobject = JObject.Parse(json);
                foreach (var result in jobject["responseData"]["results"])
                    results.Add(WebUtility.HtmlDecode(HtmlRemoval.StripTagsRegexCompiled(Uri.UnescapeDataString(result["title"].Value<string>())) +
                                " " + Uri.UnescapeDataString(result["url"].Value<string>())));
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                {
                    results.Add(e.GetType().Name + ": " + e.Message);
                    return results;
                }
            }
            return results;
        }

        #endregion

        #region Main

        static StreamWriter LogWriter;
        static JeBotMain JeBot;
        static XDocument Settings;
        static void Main(string[] args)
        {
            LogWriter = new StreamWriter("log.txt", true);
            Settings = XDocument.Load("config.xml");
            string address = Settings.Root.Element("server").Attribute("address").Value;
            IrcUser user = new IrcUser(
                Settings.Root.Element("server").Element("user").Element("nick").Value,
                Settings.Root.Element("server").Element("user").Element("user").Value,
                Settings.Root.Element("server").Element("user").Element("password").Value);
            JeBot = new JeBotMain(address, user);
            foreach (var manager in Settings.Root.Element("server").Element("managers").Elements("manager"))
                JeBot.Managers.Add(manager.Attribute("name").Value);
            JeBot.ConnectionComplete += new EventHandler(jebot_ConnectionComplete);
            JeBot.RawMessageRecieved += new EventHandler<RawMessageEventArgs>(JeBot_RawMessageRecieved);
            JeBot.RawMessageSent += new EventHandler<RawMessageEventArgs>(JeBot_RawMessageRecieved);
            JeBot.Run();
            while (true) Thread.Sleep(1);
        }

        static void JeBot_RawMessageRecieved(object sender, RawMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
            LogWriter.WriteLine(e.Message);
            LogWriter.Flush();
        }

        static void jebot_ConnectionComplete(object sender, EventArgs e)
        {
            foreach (var channel in Settings.Root.Element("server").Element("channels").Elements("channel"))
                JeBot.JoinChannel(channel.Attribute("name").Value);
        }

        #endregion
    }

    #region Utility Classes

    public static class TimeSpanExtensions
    {
        public static int GetYears(this TimeSpan timespan)
        {
            return (int)((double)timespan.Days / 365.2425);
        }
        public static int GetMonths(this TimeSpan timespan)
        {
            return (int)((double)timespan.Days / 30.436875);
        }
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
        public static string StripIrcColors(this string value)
        {
            string result = "";
            value = value.Trim().Replace("\u00031\u000315", "");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\u0003')
                    i++;
                else if (value[i] == '\u000f') ;
                else
                    result += value[i];
            }
            return result;
        }
    }

    public static class HtmlRemoval
    {
        /// <summary>
        /// Remove HTML from string with Regex.
        /// </summary>
        public static string StripTagsRegex(string source)
        {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }

        /// <summary>
        /// Compiled regular expression for performance.
        /// </summary>
        static Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

        /// <summary>
        /// Remove HTML from string with compiled Regex.
        /// </summary>
        public static string StripTagsRegexCompiled(string source)
        {
            return _htmlRegex.Replace(source, string.Empty);
        }

        /// <summary>
        /// Remove HTML tags from string using char array.
        /// </summary>
        public static string StripTagsCharArray(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }
    }

#endregion
}
