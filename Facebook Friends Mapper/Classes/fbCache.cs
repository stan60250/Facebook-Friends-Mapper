using Facebook_Friends_Mapper.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public class fbCache
    {
        private HashSet<fbUser> cachedUsers = new HashSet<fbUser>();

        public void addUser(String uid)
        {
            addUser(new fbUser(uid, FBCrawler.getFBNameByUid(uid)));
        }

        public void addUser(fbUser fbuser)
        {
            foreach (fbUser user in cachedUsers)
            {
                if (user.profile_id.Equals(fbuser.profile_id))
                {
                    //宣告使用者更新的Class & 更新flag
                    bool update_flag = false;
                    fbUser updatedUser = new fbUser(fbuser.profile_id, fbuser.cover_name);
                    //如果名字換了
                    if (!fbuser.cover_name.Equals(user.cover_name))
                    {
                        //上面建構子已經更新過
                        update_flag = true;
                    }
                    //如果好友資料有存在
                    if (fbuser.friends_isCached && fbuser.friends != null)
                    {
                        updatedUser.friends_isCached = fbuser.friends_isCached;
                        updatedUser.friends_isPublic = fbuser.friends_isPublic;
                        updatedUser.friends = new HashSet<String>(fbuser.friends);
                        update_flag = true;
                    }
                    //如果資料有更新
                    if (update_flag)
                    {
                        cachedUsers.Remove(user);
                        cachedUsers.Add(updatedUser);
                    }
                    return;
                }
            }
            cachedUsers.Add(fbuser);
        }

        public void addMultiUsers(HashSet<String> uidset)
        {
            foreach (String uid in uidset)
            {
                addUser(uid);
            }
        }

        public void addMultiUsers(HashSet<fbUser> fbusers)
        {
            foreach (fbUser fbuser in fbusers)
            {
                addUser(fbuser);
            }
        }

        public fbUser getUser(fbUser fbuser)
        {
            foreach (fbUser user in cachedUsers)
            {
                if (user.profile_id.Equals(fbuser.profile_id))
                    return user;
            }
            return null;
        }

        public fbUser getUser(String profile_id)
        {
            foreach (fbUser user in cachedUsers)
            {
                if (user.profile_id.Equals(profile_id))
                    return user;
            }
            return null;
        }

        public bool hasUser(String uid)
        {
            foreach (fbUser user in cachedUsers)
            {
                if (user.profile_id.Equals(uid))
                    return true;
            }
            return false;
        }

        public void flush()
        {
            cachedUsers.Clear();
        }

        public int getUserCount()
        {
            return cachedUsers.Count;
        }

        public void saveToFile(string path)
        {
            if (File.Exists(path + ".tmp"))
                File.Delete(path + ".tmp");

            XMLManager.WriteToXmlFile<HashSet<fbUser>>(path + ".tmp", cachedUsers);

            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + ".tmp", path);
        }

        public void loadFromFile(string path)
        {
            cachedUsers = XMLManager.ReadFromXmlFile<HashSet<fbUser>>(path);
        }

        /*public void saveToFile(string path)
        {
            using (FileStream outFile = File.Create(path))
            {
                XmlSerializer formatter = new XmlSerializer(typeof(HashSet<fbUser>));
                formatter.Serialize(outFile, cachedUsers);
            }
        }

        public void loadFromFile(string path)
        {
            HashSet<fbUser> listofa = new HashSet<fbUser>();
            XmlSerializer formatter = new XmlSerializer(typeof(HashSet<fbUser>));
            using (FileStream aFile = new FileStream(path, FileMode.Open))
            {
                byte[] buffer = new byte[aFile.Length];
                aFile.Read(buffer, 0, (int)aFile.Length);
                MemoryStream stream = new MemoryStream(buffer);
                cachedUsers = (HashSet<fbUser>)formatter.Deserialize(stream);
            }
        }*/

        public HashSet<fbUser> search(string uid, string name)
        {
            HashSet<fbUser> result = new HashSet<fbUser>();

            foreach (fbUser user in cachedUsers)
            {
                if ((!String.IsNullOrEmpty(uid)) && user.profile_id.IndexOf(uid, StringComparison.CurrentCultureIgnoreCase) > -1)
                {
                    result.Add(user);
                    continue;
                }
                if ((!String.IsNullOrEmpty(name)) && user.cover_name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) > -1)
                {
                    result.Add(user);
                    continue;
                }
            }

            return result;
        }
    }
}
