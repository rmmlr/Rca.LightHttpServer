using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web;

namespace Rca.LightHttpServer
{
    public class HttpProcessor
    {
        #region Constants
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB
        private const int BUF_SIZE = 4096;

        #endregion region Constants

        #region Member
        private Stream m_InputStream;

        #endregion Member

        #region Properties
        public TcpClient Socket { get; set; }
        public HttpServer Srv { get; set; }
        public StreamWriter OutputStream { get; set; }
        public string HttpMethod { get; set; }
        public string HttpUrl { get; set; }
        public string HttpProtocolVersionString { get; set; }
        public Hashtable HttpHeaders { get; set; } = new Hashtable();
        public HttpCookieCollection HttpCookies { get; set; }
        public IPAddress RemoteIP
        {
            get
            {
                if (Socket.Client.RemoteEndPoint.GetType() == typeof(IPEndPoint))
                    return ((IPEndPoint)Socket.Client.RemoteEndPoint).Address;
                else
                    return IPAddress.None;
            }
        }

        #endregion Properties

        #region Constructor
        /// <summary>
        /// Default constructor of <seealso cref="HttpProcessor"/>
        /// </summary>
        /// <param name="socket">Socket</param>
        /// <param name="srv">HTTP server</param>
        public HttpProcessor(TcpClient socket, HttpServer srv)
        {
            Socket = socket;
            Srv = srv;
        }

        #endregion Constructor

        #region Services
        public void Process()
        {
            m_InputStream = new BufferedStream(Socket.GetStream());

            OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream()));
            try
            {
                ParseRequest();
                ReadHeaders();
                if (HttpMethod.Equals("GET"))
                    HandleGetRequest();
                else if (HttpMethod.Equals("POST"))
                    HandlePostRequest();
            }
            catch (Exception ex)
            {
                HttpProcessorErrorEvent(ex);
                WriteFailure();
            }

