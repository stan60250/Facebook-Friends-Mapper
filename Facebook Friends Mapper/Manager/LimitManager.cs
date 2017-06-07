using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Manager
{
    public static class LimitManager
    {
        //限制
        public static class Limit
        {
            public static long getFB = -1; //∞
            public static long getFriendList = -1;
            public static long findFriendsInCode = -1;
            public static long getFBNameByUid = -1;

            public static void reset()
            {
                getFB = -1;
                getFriendList = -1;
                findFriendsInCode = -1;
                getFBNameByUid = -1;
            }
        }
        //計數
        public static class Count
        {
            public static long getFB = 0;
            public static long getFriendList = 0;
            public static long findFriendsInCode = 0;
            public static long getFBNameByUid = 0;

            public static void reset()
            {
                getFB = 0;
                getFriendList = 0;
                findFriendsInCode = 0;
                getFBNameByUid = 0;
            }
        }

        public static bool isVaild()
        {
            return
                ((Limit.getFB > -1 && Count.getFB < Limit.getFB) || (Limit.getFB == -1)) &&
                ((Limit.getFriendList > -1 && Count.getFriendList < Limit.getFriendList) || (Limit.getFriendList == -1)) &&
                ((Limit.findFriendsInCode > -1 && Count.findFriendsInCode < Limit.findFriendsInCode) || (Limit.findFriendsInCode == -1)) &&
                ((Limit.getFBNameByUid > -1 && Count.getFBNameByUid < Limit.getFBNameByUid) || (Limit.getFBNameByUid == -1));
        }
    }
}
