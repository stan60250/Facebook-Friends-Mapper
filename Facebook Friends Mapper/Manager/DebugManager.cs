using Facebook_Friends_Mapper.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Manager
{
    public static class DebugManager
    {
        public static List<DebugMessage> messageList { get; set; }

        static DebugManager()
        {
            messageList = new List<DebugMessage>();
        }

        public static void add(DEBUG_LEVEL level, string message)
        {
            messageList.Add(new DebugMessage(level, message));
        }

        public static void clear()
        {
            messageList.Clear();
        }
    }
}
