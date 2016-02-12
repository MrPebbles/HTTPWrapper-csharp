using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.IO.Compression;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;

namespace BlueShift
{
     public class HTTPWrapper
    {
        public event HTTPEventListener NewDataArrived;

        public bool UseGZip = true;
        public bool UseProxy = false;
        public string ProxyServer;
        public string ProxyPort;
        public string ProxyUsername;
        public string ProxyPassword;

        public struct Last_Request
        {
            public string Url;
            public string RequestType;
            public string Data;
            public string Referer;
            public string DataSent;
            public string HeaderReceived;
            public string BodyRecieved;
        }

        public Last_Request LastRequest = new Last_Request();
        public Dictionary<string, string> Cookies = new Dictionary<string, string>();
        
        public string GET(string Url)
        {
            return Navigate("GET", Url, "", null, true);
        }

        public string GET(string Url, string Referer)
        {
            return Navigate("GET", Url, "", Referer, true);
        }

        public string POST(string Url, Dictionary<string, string> pData)
        {
            return Navigate("POST", Url, Utilities.KVPToStr(pData), null, true);
        }

        public string POST(string Url, Dictionary<string, string> pData, string Referer)
        {
            return Navigate("POST", Url, Utilities.KVPToStr(pData), Referer, true);
        }

        public string POST(string Url, string pData, string Referer)
        {
            return Navigate("POST", Url, pData, Referer, true);
        }

        public string POST(string Url, string pData)
        {
            return Navigate("POST", Url, pData, null, true);
        }

        public System.Drawing.Bitmap GetImage(string Url, string Referer)
        {
            string source = Navigate("GET", Url, "", Referer, false);
            MemoryStream mStream = new MemoryStream(Encoding.Default.GetBytes(source));
            return new System.Drawing.Bitmap(mStream);
        }

        public string Refresh()
        {
            return Navigate(LastRequest.RequestType, LastRequest.Url, LastRequest.Data, LastRequest.Referer, true);
        }

        string Navigate(string Request, string Url, string Data, string Referer, bool ReturnHeader)
        {
            string Raw_Html, Server, Page;
            int Retry = 3;

            //Get the referer if nothing's provided
            if ((Referer == null) || (Referer == ""))
                if (LastRequest.RequestType == "POST")
                    Referer = LastRequest.Url + "?" + LastRequest.Data;
                else
                    Referer = LastRequest.Url;

            //Remove the "http://" part from the url
            Url = Url.Replace("http://", "");

            //Parse for the server & page info
            if (Url.Contains("/"))
            {
                int i = Url.IndexOf("/");
                Server = Url.Substring(0, i);
                Page = Url.Substring(i, Url.Length - i);
            }
            else
            {
                Server = Url;
                Page = "/";
            }

            TcpClient HTTPClient = new TcpClient();

            string Final_Server;
            int Final_Port;

            //Proxy Support
            if (string.IsNullOrEmpty(ProxyServer) || string.IsNullOrEmpty(ProxyPort) || UseProxy == false)
            {
                Final_Server = Server;
                Final_Port = 80;
            }
            else
            {
                Final_Server = ProxyServer;
                Final_Port = int.Parse(ProxyPort);
            }

            do
            {
                try
                {
                    HTTPClient.Connect(Final_Server, Final_Port);
                }
                catch 
                {
                    Thread.Sleep(5000);
                }
            } while (Retry != 0 && !HTTPClient.Connected);

            if (!HTTPClient.Connected)
            {
                if (NewDataArrived != null) NewDataArrived(this, "Error: Could not connect to server.", BuildCookies());
                return "";
            }

            NetworkStream HTTPStream = HTTPClient.GetStream();
            HTTPStream.WriteTimeout = HTTPStream.ReadTimeout = 15000; //15 Sec Timeout

            try
            {
                //Send the data
                StreamWriter sw = new StreamWriter(HTTPStream);
                LastRequest.DataSent = CreateHeader(Request, Server, Page, BuildCookies(), Referer, Data);
                sw.Write(LastRequest.DataSent);
                sw.Flush();

                //Capture all data
                StreamReader sr = new StreamReader(HTTPStream, Encoding.Default);
                Raw_Html = sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                if (NewDataArrived != null) NewDataArrived(this, "Error: Could not write/read from network stream. Original error: " + ex.Message, BuildCookies());
                return "";
            }

            HTTPClient.Close();

            if (string.IsNullOrEmpty(Raw_Html))
            {
                if (NewDataArrived != null) NewDataArrived(this, "Error: Failed to recieve data from server.", BuildCookies());
                return "";
            }

            int Separator = Raw_Html.IndexOf("\r\n\r\n") + 4;
            LastRequest.HeaderReceived = Raw_Html.Substring(0, Separator);
            LastRequest.RequestType = Request;
            LastRequest.Url = "http://" + Url;
            LastRequest.Data = Data;
            LastRequest.Referer = Referer;

            WriteCookies(LastRequest.HeaderReceived);

            string Processed_Body = Raw_Html.Substring(Separator);

            if (LastRequest.HeaderReceived.Contains("Content-Encoding: gzip"))
                Processed_Body = DecompressGZip(Processed_Body);

            LastRequest.BodyRecieved = Processed_Body;

            //FIRE ZE MISSILES..oh..i mean ZE EVENTS!!
            if (NewDataArrived != null) NewDataArrived(this, Processed_Body, BuildCookies());

            if (ReturnHeader)
                return string.Format("{0}{1}", LastRequest.HeaderReceived, Processed_Body);
            else
                return Processed_Body;
        }

