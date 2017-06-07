using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public class PrecisionRecall
    {
        public class PrecisionRecallItem
        {
            public long var_crawled = 0;
            public long var_correct = 0;
            public long var_incorrect = 0;

            public PrecisionRecallItem(long var_crawled, long var_correct, long var_incorrect)
            {
                this.var_crawled = var_crawled;
                this.var_correct = var_correct;
                this.var_incorrect = var_incorrect;
            }
        }

        public List<PrecisionRecallItem> PrecisionRecallList = new List<PrecisionRecallItem>();

        private List<String> testedUserList = new List<String>();
        private long var_correct = 0;
        private long var_incorrect = 0;

        public void addPrecisionRecallItem(long var_crawled, fbUser user, HashSet<String> answerFriendList, bool flagNotSure)
        {
            addPrecisionRecallItem(var_crawled, user.profile_id, answerFriendList, flagNotSure);
        }
        public void addPrecisionRecallItem(long var_crawled, string uid, HashSet<String> answerFriendList, bool flagNotSure)
        {
            if (testedUserList.Contains(uid))
                return;

            testedUserList.Add(uid);

            PrecisionRecallList.Add(new PrecisionRecallItem(
                var_crawled,
                answerFriendList.Contains(uid) ? ++var_correct : var_correct,
                answerFriendList.Contains(uid) ? var_incorrect : ++var_incorrect));
        }
        
    }
}
