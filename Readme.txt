Description: Can fetch/navigate webpages using GET/POST methods over HTTP

Files:
Utilities.cs - Lots of little functions that are useful for extracting information from webpages
HTTPWrapper.cs - Creates a TCP socket and sends your get/post requests with proper headers
Cookie.cs - Handles building/parsing of cookies. This object is safe to use with multithreading.
LastRequest.cs - a struct that holds info regarding the last webpage you visited. Useful when calling the refresh method
PairedData.cs - Used to organize POST and cookies data<br>

Example Usage<br>

/***Creating the object***/
HTTPWrapper http = new HTTPWrapper(new Cookie());

/****Using GET****/

string html;
html = http.GET("www.pebblabs.com"); //Automatically adds a referer 
html = http.GET("www.pebblabs.com", "www.google.com/somepage"); //where "google.com/somepage" is the referer

/****Using POST****/
PairedData pData = new PairedData();
pData.add("login", "username");
pData.add("password","123");

html = http.POST("www.somewebserver.com/login.php", pData);
//OR
html = http.POST("www.somewebserver.com/login.php", pData, yourReferer);

/****Using Proxy****/
http.SetProxy(proxyServer, proxtPort, "", ""); //if NO authentication is required
//OR
http.SetProxy(proxyServer, proxtPort, username, password); //if authentication is required
http.UseProxy = true;
http.GET("www.google.com"); //Use GET or POST methods like normal

/***Refreshing a webpage***/
http.GET("www.google.com");
http.Refresh();

/***If using multiple HTTPWrapper objects on a single webserver, make sure you share the Cookie object between them.***/
Cookie myCookie = new Cookie();
HTTPWrapper http_1 = new HTTPWrapper(myCookie); 
HTTPWrapper http_2 = new HTTPWrapper(myCookie); 
//...etc
//This way if http_1 logs into the website, then http_2 will also be able to access it. Btw, the Cookie object is threadsafe,so you can have http_1 and http_2 working in thier own threads while sharing the same cookie.


