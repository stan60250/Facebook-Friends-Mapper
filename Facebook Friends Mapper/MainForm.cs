using Facebook_Friends_Mapper.BGWorker;
using Facebook_Friends_Mapper.Classes;
using Facebook_Friends_Mapper.Manager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Facebook_Friends_Mapper
{
    public partial class MainForm : Form
    {
        #region 宣告
        //========================
        //         宣告
        //========================

        //結果列表
        private Dictionary<String, mapResult> ResultList = new Dictionary<String, mapResult>();
        private Dictionary<String, mapResult> ResultListFromFile = new Dictionary<String, mapResult>();

        //GUI長寬
        private int oldWidth;
        private int oldHeight;

        //除錯訊息顯示
        public int debugMsgCount = 0;
        public int timerGUIConv = 0;

        //正在mapping的目標使用者輸出
        private bwMain main;

        private bool flag_cacheBusy = false;

        //自動儲存倒數
        private int cacheAutoSaveCountDown = 0;

        #endregion

        //表單初始化
        public MainForm()
        {
            InitializeComponent();

            //初始化WebManager
            FBCrawler.cookiejarFilePath = Application.StartupPath + @"\cookies.dat";
            FBCrawler.cacheFilePath = Application.StartupPath + @"\cache.xml";

            label_Msg.Text = "";
            label_result.Text = "沒有結果 No Result.";

            //格式化資料表
            formatTable2DetailList(OutputTable);
            formatTable2GraphList(GraphTable);

            //初始化表單長寬
            oldWidth = this.Width;
            oldHeight = this.Height;

            //偵測 CookieJar 內的 Cookies
            if (System.IO.File.Exists(FBCrawler.cookiejarFilePath))
            {
                FBCrawler.cookieJar = FBCrawler.ReadCookiesFromDisk(FBCrawler.cookiejarFilePath);
                button_Cookies.Text = "Clean Cookies";
                button_Cookies.Enabled = true;
                cBox_Cookies.Items.Add("Default");
            }
            else
            {
                button_Cookies.Text = "No Cookies";
            }
            checkCookieFileExist();
            if (!Directory.Exists(Application.StartupPath + @"\cookiejar\"))
                Directory.CreateDirectory(Application.StartupPath + @"\cookiejar\");
            foreach (string f in Directory.EnumerateFiles(Application.StartupPath + @"\cookiejar\", "*.dat", SearchOption.TopDirectoryOnly))
            {
                cBox_Cookies.Items.Add(Path.GetFileName(f).Replace(".dat", ""));
            }

            //讀取之前結果
            if (!Directory.Exists(Application.StartupPath + @"\result\"))
                Directory.CreateDirectory(Application.StartupPath + @"\result\");
            getResultListFromFile();
            updateResultList();

            //偵測Cache是否存在
            if (System.IO.File.Exists(FBCrawler.cacheFilePath))
            {
                button_LoadCache.Enabled = true;
                FBCrawler.FBCache.loadFromFile(FBCrawler.cacheFilePath);
            }
        }

        //表單改變大小
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (!(WindowState == FormWindowState.Minimized))
            {
                if (this.Width < 600) this.Width = 600;
                if (this.Height < 300) this.Height = 300;

                try
                {
                    //控制項Resize
                    tabControl2.Width = tabControl2.Width + (this.Width - oldWidth);
                    tabControl2.Height = tabControl2.Height + (this.Height - oldHeight);

                    linkLabel_result.Location = new Point(linkLabel_result.Location.X + (this.Width - oldWidth), linkLabel_result.Location.Y);

                    //label_Precision.Location = new Point(label_Precision.Location.X + (this.Width - oldWidth) / 2, label_Precision.Location.Y);
                    label_Recall.Location = new Point(label_Recall.Location.X + (this.Width - oldWidth) / 2, label_Recall.Location.Y + (this.Height - oldHeight));

                    groupBox_result.Location = new Point(groupBox_result.Location.X + (this.Width - oldWidth) / 2, groupBox_result.Location.Y + (this.Height - oldHeight) / 2);
                }
                catch (Exception) { }

                oldWidth = this.Width;
                oldHeight = this.Height;
            }
        }

        #region GUI 控制項方法
        //========================
        //     GUI 控制項方法
        //========================

        private void timer_GUI_Tick(object sender, EventArgs e)
        {
            //GUI 介面更新, 顯示存取FB次數
            if (DebugManager.messageList.Count > debugMsgCount)
            {
                for (int i = 0; i < (DebugManager.messageList.Count - debugMsgCount); i++)
                {
                    listBox_debug.Items.Add(String.Format("[{0}][{1}] {2}", DebugManager.messageList[i + debugMsgCount].level.ToString(), DebugManager.messageList[i + debugMsgCount].time.ToString(), DebugManager.messageList[i + debugMsgCount].message));
                    listBox_debug.SelectedIndex = listBox_debug.Items.Count - 1;
                }
                debugMsgCount = DebugManager.messageList.Count;
                Application.DoEvents();
            }

            if (timerGUIConv <= 0)
            {
                FBCrawler.fbAccessTotal += FBCrawler.fbAccessCount;
                FBCrawler.chacheAccessTotal += FBCrawler.chacheAccessCount;
                label_info.Text = "FB Access per sec: " + FBCrawler.fbAccessCount.ToString() + "\n"
                    + "FB Access Total: " + FBCrawler.fbAccessTotal.ToString() + "\n"
                    + "Cached Access count: " + FBCrawler.chacheAccessCount.ToString() + "\n"
                    + "Cached Access Total: " + FBCrawler.chacheAccessTotal.ToString() + "\n"
                    + "Cached Users count: " + FBCrawler.FBCache.getUserCount() + "\n";
                FBCrawler.fbAccessCount = 0;
                FBCrawler.chacheAccessCount = 0;

                label_Msg.Text = "getFB: " + LimitManager.Count.getFB.ToString() + " / " + ((LimitManager.Limit.getFB == -1) ? "∞" : LimitManager.Limit.getFB.ToString()) + "\n"
                    + "getFriendList: " + LimitManager.Count.getFriendList.ToString() + " / " + ((LimitManager.Limit.getFriendList == -1) ? "∞" : LimitManager.Limit.getFriendList.ToString()) + "\n"
                    + "findFriendsInCode: " + LimitManager.Count.findFriendsInCode.ToString() + " / " + ((LimitManager.Limit.findFriendsInCode == -1) ? "∞" : LimitManager.Limit.findFriendsInCode.ToString()) + "\n"
                    + "getFBNameByUid: " + LimitManager.Count.getFBNameByUid.ToString() + " / " + ((LimitManager.Limit.getFBNameByUid == -1) ? "∞" : LimitManager.Limit.getFBNameByUid.ToString());

                timerGUIConv = 1000 / timer_GUI.Interval;
                Application.DoEvents();
            }
            else
            {
                timerGUIConv--;
            }
        }

        private void timer_cacheAutoSave_Tick(object sender, EventArgs e)
        {
            if (flag_cacheBusy) return;
            if (cacheAutoSaveCountDown <= 0)
            {
                button_SaveCache.Enabled = false;
                button_LoadCache.Enabled = false;
                flag_cacheBusy = true;
                button_SaveCache.Text = "Saving...";
                Application.DoEvents();

                try
                {
                    FBCrawler.FBCache.saveToFile(FBCrawler.cacheFilePath);
                }
                catch (Exception ex)
                {
                    DebugManager.add(DEBUG_LEVEL.ERROR, ex.Message);
                    try
                    {
                        if (File.Exists(FBCrawler.cacheFilePath + ".tmp"))
                            File.Delete(FBCrawler.cacheFilePath + ".tmp");
                    }
                    catch (Exception) { }
                }
                finally
                {
                    button_SaveCache.Text = "Save Cache";
                    button_SaveCache.Enabled = true;
                    button_LoadCache.Enabled = true;
                    flag_cacheBusy = false;
                    Application.DoEvents();
                    cacheAutoSaveCountDown = (int)num_AutoSaveDelay.Value;
                }
            }
            else
            {
                button_SaveCache.Text = "Save Cache (" + cacheAutoSaveCountDown + ")";
                cacheAutoSaveCountDown--;
            }
            
        }

        private void button_Login_Click(object sender, EventArgs e)
        {
            if (FBCrawler.loginFB(textBox_FBID.Text, textBox_FBPW.Text))
            {
                button_Cookies.Text = "Save Cookies";
                button_Cookies.Enabled = true;
                DebugManager.add(DEBUG_LEVEL.INFO, "登入成功!");
                MessageBox.Show("登入成功!");
            }
            else
            {
                DebugManager.add(DEBUG_LEVEL.ERROR, "登入失敗!");
                MessageBox.Show("登入失敗!");
            }
        }

        private void button_LoadCookie_Click(object sender, EventArgs e)
        {
            if (System.IO.File.Exists(Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat"))
            {
                if (System.IO.File.Exists(FBCrawler.cookiejarFilePath))
                    System.IO.File.Delete(FBCrawler.cookiejarFilePath);
                System.IO.File.Copy(Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat", FBCrawler.cookiejarFilePath);
                MessageBox.Show(cBox_Cookies.Text + " Cookie Loaded!");
            }
            else
            {
                MessageBox.Show("File not exist!");
            }
        }

        private void button_SaveCookie_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(cBox_Cookies.Text))
            {
                MessageBox.Show("File Name is NULL!");
                return;
            }
            if (!System.IO.File.Exists(Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat"))
            {
                System.IO.File.Copy(FBCrawler.cookiejarFilePath, Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat");
                MessageBox.Show(cBox_Cookies.Text + " Cookie Saved!");
            }
            else
            {
                MessageBox.Show("File already exist!");
            }
        }

        private void button_Cookies_Click(object sender, EventArgs e)
        {
            if (System.IO.File.Exists(FBCrawler.cookiejarFilePath))
            {
                System.IO.File.Delete(FBCrawler.cookiejarFilePath);
            }
            if (button_Cookies.Text.Equals("Clean Cookies"))
            {
                button_Cookies.Text = "No Cookies";
                button_Cookies.Enabled = false;
            }
            else if (button_Cookies.Text.Equals("Save Cookies"))
            {
                FBCrawler.WriteCookiesToDisk(FBCrawler.cookiejarFilePath, FBCrawler.cookieJar);
                button_LoadCookie.Enabled = true;
                button_SaveCookie.Enabled = true;
            }
        }

        private void cBox_Cookies_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkCookieFileExist();
        }

        private void button_TestFriendList_Click(object sender, EventArgs e)
        {
            testFriendList(cBox_TestID.Text);
        }

        private void button_TestCoverPhoto_Click(object sender, EventArgs e)
        {
            groupBox_FList.Text = "CoverPhoto List (Loading...)";
            Application.DoEvents();

            HashSet<String> urls = FBCrawler.getCoverPhotoList(cBox_TestID.Text);

            formatTable2CoverPhotoList(OutputTable);

            int i = 0;
            foreach (String url in urls)
            {
                i++;
                Add2Table(OutputTable, new String[] { i.ToString(), url });
            }

            groupBox_FList.Text = FBCrawler.getFBNameByUid(cBox_TestID.Text) + "'s CoverPhoto List(" + i.ToString() + ")";
        }

        private void button_EntryList_Click(object sender, EventArgs e)
        {
            groupBox_FList.Text = "Entry List (Loading...)";
            Application.DoEvents();

            HashSet<fbUser> friends = FBCrawler.getEntryFriendList(cBox_TestID.Text);

            groupBox_FList.Text = FBCrawler.getFBNameByUid(cBox_TestID.Text) + "'s Entry List(" + friends.Count + ")";
            formatTable2DetailList(OutputTable);
            Application.DoEvents();

            int i = 0;
            foreach (fbUser user in friends)
            {
                i++;
                Add2Table(OutputTable, new String[] { i.ToString(), user.cover_name, user.profile_id });
            }
        }

        private void button_Reset_Click(object sender, EventArgs e)
        {
            LimitManager.Count.reset();
        }

        private void num_Limit_getFB_ValueChanged(object sender, EventArgs e)
        {
            LimitManager.Limit.getFB = (long)num_Limit_getFB.Value;
        }

        private void num_Limit_getFriendList_ValueChanged(object sender, EventArgs e)
        {
            LimitManager.Limit.getFriendList = (long)num_Limit_getFriendList.Value;
        }

        private void cBox_Chache_CheckedChanged(object sender, EventArgs e)
        {
            FBCrawler.flag_useChache = cBox_Chache.Checked;
        }

        private void button_SaveCache_Click(object sender, EventArgs e)
        {
            if (flag_cacheBusy) return;
            button_SaveCache.Enabled = false;
            button_LoadCache.Enabled = false;
            flag_cacheBusy = true;
            button_SaveCache.Text = "Saving...";
            Application.DoEvents();

            try
            {
                FBCrawler.FBCache.saveToFile(FBCrawler.cacheFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
            button_SaveCache.Text = "Save Cache";
            button_SaveCache.Enabled = true;
            button_LoadCache.Enabled = true;
            flag_cacheBusy = false;
            Application.DoEvents();
        }

        private void cBox_ChacheAutoSave_CheckedChanged(object sender, EventArgs e)
        {
            button_SaveCache.Text = "Save Cache";
            timer_cacheAutoSave.Enabled = cBox_ChacheAutoSave.Checked;
            cacheAutoSaveCountDown = (int)num_AutoSaveDelay.Value;
        }

        private void button_LoadCache_Click(object sender, EventArgs e)
        {
            if (flag_cacheBusy) return;
            button_LoadCache.Enabled = false;
            button_SaveCache.Enabled = false;
            flag_cacheBusy = true;
            button_LoadCache.Text = "Loading...";
            Application.DoEvents();
            FBCrawler.FBCache.loadFromFile(FBCrawler.cacheFilePath);
            button_LoadCache.Text = "Load Cache";
            button_LoadCache.Enabled = true;
            button_SaveCache.Enabled = true;
            flag_cacheBusy = false;
            Application.DoEvents();
        }

        private void button_FlushCache_Click(object sender, EventArgs e)
        {
            FBCrawler.FBCache.flush();
        }

        private void num_Delay_getFList_ValueChanged(object sender, EventArgs e)
        {
            DelayManager.getFriendList = (long)num_Delay_getFList.Value;
        }

        private void num_Delay_getFListPage_ValueChanged(object sender, EventArgs e)
        {
            DelayManager.findFriendsInCode = (long)num_Delay_getFListPage.Value;
        }

        private void button_Search_Click(object sender, EventArgs e)
        {
            searchCache();
        }

        //===OutputTable(ListView)排序===
        private int sortColumn = -1;

        private void OutputTable_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine whether the column is the same as the last column clicked.
            if (e.Column != sortColumn)
            {
                // Set the sort column to the new column.
                sortColumn = e.Column;
                // Set the sort order to ascending by default.
                OutputTable.Sorting = SortOrder.Ascending;
            }
            else
            {
                // Determine what the last sort order was and change it.
                if (OutputTable.Sorting == SortOrder.Ascending)
                    OutputTable.Sorting = SortOrder.Descending;
                else
                    OutputTable.Sorting = SortOrder.Ascending;
            }

            // Call the sort method to manually sort.
            OutputTable.Sort();
            // Set the ListViewItemSorter property to a new ListViewItemComparer
            // object.
            this.OutputTable.ListViewItemSorter = new ListViewItemComparer(e.Column, OutputTable.Sorting);
        }
        private class ListViewItemComparer : System.Collections.IComparer
        {
            private int col;
            private SortOrder order;
            public ListViewItemComparer()
            {
                col = 0;
                order = SortOrder.Ascending;
            }
            public ListViewItemComparer(int column, SortOrder order)
            {
                col = column;
                this.order = order;
            }
            public int Compare(object x, object y)
            {
                int returnVal = -1;
                returnVal = String.Compare(((ListViewItem)x).SubItems[col].Text,
                                ((ListViewItem)y).SubItems[col].Text);
                // Determine whether the sort order is descending.
                if (order == SortOrder.Descending)
                    // Invert the value returned by String.Compare.
                    returnVal *= -1;
                return returnVal;
            }
        }
        //===OutputTable(ListView)排序===

        private void OutputTable_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if ((!String.IsNullOrEmpty(OutputTable.SelectedItems[0].SubItems[2].Text)))
            {
                testFriendList(OutputTable.SelectedItems[0].SubItems[2].Text);
            }
        }

        private void cBox_ResultDisplay_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void cBox_IncludeDrop_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void cBox_UserMode_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void cBox_UserModeWithAnswer_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void rBtn_GraphTable_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void rBtn_GraphProcess_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void num_Graph_RecallRound_ValueChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void num_Graph_DataRound_ValueChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void cBox_GraphShowDetail_CheckedChanged(object sender, EventArgs e)
        {
            updateOutputByGUIControl();
        }

        private void updateOutputByGUIControl()
        {
            if (ResultList.Count > 0 && (!cBox_ResultDisplay.Text.Equals("(null)")) && (!String.IsNullOrEmpty(cBox_ResultDisplay.Text)) && ResultList[cBox_ResultDisplay.Text] != null)
                displayResult(ResultList[cBox_ResultDisplay.Text], true, cBox_IncludeDrop.Checked, !cBox_UserMode.Checked);
        }

        private void cBox_ResultDisplayUpdate(string item_name)
        {
            cBox_ResultDisplay.Items.Clear();
            foreach (string item in ResultList.Keys)
            {
                cBox_ResultDisplay.Items.Add(item);
            }
            cBox_ResultDisplay.Text = item_name;
        }

        private void button_ExportCSV_Click(object sender, EventArgs e)
        {
            string file = Application.StartupPath + @"\output.csv";
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
                ExportManager.ListViewToCSV(OutputTable, file, true);
                if (cBox_OpenNow.Checked)
                    System.Diagnostics.Process.Start(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button_ExportGraph_Click(object sender, EventArgs e)
        {
            string file = Application.StartupPath + @"\output.csv";
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
                ExportManager.ListViewToCSV(GraphTable, file, true);
                if (cBox_OpenNow.Checked)
                    System.Diagnostics.Process.Start(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void linkLabel_result_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (groupBox_result.Visible == false)
                groupBox_result.Visible = true;
        }

        private void label_close_Click(object sender, EventArgs e)
        {
            if (groupBox_result.Visible == true)
                groupBox_result.Visible = false;
        }

        #endregion

        #region GUI 共用函數/方法
        //========================
        //   GUI 共用函數/方法
        //========================

        //加入資料至Table
        private void Add2Table(ListView Table, string[] Text)
        {
            ListViewItem lvi = new ListViewItem(Text);
            Table.Items.Add(lvi);
        }

        //好友名單DataTable格式化
        private void formatTable2FriendList(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 30, HorizontalAlignment.Left);
            Table.Columns.Add("Name", 80, HorizontalAlignment.Left);
            Table.Columns.Add("UID/UName", 100, HorizontalAlignment.Left);
            Table.Refresh();
        }

        //封面照片DataTable格式化
        private void formatTable2CoverPhotoList(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 30, HorizontalAlignment.Left);
            Table.Columns.Add("URL", 1000, HorizontalAlignment.Left);
            Table.Refresh();
        }

        //詳細資料DataTable格式化
        private void formatTable2DetailList(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 35, HorizontalAlignment.Left);
            Table.Columns.Add("名稱", 100, HorizontalAlignment.Left);
            Table.Columns.Add("UserID", 105, HorizontalAlignment.Left);
            Table.Columns.Add("好友數", 45, HorizontalAlignment.Left);
            Table.Columns.Add("結果", 100, HorizontalAlignment.Left);
            Table.Columns.Add("與目標距離", 65, HorizontalAlignment.Left);
            Table.Columns.Add("處理順位", 60, HorizontalAlignment.Left);
            Table.Columns.Add("包含/總數", 60, HorizontalAlignment.Left);
            Table.Columns.Add("可能性分數", 350, HorizontalAlignment.Left);
            Table.Refresh();
        }

        //詳細資料DataTable格式化(使用者模式)
        private void formatTable2DetailUserList(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 35, HorizontalAlignment.Left);
            Table.Columns.Add("名稱", 100, HorizontalAlignment.Left);
            Table.Columns.Add("UserID", 105, HorizontalAlignment.Left);
            Table.Columns.Add("比對結果", 100, HorizontalAlignment.Left);
            Table.Refresh();
        }

        private void formatTable2DetailUserListWithAnswer(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 35, HorizontalAlignment.Left);
            Table.Columns.Add("名稱", 100, HorizontalAlignment.Left);
            Table.Columns.Add("UserID", 105, HorizontalAlignment.Left);
            Table.Columns.Add("比對結果", 100, HorizontalAlignment.Left);
            Table.Columns.Add("答案", 100, HorizontalAlignment.Left);
            Table.Refresh();
        }

        //圖表資料GraphTable格式化
        private void formatTable2GraphList(ListView Table)
        {
            Table.Clear();
            Table.View = View.Details;
            Table.GridLines = true;
            Table.Columns.Clear();
            Table.Columns.Add("#", 35, HorizontalAlignment.Left);
            Table.Columns.Add("Y:Precision", 100, HorizontalAlignment.Left);
            Table.Columns.Add("X:Recall", 100, HorizontalAlignment.Left);
            Table.Columns.Add("順序", 60, HorizontalAlignment.Left);
            Table.Columns.Add("正確", 60, HorizontalAlignment.Left);
            Table.Columns.Add("錯誤", 60, HorizontalAlignment.Left);
            //Table.Columns.Add("正確(對答案)", 80, HorizontalAlignment.Left);
            //Table.Columns.Add("錯誤(對答案)", 80, HorizontalAlignment.Left);
            Table.Refresh();
        }

        //檢查檔案快取
        private void checkCookieFileExist()
        {
            button_LoadCookie.Enabled = System.IO.File.Exists(Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat")
                || (cBox_Cookies.Text.Equals("Default") && System.IO.File.Exists(FBCrawler.cookiejarFilePath));
            button_SaveCookie.Enabled = (!String.IsNullOrEmpty(cBox_Cookies.Text))
                && (!System.IO.File.Exists(Application.StartupPath + @"\cookiejar\" + cBox_Cookies.Text + @".dat"))
                && System.IO.File.Exists(FBCrawler.cookiejarFilePath);
        }

        //從Cache中搜尋好友
        private void searchCache()
        {
            HashSet<fbUser> result = FBCrawler.FBCache.search(cBox_Search_UID.Checked ? tBox_Search_UID.Text : "", cBox_Search_Name.Checked ? tBox_Search_Name.Text : "");
            int i = 0;

            formatTable2DetailList(OutputTable);
            groupBox_FList.Text = "找到 " + result.Count + " 筆結果";

            //確認為好友
            foreach (fbUser user in result)
            {
                i++;
                Add2Table(OutputTable, new String[] {
                            i.ToString(),  //#
                            user.cover_name,  //名稱
                            user.profile_id,  //UserID
                            (user.friends_isPublic) ? user.friends.Count.ToString() : "-",   //好友數
                            "-",  //結果
                            "-",   //與目標距離
                            "-",  //處理順位
                            "-" });  //可能性分數
            }
        }

        //測試getFriend函數
        private void testFriendList(string uid)
        {
            groupBox_FList.Text = "Friend List (Loading...)";

            bwGetFriendsList tmp = new bwGetFriendsList(uid, "GUI Test Func");
            while (tmp.isCanceled() == false && tmp.isFinished() == false)
            {
                groupBox_FList.Text = "Friend List (" + tmp.getProgressMsg() + ")";
                Application.DoEvents();
                System.Threading.Thread.Sleep(200);
            }

            if (tmp.isFinished())
            {
                HashSet<String> friends = tmp.getResult();
                groupBox_FList.Text = FBCrawler.getFBNameByUid(uid) + "(" + uid + ")'s Friend List (" + friends.Count + ")";
                formatTable2FriendList(OutputTable);

                int i = 0;
                foreach (String user_id in friends)
                {
                    i++;
                    Add2Table(OutputTable, new String[] { i.ToString(), FBCrawler.getFBNameByUid(user_id), user_id });
                }
            }
            else
            {
                groupBox_FList.Text = "Friend List (Canceled)";
            }
        }

        //顯示結果至OutputTable上
        private void displayResult(mapResult result, bool isFinal, bool includeDrop = false, bool debug = false)
        {

            //編號
            int i = 0;

            try
            {
                if (debug)
                {
                    formatTable2DetailList(OutputTable);

                    //確認為好友
                    foreach (fbUser user in result.targetUser.checkedFriendList)
                    {
                        i++;
                        Add2Table(OutputTable, new String[] { 
                                i.ToString(),  //#
                                user.cover_name,  //名稱
                                user.profile_id,  //UserID
                                (user.friends_isPublic) ? user.friends.Count.ToString() : "-",   //好友數
                                "確認為好友",  //結果
                                user.distance.ToString(),   //與目標距離
                                (result.targetUser.processList.FindIndex(a => a.Equals(user.profile_id))+1).ToString(),  //處理順位
                                (result.targetUser.dataList.Contains(user.profile_id)) ? result.targetUser.dataList[user.profile_id].ToString() : "-",  //資訊
                                "-" });  //可能性分數
                    }

                    //無法確定為好友, 但對答案為好友
                    foreach (fbUser user in result.targetUser.possibleFriendList)
                    {
                        if (result.targetUser.answerFriendList.Contains(user.profile_id))
                        {
                            i++;

                            Add2Table(OutputTable, new String[] { 
                            i.ToString(),  //#
                            user.cover_name,  //名稱
                            user.profile_id,  //UserID
                            (user.friends_isPublic) ? user.friends.Count.ToString() : "-",   //好友數
                            "是好友(對答案)",  //結果
                            user.distance.ToString(),   //與目標距離
                            (result.targetUser.processList.FindIndex(a => a.Equals(user.profile_id))+1).ToString(),  //處理順位
                            (result.targetUser.dataList.Contains(user.profile_id)) ? result.targetUser.dataList[user.profile_id].ToString() : "-",  //資訊
                            (result.targetUser.possibleFriendListScore.Contains(user.profile_id)) ? result.targetUser.possibleFriendListScore[user.profile_id].ToString() : "-" });  //可能性分數
                        }
                    }

                    //無法確定為好友, 對答案不是好友, 或是根本沒有答案
                    foreach (fbUser user in result.targetUser.possibleFriendList)
                    {
                        if (!result.targetUser.answerFriendList.Contains(user.profile_id))
                        {
                            i++;

                            Add2Table(OutputTable, new String[] { 
                            i.ToString(),  //#
                            user.cover_name,  //名稱
                            user.profile_id,  //UserID
                            (user.friends_isPublic) ? user.friends.Count.ToString() : "-",   //好友數
                            (result.targetUser.answerFriendList.Count==0) ? "無法確定" : "不是好友(對答案)",  //結果
                            user.distance.ToString(),   //與目標距離
                            (result.targetUser.processList.FindIndex(a => a.Equals(user.profile_id))+1).ToString(),  //處理順位
                            (result.targetUser.dataList.Contains(user.profile_id)) ? result.targetUser.dataList[user.profile_id].ToString() : "-",  //資訊
                            (result.targetUser.possibleFriendListScore.Contains(user.profile_id)) ? result.targetUser.possibleFriendListScore[user.profile_id].ToString() : "-" });  //可能性分數
                        }
                    }

                    if (includeDrop)
                    {
                        //確定不是好友(被丟棄的)
                        foreach (fbUser user in result.targetUser.dropFriendList)
                        {
                            i++;

                            Add2Table(OutputTable, new String[] { 
                            i.ToString(),  //#
                            user.cover_name,  //名稱
                            user.profile_id,  //UserID
                            (user.friends_isPublic) ? user.friends.Count.ToString() : "-",   //好友數
                            "確認為非好友",  //結果
                            "-",   //與目標距離
                            (result.targetUser.processList.FindIndex(a => a.Equals(user.profile_id))+1).ToString(),  //處理順位
                            (result.targetUser.dataList.Contains(user.profile_id)) ? result.targetUser.dataList[user.profile_id].ToString() : "-",  //資訊
                            (result.targetUser.possibleFriendListScore.Contains(user.profile_id)) ? result.targetUser.possibleFriendListScore[user.profile_id].ToString() : "-" });  //可能性分數
                        }
                    }
                    
                }
                else
                {
                    //使用者模式
                    if (cBox_UserModeWithAnswer.Checked && result.targetUser.answerFriendList.Count > 0)
                    {
                        formatTable2DetailUserListWithAnswer(OutputTable);
                    }
                    else
                    {
                        formatTable2DetailUserList(OutputTable);
                    }

                    //確認為好友
                    foreach (fbUser user in result.targetUser.checkedFriendList)
                    {
                        i++;
                        if (cBox_UserModeWithAnswer.Checked && result.targetUser.answerFriendList.Count > 0)
                        {
                            Add2Table(OutputTable, new String[] { 
                                i.ToString(),  //#
                                user.cover_name,  //名稱
                                user.profile_id,  //UserID
                                "✔ 確認為好友",  //結果
                                (result.targetUser.answerFriendList.Contains(user.profile_id)?"✔ 正確":"✘ 錯誤") //答案
                            });
                        }
                        else
                        {
                            Add2Table(OutputTable, new String[] { 
                                i.ToString(),  //#
                                user.cover_name,  //名稱
                                user.profile_id,  //UserID
                                "✔ 確認為好友"  //結果
                            });
                        }
                    }

                    if (isFinal)
                    {
                        //無法確定為好友
                        foreach (fbUser user in result.targetUser.possibleFriendSortedList.Keys)
                        {
                            i++;
                            if (cBox_UserModeWithAnswer.Checked && result.targetUser.answerFriendList.Count > 0)
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "？ 可能為好友",  //結果
                                    (result.targetUser.answerFriendList.Contains(user.profile_id)?"✔ 正確":"✘ 錯誤") //答案
                                });
                            }
                            else
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "？ 可能為好友"  //結果
                                });
                            }
                            
                        }
                    }
                    else
                    {
                        //無法確定為好友
                        foreach (fbUser user in result.targetUser.possibleFriendList)
                        {
                            i++;
                            if (cBox_UserModeWithAnswer.Checked && result.targetUser.answerFriendList.Count > 0)
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "？ 可能為好友",  //結果
                                    (result.targetUser.answerFriendList.Contains(user.profile_id)?"✔ 正確":"✘ 錯誤") //答案
                                });
                            }
                            else
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "？ 可能為好友"  //結果
                                });
                            }
                            
                        }
                    }
                    

                    if (includeDrop)
                    {
                        //確定不是好友(被丟棄的)
                        foreach (fbUser user in result.targetUser.dropFriendList)
                        {
                            i++;
                            if (cBox_UserModeWithAnswer.Checked && result.targetUser.answerFriendList.Count > 0)
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "✘ 不是好友",  //結果
                                    (result.targetUser.answerFriendList.Contains(user.profile_id)?"✔ 正確":"✘ 錯誤") //答案
                                });
                            }
                            else
                            {
                                Add2Table(OutputTable, new String[] { 
                                    i.ToString(),  //#
                                    user.cover_name,  //名稱
                                    user.profile_id,  //UserID
                                    "✘ 不是好友"  //結果
                                });
                            }
                            
                        }
                    }
                }

                Application.DoEvents();

                if (isFinal)
                {
                    if (debug)
                    {
                        label_result.Text =
                        "===== 除錯模式 =====" + "\n" +
                        " 抓取好友數: " + result.var_count_getFriendList.ToString() + "\n" +
                        " 找到好友數: " + result.var_correct.ToString() + "\n" +
                        " 找到好友數(對答案): " + result.var_notsure_correct.ToString() + "\n" +
                        " 丟棄好友數: " + result.var_incorrect.ToString() + "\n" +
                        " 不確定好友數: " + result.var_notsure.ToString() + "\n" +
                        " Answer(Crawl): " + ((result.answer > 0) ? result.answer.ToString() : "N/A") + "\n" +
                        " Answer(Given): " + ((result.answer_given > 0) ? result.answer_given.ToString() : "N/A") + "\n" +
                        " Precision: " + (((double)result.var_correct / ((double)result.var_count_getFriendList))).ToString("#0.000000") + " (" + result.var_correct.ToString() + "/" + result.var_count_getFriendList.ToString() + ")" + "\n" +
                        " Recall(Crawl): " + ((result.answer > 0) ? ((double)result.var_correct / (double)result.answer).ToString("#0.000000") : "N/A") + " (" + result.var_correct.ToString() + "/" + ((result.answer > 0) ? result.answer.ToString() : "???") + ")" + "\n" +
                        " Recall(Given): " + ((result.answer_given > 0) ? ((double)result.var_correct / (double)result.answer_given).ToString("#0.000000") : "N/A") + " (" + result.var_correct.ToString() + "/" + ((result.answer_given > 0) ? result.answer_given.ToString() : "???") + ")" + "\n" +
                        " Precision[+posbList](Crawl): " + ((result.var_notsure_correct > 0) ? (((double)result.var_notsure_correct / ((double)result.var_count_getFriendList))).ToString("#0.000000") : "N/A") + " (" + result.var_notsure_correct.ToString() + "/" + result.var_count_getFriendList.ToString() + ")" + "\n" +
                        " Recall[+posbList](Crawl): " + ((result.answer > 0 && result.var_notsure_correct > 0) ? ((double)result.var_notsure_correct / (double)result.answer).ToString("#0.000000") : "N/A") + " (" + result.var_notsure_correct.ToString() + "/" + ((result.answer > 0) ? result.answer.ToString() : "???") + ")" + "\n" +
                        " Recall[+posbList](Given): " + ((result.answer_given > 0 && result.var_notsure_correct > 0) ? ((double)result.var_notsure_correct / (double)result.answer_given).ToString("#0.000000") : "N/A") + " (" + result.var_notsure_correct.ToString() + "/" + ((result.answer_given > 0) ? result.answer_given.ToString() : "???") + ")";
                    }
                    else
                    {
                        label_result.Text =
                        "===== 使用者模式 =====" + "\n" +
                        " 抓取好友數: " + result.var_count_getFriendList.ToString() + "\n" +
                        " ✔ 找到好友數: " + (result.var_correct - result.var_notsure_correct).ToString() + "\n" +
                        " ？ 可能的好友數: " + result.var_notsure.ToString() +
                        (cBox_IncludeDrop.Checked?"\n" +" ✘ 不是好友數: " + (result.var_incorrect - result.var_notsure_incorrect).ToString():"");
                    }
                }

                //嘗試繪圖
                if (result.answer > 0 || result.answer_given > 0)
                {
                    if (rBtn_GraphTable.Checked)
                    {
                        paintLineChart(result.targetUser.cover_name + "(" + result.targetUser.profile_id + ") 抓取好友的 Precision and recall", OutputTable, 2, result.targetUser.answerFriendList, result.answer_given);
                    }
                    else if (rBtn_GraphProcess.Checked)
                    {
                        paintLineChart(result.targetUser.cover_name + "(" + result.targetUser.profile_id + ") 抓取好友的 Precision and recall", result);
                    }
                }
                else
                {
                    this.LineChart.Titles.Clear();
                    this.LineChart.Titles.Add("無法產生繪圖! " + result.targetUser.cover_name + "(" + result.targetUser.profile_id + ")" + " 沒有正確的好友名單資料");
                }
                

                Application.DoEvents();
            }
            catch (Exception ex)
            {
                if (isFinal)
                {
                    DebugManager.add(DEBUG_LEVEL.ERROR, "displayResult : " + ex.Message);
                }
                else
                {
                    MessageBox.Show("找不到項目: " + cBox_ResultDisplay.Text + "\n\n" + ex.Message);
                    cBox_ResultDisplayUpdate("");
                }
            }
            
        }

        #endregion


        private void button_Action_Click(object sender, EventArgs e)
        {

            if (button_Action.Text.Equals("Start"))
            {
                cBox_ResultDisplay.Enabled = false;
                cBox_ResultDisplay.Text = "(null)";
                button_SaveResultToFile.Enabled = false;

                LimitManager.Count.reset();
                string str = cBox_TergetID.Text;
                string uid = (str.IndexOf('(') > -1 && str.IndexOf(')') > -1) ? str.Substring(str.IndexOf('(') + 1, str.IndexOf(')') - str.IndexOf('(') - 1) : str;
                string name = FBCrawler.getFBNameByUid(uid);
                if (String.IsNullOrEmpty(name)) { MessageBox.Show("找不到 " + uid + " 的對應使用者!"); return; }

                FBCrawler.flag_cancel = false;
                button_Action.Text = "Stop";
                textBox_Answer.Text = ((cBox_TryAnswer.Checked) ? "???" : "N/A") + " / " + ((num_SetAnswer.Value > 0) ? num_SetAnswer.Value.ToString() : "N/A");
                textBox_Answer.Visible = true;
                Application.DoEvents();

                FBCrawler.flag_newChache = false;

                LimitManager.Limit.getFB = (long)num_Limit_getFB.Value;
                LimitManager.Limit.getFriendList = (long)num_Limit_getFriendList.Value;
                LimitManager.Count.getFriendList -= (cBox_TryAnswer.Checked ? 1 : 0); //扣除抓取答案的限制

                //嘗試找出答案(如果有)
                long answer = -1;
                long answer_given = (long)num_SetAnswer.Value;
                HashSet<String> answerList = new HashSet<String>();

                if (cBox_TryAnswer.Checked)
                {
                    answerList = FBCrawler.getFriendList(uid);
                    answer = (answerList.Count > 0) ? answerList.Count : -1;
                }

                textBox_Answer.Text = ((answer > 0) ? answer.ToString() : "N/A") + " / " + ((num_SetAnswer.Value > 0) ? num_SetAnswer.Value.ToString() : "N/A");
                Application.DoEvents();

                //宣告結果ResultList
                mapResult tmpResult = new mapResult();

                main = new bwMain(uid, (int)num_Layer.Value, "Main", answerList);
                main.flagMutualFriends = cBox_FindMutualFriends.Checked;
                main.Run();
                while (main.isCanceled() == false && main.isFinished() == false)
                {
                    try
                    {
                        groupBox_FList.Text = "Result List (" + main.getProgressMsg() + ")";

                        //儲存結果至ResultList
                        tmpResult.Save(
                            main.getResult(),
                            main.Precision_Recall,
                            LimitManager.Count.getFriendList,
                            LimitManager.Count.getFB,
                            -1, -1, -1, -1, -1,
                            answer,
                            answer_given);

                        displayResult(tmpResult, false, cBox_IncludeDrop.Checked, !cBox_UserMode.Checked);

                    }
                    catch (Exception ex)
                    {
                        DebugManager.add(DEBUG_LEVEL.ERROR, ex.Message);
                    }

                    Application.DoEvents();
                    System.Threading.Thread.Sleep(500);
                }

                if (main.isFinished())
                {
                    //宣告結果ResultList
                    mapResult finalResult = new mapResult();
                    //儲存結果至ResultList
                    finalResult = main.getMapResult();
                    finalResult.answer_given = answer_given;

                    if (finalResult.var_correct + finalResult.var_incorrect + finalResult.var_notsure > 0)
                    {
                        //成功
                        //輸出ResultList結果
                        displayResult(finalResult, true, cBox_IncludeDrop.Checked, !cBox_UserMode.Checked);

                        groupBox_FList.Text = finalResult.targetUser.cover_name + " (" + finalResult.targetUser.profile_id + ") 的結果:";
                        groupBox_result.Visible = true;

                        ResultList.Add(name + "(" + uid + ")" + "[" + DateTime.Now.ToString() + "]", finalResult);
                        cBox_ResultDisplayUpdate(name + "(" + uid + ")" + "[" + DateTime.Now.ToString() + "]");
                    }
                    else
                    {
                        groupBox_FList.Text = finalResult.targetUser.cover_name + " (" + finalResult.targetUser.profile_id + ") 的結果取得失敗";

                        //失敗
                        cBox_ResultDisplayUpdate("(null)");
                    }
                    textBox_Answer.Visible = false;
                    cBox_ResultDisplay.Enabled = true;
                    button_SaveResultToFile.Enabled = true;
                    Application.DoEvents();
                }
                else
                {
                    groupBox_FList.Text = "已取消";
                    textBox_Answer.Visible = false;
                    Application.DoEvents();
                }
                button_Action.Text = "Start";
                Application.DoEvents();
            }
            else if (button_Action.Text.Equals("Stop"))
            {
                if (main != null && (!main.isCanceled()))
                {
                    main.Cancel();
                }
                button_Action.Text = "Start";
                cBox_ResultDisplay.Enabled = true;
                button_SaveResultToFile.Enabled = true;
                Application.DoEvents();
            }
        }

        private void paintLineChart(string Title, mapResult result)
        {
            GraphTable.Clear();
            formatTable2GraphList(GraphTable);

            Series series = new Series("Value", 1); // Recall 標題 最大數值

            series.YValueType = ChartValueType.Double;
            series.Color = Color.Blue; //設定線條顏色
            series.Font = new System.Drawing.Font("微軟正黑體", 12); //設定字型
            series.MarkerStyle = MarkerStyle.Circle; //圓形標記
            series.ChartType = SeriesChartType.Line; //折線圖
            series.IsValueShownAsLabel = true; //將數值顯示在線上

            //將數值新增至序列
            long i = 0;
            //long tmp = 0;
            foreach (PrecisionRecall.PrecisionRecallItem item in result.Precision_Recall.PrecisionRecallList)
            {
                /*if(item.var_correct>tmp)
                {*/
                    i++;
                    //tmp = item.var_correct;

                    series.Points.AddXY(
                        Math.Round(((double)(item.var_correct) / (double)((result.answer_given > result.answer) ? result.answer_given : result.answer)) * 100, (int)num_Graph_RecallRound.Value),
                        Math.Round((double)(item.var_correct) / (double)(item.var_crawled), (int)num_Graph_DataRound.Value));

                    Add2Table(GraphTable, new string[] { 
                        i.ToString(),
                        Math.Round((double)(item.var_correct) / (double)(item.var_crawled), (int)num_Graph_DataRound.Value) + (cBox_GraphShowDetail.Checked?(" (" + item.var_correct.ToString() + " / " + item.var_crawled.ToString() + ")"):""),
                        Math.Round(((double)(item.var_correct) / (double)((result.answer_given > result.answer) ? result.answer_given : result.answer)) * 100, (int)num_Graph_RecallRound.Value) + "%" + (cBox_GraphShowDetail.Checked?(" (" + item.var_correct.ToString() + " / " + ((result.answer_given > result.answer) ? result.answer_given : result.answer).ToString() + ")"):""),
                        item.var_crawled.ToString(),
                        item.var_correct.ToString(),
                        item.var_incorrect.ToString()
                    });
                /*}*/
            }

            try
            {
                //清理
                this.LineChart.Series.Clear();

                //將序列新增到圖上
                this.LineChart.Series.Add(series);

                //標題
                this.LineChart.Titles.Clear();
                this.LineChart.Titles.Add(Title);

            }
            catch (Exception ex)
            {
                DebugManager.add(DEBUG_LEVEL.ERROR, "paintLineChart - " + ex.Message);
            }


        }

        private void paintLineChart(string Title, ListView listView, int dataIndex, HashSet<String> answerList, long answer = -1)
        {
            GraphTable.Clear();
            formatTable2GraphList(GraphTable);

            Series series = new Series("Value", 1); // Recall 標題 最大數值

            series.YValueType = ChartValueType.Double;
            series.Color = Color.Blue; //設定線條顏色
            series.Font = new System.Drawing.Font("微軟正黑體", 12); //設定字型
            series.MarkerStyle = MarkerStyle.Circle; //圓形標記
            series.ChartType = SeriesChartType.Line; //折線圖
            series.IsValueShownAsLabel = true; //將數值顯示在線上

            //將數值新增至序列
            long i = 0;
            //long tmp = 0;
            long var_correct = 0;
            long var_incorrect = 0;
            for (int j = 0; j < listView.Items.Count; j++)
            {
                if ((!String.IsNullOrEmpty(listView.Items[j].SubItems[dataIndex].Text)) && answerList.Contains(listView.Items[j].SubItems[dataIndex].Text))
                    var_correct++;
                else
                    var_incorrect++;
                /*if (var_correct > tmp)
                {*/
                    i++;
                    //tmp = var_correct;

                    series.Points.AddXY(
                        Math.Round(((double)(var_correct) / (double)((answer > answerList.Count) ? answer : answerList.Count)) * 100, (int)num_Graph_RecallRound.Value), //recall
                        Math.Round((double)(var_correct) / (double)(j + 1), (int)num_Graph_DataRound.Value)); //precision

                    Add2Table(GraphTable, new string[] { 
                        i.ToString(),
                        //precision
                        Math.Round((double)(var_correct) / (double)(var_correct + var_incorrect), (int)num_Graph_DataRound.Value) + (cBox_GraphShowDetail.Checked?(" (" + var_correct.ToString() + " / " + (j+1).ToString() + ")"):""),
                        //recall
                        Math.Round(((double)(var_correct) / (double)((answer > answerList.Count) ? answer : answerList.Count)) * 100, (int)num_Graph_RecallRound.Value) + "%" + (cBox_GraphShowDetail.Checked?(" (" + var_correct.ToString() + " / " + ((answer > answerList.Count) ? answer : answerList.Count).ToString() + ")"):""),
                        (var_correct + var_incorrect).ToString(),
                        var_correct.ToString(),
                        var_incorrect.ToString()
                    });
                /*}*/
            }

            try
            {
                //清理
                this.LineChart.Series.Clear();

                //將序列新增到圖上
                this.LineChart.Series.Add(series);

                //標題
                this.LineChart.Titles.Clear();
                this.LineChart.Titles.Add(Title);

            }
            catch (Exception ex)
            {
                DebugManager.add(DEBUG_LEVEL.ERROR, "paintLineChart - " + ex.Message);
            }


        }

        private void rBtn_RecentResult_CheckedChanged(object sender, EventArgs e)
        {
            updateResultList(rBtn_FileResult.Checked);
        }

        private void rBtn_FileResult_CheckedChanged(object sender, EventArgs e)
        {
            updateResultList(rBtn_FileResult.Checked);
        }

        private void button_SaveResultToFile_Click(object sender, EventArgs e)
        {
            if (ResultList.Count > 0 && (!cBox_ResultDisplay.Text.Equals("(null)")) && (!String.IsNullOrEmpty(cBox_ResultDisplay.Text)) && ResultList[cBox_ResultDisplay.Text] != null)
            {
                button_SaveResultToFile.Enabled = false;
                button_SaveResultToFile.Text = "Saving...";
                Application.DoEvents();

                string temp = cBox_ResultDisplay.Text;
                char[] ill = new char[] { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };
                string file_name = temp;
                foreach(char i in ill)
                    file_name.Replace(i, '-');
                try
                {
                    ResultList[cBox_ResultDisplay.Text].saveToFile(Application.StartupPath + @"\result\" + file_name);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                button_SaveResultToFile.Text = "Save";
                button_SaveResultToFile.Enabled = true;
                getResultListFromFile();
                updateResultList(rBtn_FileResult.Checked);
                cBox_ResultDisplay.Text = temp;
                Application.DoEvents();
            }
        }

        private void updateResultList(bool fromFile = false)
        {
            cBox_ResultDisplay.Items.Clear();
            foreach (string item in (fromFile ? ResultListFromFile.Keys : ResultList.Keys))
            {
                cBox_ResultDisplay.Items.Add(item);
            }
            cBox_ResultDisplay.Text = "(null)";
        }

        private void getResultListFromFile()
        {
            ResultListFromFile.Clear();
            foreach (string f in Directory.EnumerateFiles(Application.StartupPath + @"\result\", "*.xml", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    ResultListFromFile.Add(Path.GetFileName(f).Replace(".xml", ""), new mapResult().loadFromFile(f));
                }
                catch (Exception ex)
                {
                    DebugManager.add(DEBUG_LEVEL.ERROR, "讀取 " + Path.GetFileName(f) + " 失敗 - " + ex.Message);
                }
            }
        }

        private void button_TestMutualFriendList_Click(object sender, EventArgs e)
        {
            groupBox_FList.Text = "Mutual Friend List (Loading...)";
            Application.DoEvents();

            HashSet<fbUser> uesrs = FBCrawler.getMutualFriendList(cBox_TestID.Text, cBox_TestID2.Text);

            formatTable2DetailList(OutputTable);

            int i = 0;
            foreach (fbUser uesr in uesrs)
            {
                i++;
                Add2Table(OutputTable, new String[] { i.ToString(), uesr.cover_name, uesr.profile_id });
            }

            groupBox_FList.Text = FBCrawler.getFBNameByUid(cBox_TestID.Text) + " and " + FBCrawler.getFBNameByUid(cBox_TestID2.Text) + "'s Mutual Friend List(" + i.ToString() + ")";
        }

        

    }
}
