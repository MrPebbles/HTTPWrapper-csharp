using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlueShift
{
    class Cookie
    {
        private PairedData pData;

        public Cookie() {
            pData = new PairedData();           
        }

        public void ClearCookies()
        {
            lock (pData)
            {
                pData.Clear();
            }
        }

        public string BuildCookies()
        {
            lock (pData)
            {
                StringBuilder output = new StringBuilder();
                foreach (KeyValuePair<string, string> kvp in pData)
                    output.Append(string.Format("{0}={1};", kvp.Key, kvp.Value));
                return output.ToString();
            }
        }

        public void WriteCookies(string hdr)
        {
            lock (pData)
            {
                if (hdr.Contains("Set-Cookie: "))
                {
                    string[] all_cookies = Utilities.GetBetweenAll("Set-Cookie: ", ";", hdr);
                    for (int i = 0; i <= all_cookies.GetUpperBound(0); i++)
                    {
                        string[] parts = all_cookies[i].Split('=');
                        if (pData.ContainsKey(parts[0]))
                            if (parts[1] == "deleted")
                                pData.Remove(parts[0]);
                            else
                                pData[parts[0]] = parts[1];
                        else
                            pData.Add(parts[0], parts[1]);
                    }
                }
            }
        }
        


    }
}