        string CreateHeader(string HTTPRequest, string Server, string Page, string Cookies, string Referer, string Data)
        {
            StringBuilder output = new StringBuilder();

            if (UseProxy)
                Page = "http://" + Server + Page;
              
            output.Append(string.Format("{0} {1} HTTP/1.1\r\nHost: {2}\r\n", HTTPRequest, Page, Server));

            if (UseProxy && !string.IsNullOrEmpty(ProxyUsername) && !string.IsNullOrEmpty(ProxyPassword))
            {
                string auth_string = base64Encode(string.Format("{0}:{1}", ProxyUsername, ProxyPassword));
                output.Append(string.Format("Proxy-Authorization: Basic {0}\r\n", auth_string));
            }

            output.Append("User-Agent: Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.1.4) Gecko/20091016 Firefox/3.5.4 (.NET CLR 3.5.30729)\r\n");
            output.Append("Accept: text/xml,application/xml,application/xhtml+xml,text/html;q=0.9,text/plain;q=0.8,image/png,*/ *;q=0.5\r\n");
            
            if (UseGZip)
                output.Append("Accept-Encoding: gzip\r\n");

            output.Append("Accept-Charset: ISO-8859-1,utf-8;q=0.7,*;q=0.7\r\n");
            output.Append(string.Format("Referer: {0}\r\n", Referer));

            if (!string.IsNullOrEmpty(Cookies))
                output.Append(string.Format("Cookie: {0}\r\n", Cookies));

            output.Append(string.Format("Content-Length: {0}\r\n", Data.Length));

            if(HTTPRequest == "POST")
            {
                output.Append("Content-Type: application/x-www-form-urlencoded\r\n");
                output.Append(string.Format("Connection: close\r\n\r\n{0}", Data));
            }
            else
                output.Append(string.Format("Connection: close\r\n\r\n"));

            return output.ToString();
        }

        public void ClearCookies()
        {
            Cookies.Clear();
        }

        string BuildCookies()
        {
            StringBuilder output = new StringBuilder();
            foreach (KeyValuePair<string, string> kvp in Cookies)
                output.Append(string.Format("{0}={1};", kvp.Key , kvp.Value));
            return output.ToString();
        }

        void WriteCookies(string hdr)
        {
            if (hdr.Contains("Set-Cookie: "))
            {
                string[] all_cookies = Utilities.GetBetweenAll("Set-Cookie: ", ";", hdr);
                for (int i = 0; i <= all_cookies.GetUpperBound(0); i++)
                {
                    string[] parts = all_cookies[i].Split('=');
                    if (Cookies.ContainsKey(parts[0]))
                        if (parts[1] == "deleted")
                            Cookies.Remove(parts[0]);
                        else
                            Cookies[parts[0]] = parts[1];
                    else
                        Cookies.Add(parts[0], parts[1]);
                }
            }
        }
        
        string DecompressGZip(string Compressed)
        {
            MemoryStream mStrm = new MemoryStream(Encoding.Default.GetBytes(Compressed));
            GZipStream dStrm = new GZipStream(mStrm, CompressionMode.Decompress);

            byte[] endBytes = new byte[4];
            int position = (int)mStrm.Length - 4;

            mStrm.Position = position;
            mStrm.Read(endBytes, 0, 4);
            mStrm.Position = 0;

            byte[] buffer = new byte[BitConverter.ToInt32(endBytes, 0) + 512];
            int offset = 0;
            while (true)
            {
                int bytes_read = dStrm.Read(buffer, offset, 512);
                if (bytes_read == 0) break;
                offset += bytes_read;
            }
            return Encoding.Default.GetString(buffer);
        }

        public string base64Encode(string data)
        {
            try
            {
                byte[] encData_byte = new byte[data.Length];
                encData_byte = System.Text.Encoding.UTF8.GetBytes(data);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Encode" + e.Message);
            }
        }

        public string base64Decode(string data)
        {
            try
            {
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();

                byte[] todecode_byte = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }
        }

    }

}
