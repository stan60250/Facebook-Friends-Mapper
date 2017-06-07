using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public class fbUser //Facebook 使用者
    {
        public String profile_id { get; set; } //UID or 自訂username

        //public String profile_photo_link { get; set; }
        //public String profile_video_link { get; set; }

        public String cover_name { get; set; }
        public int distance { get; set; }
        public bool friends_isCached { get; set; }
        public bool friends_isPublic { get; set; }


        public HashSet<String> friends = new HashSet<String>();

        //建構子 Constructor
        public fbUser()
        {
            this.profile_id = "";
            this.cover_name = "";
            this.distance = -1;
            this.friends_isCached = false;
        }

        public fbUser(String profile_id, String cover_name)
        {
            this.profile_id = profile_id;
            this.cover_name = cover_name;
            this.distance = -1;
            this.friends_isCached = false;
        }

        public fbUser(String profile_id, String cover_name, int distance)
        {
            this.profile_id = profile_id;
            this.cover_name = cover_name;
            this.distance = distance;
            this.friends_isCached = false;
        }

        //方法 Method
        public bool hasFriend(string fbUserUid)
        {
            return friends.Contains(fbUserUid);
        }

        public override bool Equals(object obj)
        {
            var item = obj as fbUser;

            if (item == null)
            {
                return false;
            }

            return this.profile_id.Equals(item.profile_id);
        }

        public override int GetHashCode()
        {
            return this.profile_id.GetHashCode();
        }
    }
}
