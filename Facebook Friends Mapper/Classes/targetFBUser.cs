using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public class targetFBUser : fbUser //繼承fbUser
    {
        public HashSet<fbUser> checkedFriendList = new HashSet<fbUser>();
        public HashSet<fbUser> possibleFriendList = new HashSet<fbUser>();
        public Dictionary<fbUser, int> possibleFriendSortedList = new Dictionary<fbUser, int>();
        public HashSet<fbUser> dropFriendList = new HashSet<fbUser>();
        public HashSet<String> answerFriendList = new HashSet<String>();
        public List<String> processList = new List<String>();
        public Hashtable dataList = new Hashtable();
        public Hashtable possibleFriendListScore = new Hashtable();

        //建構子 Constructor
        public targetFBUser()
        {
            this.profile_id = "";
            this.cover_name = "";
            this.distance = 0;
        }

        public targetFBUser(string profile_id, string cover_name)
        {
            this.profile_id = profile_id;
            this.cover_name = cover_name;
            this.distance = 0;
        }

        //方法 Method
        public bool IsInFriendList(string fbUserUid, HashSet<fbUser> friendList)
        {
            foreach (fbUser user in friendList)
            {
                if (user.profile_id.Equals(fbUserUid))
                    return true;
            }
            return false;
        }

        public bool IsInFriendList(fbUser fbUserClass, HashSet<fbUser> friendList)
        {
            return IsInFriendList(fbUserClass.profile_id, friendList);
        }

    }
}
