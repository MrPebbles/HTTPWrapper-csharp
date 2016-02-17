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
    class HTTPWrapper
    {
        private const int DEFAULT_RETRIES = 5;
        private const int MIN_RETRIES = 0;
        private const int MAX_RETRIES = 10;

        private const int DEFAULT_TIMEOUT = 10000;

        public bool UseGZip;
        public bool UseProxy;

        private LastRequest lastReq;
        private Cookie cookies;

        private string proxyServer;
        private int proxyPort;
        private string proxyUsername;
        private string proxyPassword;

        private int retries;


        public HTTPWrapper(Cookie sharedCookie) {
            UseGZip = true;
            UseProxy = false;
            lastReq = new LastRequest();
            cookies = sharedCookie;
            retries = DEFAULT_RETRIES; 
        }

        public void setProxy(string server, int port, string username, string password)
        {
            proxyServer = server;
            proxyPort = port;
            proxyUsername = username;
            proxyPassword = password;
        }

        public int Retries
        {
            get { return retries; }
            set {
                if (value < MIN_RETRIES)
                    retries = MIN_RETRIES;
                else if (value > MAX_RETRIES)
                    retries = MAX_RETRIES;
                else
                    retries = value;
            }
        }

        public string GET(string url)
        {
            return Navigate("GET", url, "", null, true);
        }

        public string GET(string url, string referer)
        {
            return Navigate("GET", url, "", referer, true);
        }

        public string POST(string url, PairedData pData)
        {
            return Navigate("POST", url, pData.ToPOSTStr(), null, true);
        }

        public string POST(string url, PairedData pData, string referer)
        {
            return Navigate("POST", url, pData.ToPOSTStr(), referer, true);
        }

        public string POST(string url, string pData, string referer)
        {
            return Navigate("POST", url, pData, referer, true);
        }

        public string POST(string url, string pData)
        {
            return Navigate("POST", url, pData, null, true);
        }

        public System.Drawing.Bitmap GetImage(string url, string referer)
        {
            string source = Navigate("GET", url, "", referer, false);
            MemoryStream mStream = new MemoryStream(Encoding.Default.GetBytes(source));
            return new System.Drawing.Bitmap(mStream);
        }

        public string Refresh()
        {
            return Navigate(lastReq.RequestType, lastReq.Url, lastReq.Data, lastReq.Referer, true);
        }

        private string Navigate(string request, string url, string pData, string referer, bool returnHeader)
        {
            string rawHTML, server, page;
            int retriesLeft = retries;

            //Get the referer if nothing's provided
            if ((referer == null) || (referer == ""))
                if (lastReq.RequestType == "POST")
                    referer = lastReq.Url + "?" + lastReq.Data;
                else
                    referer = lastReq.Url;

            //Remove the "http://" part from the url
            url = url.Replace("http://", "");

            //Parse for the server & page info
            if (url.Contains("/"))
            {
                int i = url.IndexOf("/");
                server = url.Substring(0, i);
                page = url.Substring(i, url.Length - i);
            }
            else
            {
                server = url;
                page = "/";
            }

            TcpClient HTTPClient = new TcpClient();

            string Final_Server;
            int Final_Port;

            //Proxy Support
            if (UseProxy)
            {
                Final_Server = proxyServer;
                Final_Port = proxyPort;
            }
            else
            {
                Final_Server = server;
                Final_Port = 80;
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
            } while (retriesLeft != 0 && !HTTPClient.Connected);

            if (!HTTPClient.Connected)
            {
                throw new Exception("Error: Could not connect to server");
            }

            NetworkStream HTTPStream = HTTPClient.GetStream();
            HTTPStream.WriteTimeout = HTTPStream.ReadTimeout = DEFAULT_TIMEOUT; 

            try
            {
                //Send the data
                StreamWriter sw = new StreamWriter(HTTPStream);
                lastReq.DataSent = CreateHeader(request, server, page, cookies.BuildCookies(), referer, pData);
                sw.Write(lastReq.DataSent);
                sw.Flush();

                //Capture all data
                StreamReader sr = new StreamReader(HTTPStream, Encoding.Default);
                rawHTML = sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new Exception("Error: Could not write/read from network stream. Original error: " + ex.Message);
            }

            HTTPClient.Close();

            if (string.IsNullOrEmpty(rawHTML))
            {
                throw new Exception("Error: Failed to recieve data from server.");
            }

            int Separator = rawHTML.IndexOf("\r\n\r\n") + 4;
            lastReq.HeaderReceived = rawHTML.Substring(0, Separator);
            lastReq.RequestType = request;
            lastReq.Url = "http://" + url;
            lastReq.Data = pData;
            lastReq.Referer = referer;

            cookies.WriteCookies(lastReq.HeaderReceived);

            string processedBody = rawHTML.Substring(Separator);

            if (lastReq.HeaderReceived.Contains("Content-Encoding: gzip"))
                processedBody = DecompressGZip(processedBody);

            lastReq.BodyRecieved = processedBody;

            if (returnHeader)
                return string.Format("{0}{1}", lastReq.HeaderReceived, processedBody);
            else
                return processedBody;
        }

        string CreateHeader(string HTTPRequest, string Server, string Page, string Cookies, string Referer, string Data)
        {
            StringBuilder output = new StringBuilder();

            if (UseProxy)
                Page = "http://" + Server + Page;
              
            output.Append(string.Format("{0} {1} HTTP/1.1\r\nHost: {2}\r\n", HTTPRequest, Page, Server));

            if (UseProxy && !string.IsNullOrEmpty(proxyUsername) && !string.IsNullOrEmpty(proxyPassword))
            {
                string auth_string = base64Encode(string.Format("{0}:{1}", proxyUsername, proxyPassword));
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
