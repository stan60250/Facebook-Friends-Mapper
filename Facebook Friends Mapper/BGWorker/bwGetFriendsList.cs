using Facebook_Friends_Mapper.Classes;
using Facebook_Friends_Mapper.Manager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Facebook_Friends_Mapper.BGWorker
{
    class bwGetFriendsList
    {
        private string debugInfo = "";
        private HashSet<String> result;
        private bool flagCanceled = false;
        private string progressMsg = "";
        private int progressInt = 0;
        private bool flagFinished = false;

        private BackgroundWorker _bw = new BackgroundWorker();

        public bwGetFriendsList(string uid, string debugInfo)
        {
            _bw.DoWork += bw_DoWork;
            _bw.ProgressChanged += bw_ProgressChanged;
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;
            _bw.RunWorkerAsync(uid);
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            _bw.ReportProgress(0, "init...");

            string profile_id = e.Argument as string;
            result = new HashSet<String>();

            _bw.ReportProgress(0, "getFriendList UID:" + e.Argument);

            //Search Cache first
            if (FBCrawler.flag_useChache)
            {
                if (FBCrawler.FBCache.hasUser(profile_id))
                {
                    fbUser tmp = new fbUser();
                    tmp = FBCrawler.FBCache.getUser(profile_id);
                    if (tmp.friends_isCached)
                    {
                        FBCrawler.chacheAccessCount++;
                        _bw.ReportProgress(100, "getFriendList output from Cache");
                        result = new HashSet<String>(tmp.friends);
                        flagFinished = true;
                        return;
                    }
                }
            }

            int p = 0;
            string suffix = "";
            string code = "";
            do
            {
                p++;
                _bw.ReportProgress(50, "getFriendList page: " + p.ToString());

                Regex rgx = new Regex(@"[0-9]{8,20}");
                code = FBCrawler.getFB(
                    rgx.IsMatch(profile_id) ?
                    String.Format("https://m.facebook.com/profile.php?v=friends&id={0}" + ((suffix.Length > 0 && suffix.IndexOf("startindex") > 0) ? "&" + suffix.Substring(suffix.IndexOf("startindex")) : ""), profile_id) :
                    String.Format("https://m.facebook.com/{0}/friends" + ((suffix.Length > 0 && suffix.IndexOf("startindex") > 0) ? "?" + suffix.Substring(suffix.IndexOf("startindex")) : ""), profile_id)
                    );
                suffix = "";

                HashSet<String> friends = FBCrawler.findFriendsInCode(code);
                result.UnionWith(friends);

                suffix = FBCrawler.getStringBySplitText(FBCrawler.getHtmlByInnerText(code, "See More Friends", 2), '"');

            }
            while (suffix.Length > 0);

            //Save to Cache
            fbUser user = new fbUser(profile_id, FBCrawler.getFBNameByUid(profile_id));
            user.friends_isCached = true;
            user.friends_isPublic = (result.Count > 0);
            user.friends.UnionWith(result);
            FBCrawler.FBCache.addUser(user);
            _bw.ReportProgress(100, "getFriendList save to Cache UID:" + profile_id);
            FBCrawler.FBCache.addMultiUsers(result);
            _bw.ReportProgress(100, "getFriendList save to Cache UID:" + profile_id + "'s friends(" + result.Count + ")");

            flagFinished = true;
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressMsg = e.UserState as String;
            progressInt = e.ProgressPercentage;
            DebugManager.add(DEBUG_LEVEL.DEBUG, String.Format("[ BW GetFriends][{0}] {1} - {2}%", debugInfo, e.UserState as String, e.ProgressPercentage.ToString()));
        }

        public bool isCanceled()
        {
            return flagCanceled;
        }

        public bool isFinished()
        {
            return flagFinished;
        }

        public void Cancel()
        {
            flagCanceled = true;
            this._bw.WorkerReportsProgress = false;
            this._bw.CancelAsync();
            this._bw.Dispose();
        }

        public String getProgressMsg()
        {
            return progressMsg;
        }

        public int getProgressInt()
        {
            return progressInt;
        }

        public HashSet<String> getResult()
        {
            return result;
        }
    }
}
