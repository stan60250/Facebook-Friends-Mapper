using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facebook_Friends_Mapper.Classes
{
    public enum DEBUG_LEVEL
    {
        FATAL,
        ERROR,
        WARN,
        INFO,
        DEBUG
    }

    public class DebugMessage
    {
        public DateTime time { get; set; }
        public DEBUG_LEVEL level { get; set; }
        public string message { get; set; }

        public DebugMessage(DEBUG_LEVEL level, string message)
        {
            this.time = DateTime.Now;
            this.level = level;
            this.message = message;
        }
    }
}
