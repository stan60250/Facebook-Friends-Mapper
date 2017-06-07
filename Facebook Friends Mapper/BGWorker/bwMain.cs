using Facebook_Friends_Mapper.Classes;
using Facebook_Friends_Mapper.Manager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.BGWorker
{
    class bwMain
    {
        private targetFBUser TGTfbUser;

        private string uid = "";
        private string debugInfo = "";
        private bool flagCanceled = false;
        private string progressMsg = "";
        private int progressInt = 0;
        private int layer = 1;
        private bool flagFinished = false;

        //Precision and Recall
        public PrecisionRecall Precision_Recall = new PrecisionRecall();

        public bool flagMutualFriends = true;

        private BackgroundWorker _bw = new BackgroundWorker();

        public bwMain(string uid, int layer, string debugInfo, HashSet<String> answer = null)
        {
            this.uid = uid;
            this.layer = layer;

            _bw.DoWork += bw_DoWork;
            _bw.ProgressChanged += bw_ProgressChanged;

            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            TGTfbUser = new targetFBUser(uid, FBCrawler.getFBNameByUid(uid));
            if (answer != null)
                TGTfbUser.answerFriendList = answer;
        }

        public void Run()
        {
            _bw.RunWorkerAsync(uid);
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            _bw.ReportProgress(0, "init...");


            //宣告
            Dictionary<fbUser, int?> tmp_FriendList = new Dictionary<fbUser, int?>();
            //HashSet<fbUser> tmp_FriendList = new HashSet<fbUser>();
            int p = 0;
            string profile_id = e.Argument as string;

            //初始化目標使用者的Class
            //TGTfbUser = new targetFBUser(profile_id, FBCrawler.getFBNameByUid(profile_id)); 

            //找出為目標User的CoverPhoto點讚的User集合
            _bw.ReportProgress(0, "Getting Entry Friend List...");

            HashSet<fbUser> userWhoLikesCoverPhoto = FBCrawler.getEntryFriendList(profile_id);

            //反查, 並初始化目標的好友清單集合
            foreach (fbUser user in userWhoLikesCoverPhoto)
            {
                p++;
                _bw.ReportProgress(0, "Checking Friends [" + p + " / " + userWhoLikesCoverPhoto.Count + "] - " + user.profile_id);

                if (!LimitManager.isVaild())
                {
                    _bw.ReportProgress(0, "次數已達上限");
                    break;
                }

                //排除目標自己按讚
                if (user.profile_id.Equals(profile_id)) continue;

                //加入處裡順序
                if (!TGTfbUser.processList.Contains(user.profile_id))
                    TGTfbUser.processList.Add(user.profile_id);

                //加入統計
                if (!TGTfbUser.dataList.Contains(user.profile_id))
                    TGTfbUser.dataList.Add(user.profile_id, countHasFriend(user.profile_id, TGTfbUser.checkedFriendList) + " / " + TGTfbUser.checkedFriendList.Count.ToString());

                user.friends = FBCrawler.getFriendList(user.profile_id);
                user.friends_isCached = (user.friends.Count > 0) ? true : false;
                user.friends_isPublic = (user.friends.Count > 0) ? true : false;
                user.distance = 1;

                //加入Precision and recall 
                if (TGTfbUser.answerFriendList.Count > 0)
                    Precision_Recall.addPrecisionRecallItem(LimitManager.Count.getFriendList, user, TGTfbUser.answerFriendList, (user.friends.Count > 0));

                if (user.friends_isPublic)
                {
                    //好友名單可見, 反查目標
                    if (user.hasFriend(profile_id))
                    {
                        //有, 加入[確定清單]
                        TGTfbUser.checkedFriendList.Add(user);
                        //TGTfbUser.dataList.Add(user.profile_id, "-" );
                    }
                    else
                    {
                        //沒有, 丟棄
                        TGTfbUser.dropFriendList.Add(user);
                        //TGTfbUser.dataList.Add(user.profile_id, "-");
                        continue;

                        //沒有, 但是很可能認識目標, 加入[可能清單]
                        //過濾重複
                        /*if (TGTfbUser.IsInFriendList(user.profile_id, TGTfbUser.possibleFriendList)) continue;
                        TGTfbUser.possibleFriendList.Add(user);*/

                    }
                }
                else
                {
                    //好友名單不可見, 加入[可能清單], 這些人機率很大
                    //過濾重複
                    if (TGTfbUser.IsInFriendList(user.profile_id, TGTfbUser.possibleFriendList)) continue;
                    TGTfbUser.possibleFriendList.Add(user);

                    TGTfbUser.possibleFriendListScore.Add(user.profile_id, "1");
                    // [加入時該User的層數](1>2>3...) | 
                }
            }

            //新增方法 - 分別比對第一層確認的人之好友名單
            //確認"確定是好友的User"是有結果的
            if (TGTfbUser.checkedFriendList.Count > 0 && flagMutualFriends)
            {
                HashSet<fbUser> userInMutualFriends = new HashSet<fbUser>();
                HashSet<string> uidInMutualFriends = new HashSet<string>();
                HashSet<string> uidUsedList = new HashSet<string>();

                p = 0;
                foreach (fbUser user in TGTfbUser.checkedFriendList)
                {

                    p++;
                    _bw.ReportProgress(0, "Checking Mutual Friends [" + p + " / " + TGTfbUser.checkedFriendList.Count + "] - " + user.profile_id);
                    
                    if ((!user.profile_id.Equals(profile_id)) && (!uidUsedList.Contains(user.profile_id)))
                    {
                        HashSet<fbUser> tmp = FBCrawler.getMutualFriendList(profile_id, user.profile_id);
                        if (tmp.Count == 0)
                            continue;
                        foreach (fbUser new_user in tmp)
                        {
                            if (TGTfbUser.IsInFriendList(new_user.profile_id, userInMutualFriends)) continue;
                            if (uidInMutualFriends.Contains(new_user.profile_id)) continue;

                            //加入處裡順序
                            if (!TGTfbUser.processList.Contains(new_user.profile_id))
                                TGTfbUser.processList.Add(new_user.profile_id);

                            new_user.friends = FBCrawler.getFriendList(user.profile_id);
                            new_user.friends_isCached = (user.friends.Count > 0) ? true : false;
                            new_user.friends_isPublic = (user.friends.Count > 0) ? true : false;
                            new_user.distance = 1;

                            userInMutualFriends.Add(new_user);
                            uidInMutualFriends.Add(new_user.profile_id);
                        }
                        uidUsedList.Add(user.profile_id);
                        _bw.ReportProgress(0, "Checking Mutual Friends [" + p + " / " + TGTfbUser.checkedFriendList.Count + "] - " + user.profile_id + " (" + userInMutualFriends.Count + ")");
                        System.Threading.Thread.Sleep(3000);
                    }
                }

                foreach (fbUser user in userInMutualFriends)
                {
                    if (user.profile_id.Equals(profile_id)) continue;
                    //已經在目標的確定好友名單集合中
                    if (TGTfbUser.IsInFriendList(user.profile_id, TGTfbUser.checkedFriendList)) continue;
                    TGTfbUser.possibleFriendList.RemoveWhere(u => u.profile_id.Equals(user.profile_id));

                    //加入統計
                    if (!TGTfbUser.dataList.Contains(user.profile_id))
                        TGTfbUser.dataList.Add(user.profile_id, countHasFriend(user.profile_id, TGTfbUser.checkedFriendList) + " / " + TGTfbUser.checkedFriendList.Count.ToString());

                    TGTfbUser.checkedFriendList.Add(user);
                }
                
            }

            //確認"確定是好友的User"是有結果的
            if (TGTfbUser.checkedFriendList.Count > 0)
            {
                int loop = 1;
                while (loop <= layer || FBCrawler.flag_cancel)
                {

                    //第一層
                    if (loop == 1)
                    {
                        //逐一檢查"確定是好友的User"的好友名單
                        p = 0;
                        foreach (fbUser user in TGTfbUser.checkedFriendList)
                        {
                            p++;
                            _bw.ReportProgress(0, "逐一檢查與目標確定是好友的User的好友名單 (loop:" + loop.ToString() + ")[" + p + "/" + TGTfbUser.checkedFriendList.Count + "] - " + user.profile_id);

                            if (!LimitManager.isVaild())
                            {
                                _bw.ReportProgress(0, "次數已達上限");
                                tmp_FriendList.Clear();
                                break;
                            }

                            //排除目標
                            if (user.profile_id.Equals(profile_id)) continue;

                            //加入處裡順序
                            if (!TGTfbUser.processList.Contains(user.profile_id))
                                TGTfbUser.processList.Add(user.profile_id);

                            user.friends = FBCrawler.getFriendList(user.profile_id);
                            user.friends_isCached = (user.friends.Count > 0) ? true : false;
                            user.friends_isPublic = (user.friends.Count > 0) ? true : false;

                            foreach (String uid in user.friends)
                            {
                                //排除目標
                                if (uid.Equals(profile_id)) continue;
                                //已經在目標的確定好友名單集合中
                                if (TGTfbUser.IsInFriendList(uid, TGTfbUser.checkedFriendList)) continue;
                                //已經在暫存好友名單集合中
                                if (TGTfbUser.IsInFriendList(uid, new HashSet<fbUser>(tmp_FriendList.Keys))) continue;

                                //加入暫存好友名單集合, 並計算該使用者在"確定是好友的User"好友名單中的次數
                                tmp_FriendList.Add(new fbUser(uid, FBCrawler.getFBNameByUid(uid), loop), countHasFriend(uid, TGTfbUser.checkedFriendList));
                            }
                        }
                        //排序
                        tmp_FriendList = tmp_FriendList.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                    }
                    //第一層以上
                    else if (loop > 1)
                    {
                        //逐一檢查在暫存好友名單集合內的好友
                        p = 0;
                        while (tmp_FriendList.Count > 0 || FBCrawler.flag_cancel)
                        {

                            if (!LimitManager.isVaild())
                            {
                                _bw.ReportProgress(0, "次數已達上限");
                                tmp_FriendList.Clear();
                                break;
                            }

                            fbUser user = tmp_FriendList.Keys.ElementAt(0); //取得這次檢查的人
                            //加入處裡順序
                            if (!TGTfbUser.processList.Contains(user.profile_id))
                                TGTfbUser.processList.Add(user.profile_id);

                            if (!TGTfbUser.dataList.Contains(user.profile_id))
                                TGTfbUser.dataList.Add(user.profile_id, tmp_FriendList[user].Value.ToString() + " / " + TGTfbUser.checkedFriendList.Count.ToString());

                            //加入Precision and recall 
                            if (TGTfbUser.answerFriendList.Count > 0)
                                Precision_Recall.addPrecisionRecallItem(LimitManager.Count.getFriendList, user, TGTfbUser.answerFriendList, (user.friends.Count > 0));

                            tmp_FriendList.Remove(tmp_FriendList.Keys.ElementAt(0)); //移除這次檢查的人

                            p++;
                            _bw.ReportProgress(0, "逐一檢查在暫存好友名單集合內的好友 (loop:" + loop.ToString() + ")[" + p + "/" + (tmp_FriendList.Count + p - 1) + "] - " + user.profile_id);

                            //排除目標
                            if (user.profile_id.Equals(profile_id)) continue;

                            user.friends = FBCrawler.getFriendList(user.profile_id);
                            user.friends_isCached = (user.friends.Count > 0) ? true : false;
                            user.friends_isPublic = (user.friends.Count > 0) ? true : false;

                            if (user.friends_isPublic)
                            {
                                //好友名單可見, 反查目標
                                if (user.hasFriend(profile_id))
                                {
                                    //有, 加入[確定清單]
                                    //檢查重複
                                    if (!TGTfbUser.IsInFriendList(user.profile_id, TGTfbUser.checkedFriendList))
                                    {
                                        user.distance = 1;
                                        TGTfbUser.checkedFriendList.Add(user);

                                        //新增方法 - 分別比對第一層確認的人之好友名單
                                        if (flagMutualFriends)
                                        {
                                            HashSet<fbUser> mlist = FBCrawler.getMutualFriendList(profile_id, user.profile_id);
                                            if (mlist.Count > 0)
                                            {
                                                foreach (fbUser new_user in mlist)
                                                {
                                                    if (new_user.profile_id.Equals(profile_id)) continue;
                                                    if (TGTfbUser.IsInFriendList(new_user.profile_id, TGTfbUser.checkedFriendList)) continue;

                                                    TGTfbUser.possibleFriendList.RemoveWhere(u => u.profile_id.Equals(new_user.profile_id));

                                                    //加入處裡順序
                                                    if (!TGTfbUser.processList.Contains(new_user.profile_id))
                                                        TGTfbUser.processList.Add(new_user.profile_id);

                                                    //加入統計
                                                    if (!TGTfbUser.dataList.Contains(user.profile_id))
                                                        TGTfbUser.dataList.Add(user.profile_id, countHasFriend(user.profile_id, TGTfbUser.checkedFriendList) + " / " + TGTfbUser.checkedFriendList.Count.ToString());

                                                    new_user.friends = FBCrawler.getFriendList(user.profile_id);
                                                    new_user.friends_isCached = (user.friends.Count > 0) ? true : false;
                                                    new_user.friends_isPublic = (user.friends.Count > 0) ? true : false;
                                                    new_user.distance = 1;

                                                    TGTfbUser.checkedFriendList.Add(new_user);
                                                }
                                            }
                                        }

                                        //已確認的好友名單有變動, 重新計算並排序
                                        foreach (fbUser tmp in new HashSet<fbUser>(tmp_FriendList.Keys))
                                        {
                                            if (user.hasFriend(tmp.profile_id))
                                            {
                                                tmp_FriendList[tmp] += 1;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    //沒有, 丟棄
                                    TGTfbUser.dropFriendList.Add(user);
                                    continue;

                                    //沒有, 檢查層數限制, 加入擴散對象 或 丟棄
                                    /*if ((user.distance + 1) >= layer)
                                    {
                                        //層數限制, 丟棄
                                        continue;
                                    }
                                    else
                                    {
                                        //加入擴散對象
                                        user.distance += 1;
                                        if (!TGTfbUser.IsInFriendList(user.profile_id, new HashSet<fbUser>(tmp_FriendList.Keys)))
                                        {
                                            tmp_FriendList.Add(user, calcPoint(user.profile_id, TGTfbUser.checkedFriendList));
                                        }
                                    }*/
                                }
                            }
                            else
                            {
                                //好友名單不可見, 加入[可能清單], 這些人機率不高
                                user.distance += 1;
                                if (TGTfbUser.IsInFriendList(user.profile_id, TGTfbUser.possibleFriendList)) continue;
                                TGTfbUser.possibleFriendList.Add(user);
                                TGTfbUser.possibleFriendListScore.Add(user.profile_id, loop.ToString());
                            }

                            //重新排序
                            tmp_FriendList = tmp_FriendList.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

                        }
                    }

                    loop++;
                }
            }
            else
            {
                //沒有結果...死路!
            }

            p = 0;
            foreach (fbUser user in TGTfbUser.possibleFriendList)
            {
                p++;
                _bw.ReportProgress(0, "Calc F's FList Score[" + p + "/" + TGTfbUser.possibleFriendList.Count + "] - " + user.profile_id);
                try
                {
                    if (TGTfbUser.possibleFriendListScore[user.profile_id] == null)
                    {
                        TGTfbUser.possibleFriendListScore.Add(user.profile_id, calcScore(user.profile_id, -1, TGTfbUser.checkedFriendList));
                    }
                    else
                    {
                        string tmp = TGTfbUser.possibleFriendListScore[user.profile_id].ToString();
                        string score = calcScore(user.profile_id, Int16.Parse(tmp), TGTfbUser.checkedFriendList);
                        TGTfbUser.possibleFriendListScore[user.profile_id] = score;
                        TGTfbUser.possibleFriendSortedList.Add(user, (Int16.Parse(tmp) > 1 ? Int16.Parse(score.Substring(score.IndexOf('|') + 1)) : 100));
                    }
                }
                catch (Exception) { }
            }

            _bw.ReportProgress(50, "Sorting Possible Friend List...");
            //排序
            TGTfbUser.possibleFriendSortedList = TGTfbUser.possibleFriendSortedList.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            _bw.ReportProgress(100, "Finished!!");
            flagFinished = true;
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressMsg = e.UserState as String;
            progressInt = e.ProgressPercentage;
            DebugManager.add(DEBUG_LEVEL.DEBUG, String.Format("[Main][{0}] {1} - {2}%", debugInfo, e.UserState as String, e.ProgressPercentage.ToString()));
        }

        //計算該使用者在"確定是好友的User"好友名單中的次數
        private int countHasFriend(String uid, HashSet<fbUser> list)
        {
            int count = 0;
            foreach (fbUser user in list)
            {
                if (user.hasFriend(uid)) count++;
            }
            return count;
        }

        private int countHasFriend(String uid, HashSet<string> list)
        {
            int count = 0;
            foreach (string userID in list)
            {
                fbUser user = FBCrawler.FBCache.getUser(userID);
                if (user.hasFriend(uid)) count++;
            }
            return count;
        }

        private string printHasFriend(String uid, HashSet<string> list)
        {
            HashSet<string> fList = new HashSet<string>();
            foreach (string userID in list)
            {
                fbUser user = FBCrawler.FBCache.getUser(userID);
                if (user.hasFriend(uid))
                {
                    fList.Add(user.profile_id);
                    continue;
                }
            }

            return fList.Count.ToString();

            /*String[] tmpArray = new String[fList.Count];
            fList.CopyTo(tmpArray);

            return fList.Count + ((fList.Count > 0) ? ("[" + string.Join("|", tmpArray) + "]") : "")*/
            ;
        }

        //計算可能性分數(詳細資訊)
        private string calcScore(string profile_id, int Layer, HashSet<fbUser> list)
        {
            // [加入時該User的層數](1>2>3...) | 
            // [確定名單內的人好友名單之中出現次數](...3>2>1>0) | [不在確定名單但是在第一層名單內的人好友名單之中出現次數](...3>2>1>0) |
            // [在第一層但在丟棄名單內的人好友名單之中出現次數](0>1>2>3...) | [丟棄名單內的人好友名單之中出現次數](0>1>2>3...)

            return Layer + " | " + countHasFriend(profile_id, list);

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

        public targetFBUser getResult()
        {
            return TGTfbUser;
        }

        public mapResult getMapResult()
        {
            mapResult result = new mapResult();

            long var_correct = this.TGTfbUser.checkedFriendList.Count;
            long var_incorrect = this.TGTfbUser.dropFriendList.Count;
            long var_notsure = this.TGTfbUser.possibleFriendList.Count;
            long var_notsure_correct = 0;
            long var_notsure_incorrect = 0;

            foreach(fbUser user in this.TGTfbUser.possibleFriendList)
            {
                if(this.TGTfbUser.answerFriendList.Contains(user.profile_id))
                {
                    var_correct++;
                    var_notsure_correct++;
                }
                else
                {
                    var_incorrect++;
                    var_notsure_incorrect++;
                }
            }

            result.Save(
                this.TGTfbUser,
                this.Precision_Recall,
                LimitManager.Count.getFriendList,
                LimitManager.Count.getFB,
                var_correct,
                var_incorrect,
                var_notsure,
                var_notsure_correct,
                var_notsure_incorrect,
                (this.TGTfbUser.answerFriendList.Count == 0) ? -1 : this.TGTfbUser.answerFriendList.Count,
                -1
            );

            return result;
        }
    }
}
