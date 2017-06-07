using Facebook_Friends_Mapper.Classes;
using Facebook_Friends_Mapper.Manager;
using HtmlAgilityPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Facebook_Friends_Mapper
{
    public static class FBCrawler
    {
        //儲存Cookies
        public static CookieCollection cookies;
        public static CookieContainer cookieJar;
        public static String cookiejarFilePath = "";
        public static String cacheFilePath = "";

        //快取
        public static fbCache FBCache = new fbCache();

        //Flag
        public static bool flag_cancel = false;
        public static bool flag_useChache = true;
        public static int fbAccessCount = 0;
        public static int fbAccessTotal = 0;
        public static int chacheAccessCount = 0;
        public static int chacheAccessTotal = 0;
        public static bool flag_newChache = false;


        //========================
        //       共用方法
        //========================

        #region Facebook 處理函數

        //登入Facebook
        public static bool loginFB(string ID, string PW)
        {
            DebugManager.add(DEBUG_LEVEL.DEBUG, "loginFB ID:" + ID);

            cookies = new CookieCollection();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://m.facebook.com"); fbAccessCount++;
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(cookies);
            //Get the response from the server and save the cookies from the first request..
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            cookies = response.Cookies;
            string getUrl = "https://m.facebook.com/login.php?login_attempt=1";
            string postData = String.Format("email={0}&pass={1}", ID, PW);
            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(getUrl); fbAccessCount++;
            getRequest.CookieContainer = new CookieContainer();
            getRequest.CookieContainer.Add(cookies); //recover cookies First request
            cookieJar = getRequest.CookieContainer;

            getRequest.Method = WebRequestMethods.Http.Post;
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2"; //GC
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:43.0) Gecko/20100101 Firefox/43.0"; //FF
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:45.0) Gecko/20100101 Firefox/45.0"; //FF v45
            getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:46.0) Gecko/20100101 Firefox/46.0"; //FF v46
            getRequest.AllowWriteStreamBuffering = true;
            getRequest.ProtocolVersion = HttpVersion.Version11;
            getRequest.AllowAutoRedirect = true;
            getRequest.ContentType = "application/x-www-form-urlencoded";

            byte[] byteArray = Encoding.ASCII.GetBytes(postData);
            getRequest.ContentLength = byteArray.Length;
            Stream newStream = getRequest.GetRequestStream(); //open connection
            newStream.Write(byteArray, 0, byteArray.Length); // Send the data.
            newStream.Close();

            HttpWebResponse getResponse = (HttpWebResponse)getRequest.GetResponse();
            cookies.Add(getResponse.Cookies);
            using (StreamReader sr = new StreamReader(getResponse.GetResponseStream()))
            {
                string sourceCode = sr.ReadToEnd();
                return (sourceCode.IndexOf("textarea") > -1);
            }
        }

        //從Facebook取得網頁原始碼
        public static string getFB(string URL)
        {
            if (URL.IndexOf("&amp;") > -1) URL = URL.Replace(@"&amp;", @"&");
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getFB URL:" + URL);

            if (cookieJar == null) { DebugManager.add(DEBUG_LEVEL.ERROR, "Need to login!!"); return ""; }
            if (URL.StartsWith("/")) URL = @"https://m.facebook.com" + URL;
            HttpWebRequest getRequest = (HttpWebRequest)WebRequest.Create(URL); fbAccessCount++; LimitManager.Count.getFB++;
            getRequest.CookieContainer = cookieJar; //new CookieContainer();
            //getRequest.CookieContainer.Add(cookies);
            getRequest.Method = WebRequestMethods.Http.Get;
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2"; //GC
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:43.0) Gecko/20100101 Firefox/43.0"; //FF v43
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:44.0) Gecko/20100101 Firefox/44.0"; //FF v44
            //getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:45.0) Gecko/20100101 Firefox/45.0"; //FF v45
            getRequest.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:46.0) Gecko/20100101 Firefox/46.0"; //FF v46
            getRequest.AllowWriteStreamBuffering = true;
            getRequest.ProtocolVersion = HttpVersion.Version11;
            getRequest.AllowAutoRedirect = true;
            getRequest.ContentType = "application/x-www-form-urlencoded";

            try
            {
                HttpWebResponse getResponse = (HttpWebResponse)getRequest.GetResponse();
                using (StreamReader sr = new StreamReader(getResponse.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (WebException e)
            {
                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    DebugManager.add(DEBUG_LEVEL.ERROR, String.Format("Error code: {0}", httpResponse.StatusCode));
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        //return reader.ReadToEnd();
                        return "";
                    }
                }
            }
        }

        //取得目標帳號好友名單(Cached)
        public static HashSet<String> getFriendList(string profile_id)
        {
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getFriendList UID:" + profile_id);
            LimitManager.Count.getFriendList++;

            HashSet<String> result = new HashSet<String>();

            //Search Cache first
            if (flag_useChache)
            {
                if (FBCache.hasUser(profile_id))
                {
                    fbUser tmp = new fbUser();
                    tmp = FBCache.getUser(profile_id);
                    if (tmp.friends_isCached)
                    {
                        chacheAccessCount++;
                        DebugManager.add(DEBUG_LEVEL.DEBUG, "getFriendList output from Cache");
                        return tmp.friends;
                    }
                }
            }

            //<a href="/minsheng.lin/friends?all=1&amp;startindex=24"><span>See More Friends</span></a>
            string suffix = "";
            string code = "";
            do
            {

                //https://m.facebook.com/profile.php?v=friends&id=100002579614553
                Regex rgx = new Regex(@"[0-9]{8,20}");
                code = getFB(
                    rgx.IsMatch(profile_id) ?
                    String.Format("https://m.facebook.com/profile.php?v=friends&id={0}" + ((suffix.Length > 0 && suffix.IndexOf("startindex") > 0) ? "&" + suffix.Substring(suffix.IndexOf("startindex")) : ""), profile_id) :
                    String.Format("https://m.facebook.com/{0}/friends" + ((suffix.Length > 0 && suffix.IndexOf("startindex") > 0) ? "?" + suffix.Substring(suffix.IndexOf("startindex")) : ""), profile_id)
                    );
                suffix = "";

                HashSet<String> friends = findFriendsInCode(code);
                result.UnionWith(friends);

                suffix = getStringBySplitText(getHtmlByInnerText(code, "See More Friends", 2), '"');

            }
            while (suffix.Length > 0);

            //Save to Cache
            fbUser user = new fbUser(profile_id, getFBNameByUid(profile_id));
            user.friends_isCached = true;
            user.friends_isPublic = (result.Count > 0);
            user.friends.UnionWith(result);
            FBCache.addUser(user);
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getFriendList save to Cache UID:" + profile_id);
            FBCache.addMultiUsers(result);
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getFriendList save to Cache UID:" + profile_id + "'s friends(" + result.Count + ")");

            //Delay
            if (DelayManager.getFriendList > 0)
            {
                DebugManager.add(DEBUG_LEVEL.DEBUG, "getFriendList DELAY " + DelayManager.getFriendList.ToString() + " ms");
                System.Threading.Thread.Sleep((int)DelayManager.getFriendList);
            }

            flag_newChache = true;
            return result;
        }

        //取得目標帳號CoverPhoto
        public static HashSet<String> getCoverPhotoList(string profile_id)
        {
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getCoverPhotoList UID:" + profile_id);

            HashSet<String> result = new HashSet<String>();

            string code = "";

            //https://m.facebook.com/lialm.yen/photos
            //https://m.facebook.com/profile.php?v=photos&id=100002579614553

            Regex rgx = new Regex(@"[0-9]{8,20}");
            code = getFB(
                rgx.IsMatch(profile_id) ?
                String.Format("https://m.facebook.com/profile.php?v=photos&id={0}", profile_id) :
                String.Format("https://m.facebook.com/{0}/photos", profile_id)
                );

            if (code.IndexOf("Cover Photos") > -1)
            {
                code = getFB(getStringBySplitText(getHtmlByInnerText(code, "Cover Photos", 1), '"'));

                MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(code));
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.Load(ms, Encoding.UTF8);
                HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(".//*[@id='thumbnail_area']//a");

                foreach (HtmlNode node in nodes)
                {
                    try
                    {
                        string url = node.GetAttributeValue("href", "null");
                        if (!(String.IsNullOrEmpty(url) || url.Equals("null")))
                        {
                            result.Add(url);
                        }
                    }
                    catch (Exception) { continue; }
                }
            }
            else
            {
                DebugManager.add(DEBUG_LEVEL.INFO, "CoverPhoto Album NotFound");
                return result;
            }
            return result;
        }

        //取得目標帳號的 進入點 列表(Cached)
        public static HashSet<fbUser> getEntryFriendList(string profile_id)
        {
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getEntryFriendList UID:" + profile_id);

            HashSet<fbUser> result = new HashSet<fbUser>();
            HashSet<String> uid = new HashSet<String>();

            HashSet<String> coverPhotoList = getCoverPhotoList(profile_id);

            foreach (string coverPhotoURL in coverPhotoList)
            {
                string code = getFB(coverPhotoURL);
                string url = "";

                if (code.IndexOf("likes this.") > -1)
                {
                    //單人按讚
                    url = getStringBySplitText(getHtmlByInnerText(code, "likes this.", 2), '"');
                }
                else if (code.IndexOf("like this.") > -1)
                {
                    //多人按讚
                    url = getStringBySplitText(getHtmlByInnerText(code, "like this.", 2), '"');
                }
                else
                {
                    //沒人按讚
                    continue;
                }

                if (url.IndexOf("/likes/") > -1)
                {
                    //很多人按讚
                    HashSet<String> tmp = findFriendsInCoverPhotoLikes(url);
                    foreach (String user_id in tmp)
                    {
                        if (!uid.Contains(user_id))
                        {
                            uid.Add(user_id);
                            result.Add(new fbUser(user_id, getFBNameByUid(user_id)));
                        }
                    }
                }
                else
                {
                    //單人按讚
                    // /yungjui.chen.9?refid=13
                    url = url.Substring(0, code.IndexOf("?")).Replace("/", "");
                    if (!uid.Contains(url))
                    {
                        string name = getFBNameByUid(url);
                        if (!String.IsNullOrEmpty(name))
                        {
                            uid.Add(url);
                            result.Add(new fbUser(url, name));
                        }
                    }
                }
            }

            //Save to Cache
            FBCache.addMultiUsers(result);
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getEntryFriendList save to Cache UID:" + profile_id + "'s EntryFriendList(" + result.Count + ")");

            return result;
        }

        //取得目標帳號與公開好友名單使用者的共同好友
        public static HashSet<fbUser> getMutualFriendList(string profile_id, string public_uid)
        {
            DebugManager.add(DEBUG_LEVEL.DEBUG, "getMutualFriendList UID:" + profile_id + " PUID:" + public_uid);

            HashSet<fbUser> result = new HashSet<fbUser>();

            string code = "";

            //https://www.facebook.com/friendship/profile_id/public_uid/
            code = getFB(String.Format("https://www.facebook.com/friendship/{0}/{1}/", profile_id, public_uid));

            if (code.IndexOf("mutual friend") > -1)
            {
                code = getFB("https://www.facebook.com/" + getStringBySplitText(getHtmlByInnerText(code, "mutual friend", 1), '"', 5).Replace(@"&amp;", @"&"));
                HashSet<string> uids = findFriendsByURLInCode(code);
                if (uids.Count > 0)
                {
                    foreach(string uid in uids)
                    {
                        result.Add(new fbUser(uid, getFBNameByUid(uid, 3000)));
                    }
                }
            }
            else
            {
                DebugManager.add(DEBUG_LEVEL.INFO, "MutualFriend link NotFound");
                return result;
            }

            return result;
        }

        //將原始碼轉成好友名單(Cached)
        public static HashSet<String> findFriendsInCode(string code)
        {
            LimitManager.Count.findFriendsInCode++;
            HashSet<String> result = new HashSet<String>();

            MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(code));
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(ms, Encoding.UTF8);

            //XPath
            string[] xpath_pattern = {
                                         ".//*[@id='root']/div/div[2]/div[{0}]/table/tbody/tr/td[2]/a", //Page1
                                         ".//*[@id='root']/div/div[1]/div[{0}]/table/tbody/tr/td[2]/a", //Page2...
                                         ".//*[@id='root']/div/div/div[{0}]/table/tbody/tr/td[2]/a"};   //Last Page

            //XPath Choose
            int p = 0;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    p = i;
                    string tmp = doc.DocumentNode.SelectSingleNode(String.Format(xpath_pattern[i], i.ToString())).GetAttributeValue("href", "null");
                    break;
                }
                catch (Exception) { continue; }
            }

            for (int i = 1; i <= 50; i++)
            {
                try
                {
                    string link = doc.DocumentNode.SelectSingleNode(String.Format(xpath_pattern[p], i.ToString())).GetAttributeValue("href", "null");
                    string name = doc.DocumentNode.SelectSingleNode(String.Format(xpath_pattern[p], i.ToString())).InnerText;
                    if (link != "null")
                    {
                        string uid = link.Replace("/", "").Replace("profile.php?id=", "").Replace("&amp;fref=fr_tab\"", "").Replace("?fref=fr_tab\"", "").Replace("&amp;", "").Replace("?fref=fr_tab", "").Replace("fref=fr_tab", "").Replace("refid=17", "").Replace("&v=timeline", "").Replace("v=timeline", "").Replace("\\", "").Replace("\"", "");
                        FBCache.addUser(new fbUser(uid, name));
                        DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsInCode save to Cache UID:" + uid + " Name:" + name);
                        result.Add(uid);
                    }
                }
                catch (Exception) { break; }
            }

            DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsInCode result: " + result.Count);

            //Delay
            if (DelayManager.findFriendsInCode > 0)
            {
                DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsInCode DELAY " + DelayManager.findFriendsInCode.ToString() + " ms");
                System.Threading.Thread.Sleep((int)DelayManager.findFriendsInCode);
            }

            return result;
        }

        public static HashSet<String> findFriendsByURLInCode(string code)
        {
            //LimitManager.Count.findFriendsInCode++;
            HashSet<String> result = new HashSet<String>();

            foreach (string link in parseLinks(code))
            {
                if (link.IndexOf("hc_location=profile_browser")>-1)
                {
                    result.Add(link
                        .Replace(@"https://www.facebook.com/", @"")
                        .Replace(@"http://www.facebook.com/", @"")
                        .Replace(@"profile.php?id=", @"")
                        .Replace(@"&amp;", @"")
                        .Replace(@"fref=pb", @"")
                        .Replace(@"hc_location=profile_browser", @"")
                        .Replace(@"&", @"")
                        .Replace(@"?", @"")
                        );
                }
            }

            DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsByURLInCode result: " + result.Count);

            //Delay
            if (DelayManager.findFriendsInCode > 0)
            {
                DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsByURLInCode DELAY " + DelayManager.findFriendsInCode.ToString() + " ms");
                System.Threading.Thread.Sleep((int)DelayManager.findFriendsInCode);
            }

            return result;
        }

        //將對CoverPhoto點讚的人轉成好友名單
        public static HashSet<String> findFriendsInCoverPhotoLikes(string URL)
        {
            HashSet<String> result = new HashSet<String>();

            do
            {
                MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(getFB(URL)));
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.Load(ms, Encoding.UTF8);

                //XPath 
                for (int i = 1; i < 10; i++)
                {
                    try
                    {
                        string link = doc.DocumentNode.SelectSingleNode(".//*[@id='root']/table/tbody/tr/td/ul/li[" + i + "]/table/tbody/tr/td/table/tbody/tr/td[2]/div/h3[1]/a").GetAttributeValue("href", "null");
                        string name = doc.DocumentNode.SelectSingleNode(".//*[@id='root']/table/tbody/tr/td/ul/li[" + i + "]/table/tbody/tr/td/table/tbody/tr/td[2]/div/h3[1]/a").InnerText;
                        if (link != "null")
                        {
                            string uid = link.Replace("/", "").Replace("profile.php?id=", "").Replace("&amp;fref=fr_tab\"", "").Replace("?fref=fr_tab\"", "").Replace("&amp;", "").Replace("?fref=pb", "").Replace("fref=pb", "").Replace("refid=17", "").Replace("&v=timeline", "").Replace("v=timeline", "").Replace("\\", "").Replace("\"", "");
                            FBCache.addUser(new fbUser(uid, name));
                            DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsInCoverPhotoLikes save to Cache UID:" + uid + " Name:" + name);
                            result.Add(uid);
                        }
                    }
                    catch (Exception) { continue; }
                }

                try
                {
                    URL = doc.DocumentNode.SelectSingleNode(".//*[@id='root']/table/tbody/tr/td/div/a").GetAttributeValue("href", "");
                }
                catch (Exception) { URL = ""; }
            }
            while (URL.Length > 0);

            DebugManager.add(DEBUG_LEVEL.DEBUG, "findFriendsInCoverPhotoLikes result: " + result.Count);

            return result;
        }

        //透過UID/UserName取得User名稱(Cached)
        public static string getFBNameByUid(string profile_id, int delay = 0)
        {
            LimitManager.Count.getFBNameByUid++;
            //DebugManager.add(DEBUG_LEVEL.DEBUG, "getFBNameByUid UID:" + profile_id);

            //Search Cache first
            if (flag_useChache)
            {
                if (FBCache.hasUser(profile_id))
                {
                    fbUser tmp = FBCache.getUser(profile_id);
                    if (!String.IsNullOrEmpty(tmp.cover_name))
                    {
                        chacheAccessCount++;
                        //DebugManager.add(DEBUG_LEVEL.DEBUG, "getFBNameByUid output from Cache");
                        return tmp.cover_name;
                    }
                }
            }

            if (delay > 0)
            {
                System.Threading.Thread.Sleep(delay);
                DebugManager.add(DEBUG_LEVEL.DEBUG, "getFBNameByUid DELAY " + delay.ToString() + " ms");
            }

            string code = "";

            Regex rgx = new Regex(@"[0-9]{8,20}");
            code = getFB(
                rgx.IsMatch(profile_id) ?
                String.Format("https://m.facebook.com/profile.php?id={0}", profile_id) :
                String.Format("https://m.facebook.com/{0}", profile_id)
                );

            MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(code));
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(ms, Encoding.UTF8);

            //XPath
            string[] xpath_pattern = {
                                         ".//*[@id='root']/div/div[1]/div[2]/div/div[2]/span[1]/strong" //style 1
                                     };

            //XPath Choose
            for (int i = 0; i < xpath_pattern.Length; i++)
            {
                try
                {
                    string name = doc.DocumentNode.SelectSingleNode(xpath_pattern[i]).InnerText;
                    if (!String.IsNullOrEmpty(name))
                    {
                        //Save to Cache

                        FBCache.addUser(new fbUser(profile_id, name));
                        DebugManager.add(DEBUG_LEVEL.DEBUG, "getFBNameByUid save to Cache UID:" + profile_id);
                        return name;
                    }
                }
                catch (Exception) { continue; }
            }
            return "";
        }

        #endregion

        #region Cookies 處理函數
        public static void WriteCookiesToDisk(string file, CookieContainer cookieJar)
        {
            using (Stream stream = File.Create(file))
            {
                try
                {
                    Console.Out.Write("Writing cookies to disk... ");
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, cookieJar);
                    Console.Out.WriteLine("Done.");
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine("Problem writing cookies to disk: " + e.GetType());
                }
            }
        }

        public static CookieContainer ReadCookiesFromDisk(string file)
        {

            try
            {
                using (Stream stream = File.Open(file, FileMode.Open))
                {
                    Console.Out.Write("Reading cookies from disk... ");
                    BinaryFormatter formatter = new BinaryFormatter();
                    Console.Out.WriteLine("Done.");
                    return (CookieContainer)formatter.Deserialize(stream);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Problem reading cookies from disk: " + e.GetType());
                return new CookieContainer();
            }
        }

        public static CookieCollection GetAllCookies(CookieContainer cookieJar)
        {
            CookieCollection cookieCollection = new CookieCollection();
            Hashtable table = (Hashtable)cookieJar.GetType().InvokeMember("m_domainTable",
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.GetField |
                                                                            BindingFlags.Instance,
                                                                            null,
                                                                            cookieJar,
                                                                            new object[] { });
            foreach (var tableKey in table.Keys)
            {
                String str_tableKey = (string)tableKey;
                if (str_tableKey[0] == '.')
                {
                    str_tableKey = str_tableKey.Substring(1);
                }

                SortedList list = (SortedList)table[tableKey].GetType().InvokeMember("m_list",
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.GetField |
                                                                            BindingFlags.Instance,
                                                                            null,
                                                                            table[tableKey],
                                                                            new object[] { });
                foreach (var listKey in list.Keys)
                {
                    String url = "https://" + str_tableKey + (string)listKey;
                    cookieCollection.Add(cookieJar.GetCookies(new Uri(url)));
                }
            }
            return cookieCollection;
        }
        #endregion

        #region 字串處理 共用函數

        public static string getStringBySplitText(string text, char splitText, int index = 1)
        {
            string[] tmp = text.Split(splitText);
            if ((tmp.Length - 1) < index) { index = tmp.Length - 1; }
            if (index <= 0) { index = 0; }
            return tmp[index];
        }

        public static string getHtmlByInnerText(string code, string innerText, int layer = 1)
        {
            if (code.IndexOf(innerText) < 0) { return ""; }

            int lp = code.IndexOf(innerText);
            int rp = code.IndexOf(innerText) + innerText.Length - 1;

            while (layer > 0)
            {
                lp = code.Substring(0, lp).LastIndexOf('<');
                rp = code.IndexOf('>', rp + 1);
                layer -= 1;
            }
            return code.Substring(lp, rp - lp + 1);
        }

        public static string stringReverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
        public static List<string> parseLinks(string code)
        {
            List<string> result = new List<string>();
            MemoryStream ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(code));
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.Load(ms, Encoding.UTF8);
            foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                // Get the value of the HREF attribute
                string hrefValue = link.GetAttributeValue("href", string.Empty);
                result.Add(hrefValue);
            }
            return result;
        }
        #endregion

    }
}
