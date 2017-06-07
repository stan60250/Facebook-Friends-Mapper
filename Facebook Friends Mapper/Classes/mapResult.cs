using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public class mapResult //結果
    {
        //目標使用者
        public targetFBUser targetUser = null;
        public PrecisionRecall Precision_Recall = null;

        public long var_count_getFriendList = 0;
        public long var_count_getFB = 0;

        public long var_correct = 0;
        public long var_incorrect = 0;
        public long var_notsure = 0;
        public long var_notsure_correct = 0;
        public long var_notsure_incorrect = 0;

        public long answer = -1;
        public long answer_given = -1;

        //Add a parameterless constructor.
        public mapResult() { }

        public void Save(targetFBUser targetUser, PrecisionRecall Precision_Recall, long var_count_getFriendList, long var_count_getFB, long var_correct, long var_incorrect, long var_notsure, long var_notsure_correct, long var_notsure_incorrect, long answer, long answer_given)
        {
            this.targetUser = targetUser;
            this.Precision_Recall = Precision_Recall;

            this.var_count_getFriendList = var_count_getFriendList;
            this.var_count_getFB = var_count_getFB;

            this.var_correct = var_correct;
            this.var_incorrect = var_incorrect;
            this.var_notsure = var_notsure;
            this.var_notsure_correct = var_notsure_correct;
            this.var_notsure_incorrect = var_notsure_incorrect;

            this.answer = answer;
            this.answer_given = answer_given;
        }

        public void saveToFile(string path)
        {
            if (File.Exists(path + ".tmp"))
                File.Delete(path + ".tmp");

            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(this.GetType());
            Stream s = File.Open(path + ".tmp", FileMode.Create);
            ser.Serialize(s, this);
            s.Close();

            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + ".tmp", path);
        }

        public mapResult loadFromFile(string path)
        {
            System.Xml.Serialization.XmlSerializer ser = new System.Xml.Serialization.XmlSerializer(this.GetType());
            Stream s = File.Open(path, FileMode.Open);
            mapResult tmp = (mapResult)ser.Deserialize(s);
            s.Close();
            return tmp;
        }
    }
}
