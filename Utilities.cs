using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BlueShift
{
    static public class Utilities
    {
        static int last_index = 0;

        static public int CalcPause(double Min, double Max)
        {
            Random rand = new Random();
            Double output = Min + rand.NextDouble() * (Max - Min);
            return Convert.ToInt32((output * 1000));
        }

        static public int RandomNumber(int min, int max)
        {
            Random rand = new Random();
            return rand.Next(min, max);
        }

        static public string KVPToStr(Dictionary<string, string> data)
        {
            string output = "";
            bool first_flag = true;
            foreach(KeyValuePair<string, string> tmp in data)
            {
                if (first_flag)
                    output += tmp.Key + "=" + UrlEncode(tmp.Value);
                else
                    output += "&" + tmp.Key + "=" + UrlEncode(tmp.Value);
                first_flag = false;
            }
            return output;
        }

        static public int Fast_IndexOf(char[] str, int offset, char[] search)
        {
            bool found = false;
            int index = offset;
            int str_length = str.Length;
            int search_length = search.Length;

            while (index < str_length)
            {
                for (int x = 0; x < search_length; x++)
                    if (str[index + x] != search[x])
                    {
                        found = false;
                        break;
                    }
                    else
                        found = true;

                if (found)
                    return index;
                else
                    index++;
            }

            return -1;
        }

        static public string GetBetween(string start, string end, string source, int offset)
        {
            int s_index = source.IndexOf(start, offset);
            if (s_index > -1)
            {
                int e_index = source.IndexOf(end, s_index + start.Length + 1);
                if (e_index > -1)
                {
                    last_index = s_index + 1;
                    s_index += start.Length;
                    return source.Substring(s_index, e_index - s_index);
                }
            }
            return null;
        }

        static public string GetBetween_Regex(string pattern, string start, string end, string source)
        {
            string[] results = GetBetweenAll_Regex(pattern, start, end, source);
            return results[0];
        }

        static public string[] GetBetweenAll(string start, string end, string source)
        {
            string curr_val;
            last_index = 0;
            var al = new System.Collections.ArrayList();

            do
            {
                curr_val = GetBetween(start, end, source, last_index);

                if (curr_val != null)
                    al.Add((string)curr_val);

            } while (curr_val != null);

            return (string[])al.ToArray(typeof(string));
        }

        static public string[] GetBetweenAll_Regex(string pattern, string start, string end, string source)
        {
            Regex reg = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection Matches = reg.Matches(source);
            System.Collections.ArrayList al = new System.Collections.ArrayList();

            if (reg.IsMatch(source))
            {
                foreach (Match m in Matches)
                {
                    string tmpstr = GetBetween(start, end, m.Value, 0);

                    if (tmpstr != null)
                        al.Add((string)tmpstr);
                }
                return (string[])al.ToArray(typeof(string));
            }
            return null;
        }

        static public string GetMD5Hash(string input)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] bs = System.Text.Encoding.UTF8.GetBytes(input);
            bs = x.ComputeHash(bs);
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            foreach (byte b in bs)
                s.Append(b.ToString("x2").ToLower());
            return s.ToString();
        }

        static public string Humanize(string Number, int Percent_Min, int Percent_Max)
        {
            //int repeat;
            System.Text.StringBuilder output = new System.Text.StringBuilder();

            string strNumber = Number;

            strNumber = (Math.Round(Double.Parse(Number) * (double)RandomNumber(Percent_Min, Percent_Max) / 100)).ToString();

            for (int i = 1; i <= strNumber.Length; i++)
            {
                if(i%2 == 0)
                    output.Append(strNumber[1].ToString());
                else
                    output.Append(strNumber[0].ToString());
            }
           
            return output.ToString();
        }

        static public string RemoveTags(string source)
        {
            while (true)
            {
                int start = source.IndexOf("<");
                int end = source.IndexOf(">");
                if (start > -1 && end > -1)
                    source = source.Remove(start, end - start + 1);
                else
                    break;
            }
            return source;
        }

        static public string StripEscapeChars(string Source)
        {
            Source = Source.Replace("\r", "");
            Source = Source.Replace("\t", "");
            Source = Source.Replace("\n", "");
            return Source;
        }

        static public bool BoolThis(string str)
        {
            if (str == "0")
                return false;
            else
                return true;
        }

        static public string UrlEncode(string str)
        {
            return System.Web.HttpUtility.UrlEncode(str);
        }

        static public void CaptureError(Exception ex, HTTPWrapper Http, Global Options)
        {
            try
            {
                System.Diagnostics.StackTrace Trace = new System.Diagnostics.StackTrace(ex, true);
                StreamWriter sw = new StreamWriter("ErrorDump.txt");

                sw.WriteLine(String.Format("Exception: {0}\n{1}\n\n", ex.Message, Trace.ToString()));
                sw.Close();

                MessageBox.Show("The application encountered an error and will terminate shortly."
                    + "\nAll debugging information has been dumped into \"ErrorDump.txt\"", "Fatal Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            catch
            {
                Application.Exit();
            }
        }

    }
}