            try
            {
                OutputStream.Flush();
            }
            catch (Exception ex)
            {
                HttpProcessorErrorEvent(ex);
            }
            finally
            {
                m_InputStream = null; OutputStream = null;           
                Socket.Close();
            }
        }

        public void ParseRequest()
        {
            var request = StreamReadLine(m_InputStream);
            var tokens = request.Split(' ');
            if (tokens.Length != 3)
                throw new Exception("invalid http request line");

            HttpMethod = tokens[0].ToUpper();
            HttpUrl = tokens[1];
            HttpProtocolVersionString = tokens[2];

            HttpRequestEvent("starting: " + request);
        }

        public void ReadHeaders()
        {
            HttpRequestEvent("Started: Reading headers");
            string line;

            while ((line = StreamReadLine(m_InputStream)) != null)
            {
                if (line.Equals(""))
                {
                    HttpRequestEvent("Finished: Reading headers");
                    return;
                }

                if (line.StartsWith("Cookie:"))
                {
                    string cookieLine = line.Replace(" ", "");

                    try
                    {
                        HttpCookies = new HttpCookieCollection();

                        var query = cookieLine.Substring(7).Split(';');
                        foreach (string p in query)
                            HttpCookies.Add(new HttpCookie(p.Split('=').First(), p.Split('=').Last()));
                    }
                    catch (Exception)
                    {
                        throw new Exception("invalid http header cookie line: " + line);
                    }
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                    throw new Exception("invalid http header line: " + line);

                var name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                    pos++;

                var value = line.Substring(pos, line.Length - pos);
                HttpRequestEvent(string.Format("header: {0}:{1}", name, value));
                HttpHeaders[name] = value;
            }
        }

        public void HandleGetRequest()
        {
            Srv.HandleGetRequest(this);
        }

        public void HandlePostRequest()
        {
            HttpRequestEvent("Started: Get POST data");
            int contentLength = 0;
            var memStream = new MemoryStream();

            if (HttpHeaders.ContainsKey("Content-Length"))
            {
                contentLength = Convert.ToInt32(this.HttpHeaders["Content-Length"]);
                if (contentLength > MAX_POST_SIZE)
                    throw new Exception(String.Format("POST Content-Length({0}) to large for this server", contentLength));

                byte[] buf = new byte[BUF_SIZE];
                int toRead = contentLength;
                while (toRead > 0)
                {
                    HttpRequestEvent("starting Read, toRead=" + toRead);

                    int numRead = this.m_InputStream.Read(buf, 0, Math.Min(BUF_SIZE, toRead));
                    HttpRequestEvent("read finished, numread=" + numRead);
                    if (numRead == 0)
                    {
                        if (toRead == 0)
                            break;
                        else
                            throw new Exception("client disconnected during post");
                    }
                    toRead -= numRead;
                    memStream.Write(buf, 0, numRead);
                }
                memStream.Seek(0, SeekOrigin.Begin);
            }
            HttpRequestEvent("Finished: Get POST data");
            Srv.HandlePostRequest(this, new StreamReader(memStream));
        }

        public void WriteSuccess()
        {
            WriteSuccess("text/html");
        }

        public void WriteSuccess(HttpCookie cookie)
        {
            WriteSuccess("text/html", cookie);
        }

        public void WriteSuccess(string contentType, HttpCookie cookie)
        {
            var cookies = new HttpCookieCollection();
            cookies.Add(cookie);

            WriteSuccess(contentType, cookies);
        }

        public void WriteSuccess(string contentType, HttpCookieCollection cookies = null)
        {
            OutputStream.WriteLine("HTTP/1.0 200 OK");
            OutputStream.WriteLine("Content-Type: " + contentType);
            if (cookies != null)
            {
                for (int i = 0; i < cookies.Count; i++)
                {
                    if (cookies[i].Expires < DateTime.Now.AddMinutes(5)) //lifetime under 5 minutes -> session cookie
                        OutputStream.WriteLine("Set-Cookie: {0}={1}; HttpOnly", cookies[i].Name, cookies[i].Value);
                    else
                        OutputStream.WriteLine("Set-Cookie: {0}={1}; Expires={2}; HttpOnly", cookies[i].Name, cookies[i].Value, cookies[i].Expires.ToUniversalTime().ToString("r"));
                }
            }
            OutputStream.WriteLine("Connection: close");
            OutputStream.WriteLine("");
        }

        public void WriteFailure()
        {
            OutputStream.WriteLine("HTTP/1.0 404 File not found");
            OutputStream.WriteLine("Content-Type: text/html");
            OutputStream.WriteLine("Connection: close");
            OutputStream.WriteLine("");
        }

        #endregion Services

        #region Internal services
        private string StreamReadLine(Stream inputStream)
        {
            int nextChar;
            string data = "";
            while (true) //loop
            {
                nextChar = inputStream.ReadByte();
                if (nextChar == '\n')
                    break;
                if (nextChar == '\r')
                    continue;
                if (nextChar == -1)
                {
                    Thread.Sleep(1);
                    continue;
                };
                data += Convert.ToChar(nextChar);
            }
            return data;
        }

        #endregion Internal services

        #region Events

        protected void HttpRequestEvent(string message)
        {
            var e = new HttpRequestEventArgs(message);
            HttpRequest?.Invoke(null, e);
        }
        public event HttpRequestEventHandler HttpRequest;


        protected void HttpProcessorErrorEvent(Exception ex, string message = null)
        {
            var e = new HttpProcessorErrorEventArgs(ex, message);
            HttpProcessorError?.Invoke(null, e);
        }
        public event HttpProcessorErrorEventHandler HttpProcessorError;

        #endregion Events
    }

    public delegate void HttpRequestEventHandler(object sender, HttpRequestEventArgs e);
    public delegate void HttpProcessorErrorEventHandler(object sender, HttpProcessorErrorEventArgs e);
}
