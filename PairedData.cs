using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace BlueShift
{
    class PairedData : Dictionary<string, string>
    {
        public string ToPOSTStr()
        {
            string output = "";
            bool first_flag = true;
            foreach (KeyValuePair<string, string> tmp in this)
            {
                if (first_flag)
                    output += tmp.Key + "=" + HttpUtility.UrlEncode(tmp.Value);
                else
                    output += "&" + tmp.Key + "=" + HttpUtility.UrlEncode(tmp.Value);
              
                first_flag = false;
            }
            return output;
        }


    }
}
