using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        public String HttpMethod { get; set; }
        public String HttpUrl { get; set; }
        public String HttpProtocolVersionString { get; set; }
        public Hashtable HttpHeaders { get; set; } = new Hashtable();
        public HttpCookieCollection HttpCookies { get; set; }

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
                {
                    HandleGetRequest();
                }
                else if (HttpMethod.Equals("POST"))
                {
                    HandlePostRequest();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.ToString());
                WriteFailure();
            }

            try
            {
                OutputStream.Flush();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception: " + ex.ToString());
                Console.ResetColor();
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

            Console.WriteLine("starting: " + request);
        }

        public void ReadHeaders()
        {
            Console.WriteLine("readHeaders()");
            String line;

            while ((line = StreamReadLine(m_InputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
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
                        {
                            HttpCookies.Add(new HttpCookie(p.Split('=').First(), p.Split('=').Last()));
                        }
                    }
                    catch (Exception)
                    {
                        throw new Exception("invalid http header cookie line: " + line);
                    }
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }

                var name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                    pos++; // strip any spaces

                var value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                HttpHeaders[name] = value;
            }
        }

        public void HandleGetRequest()
        {
            Srv.HandleGetRequest(this);
        }

        public void HandlePostRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            var ms = new MemoryStream();

            if (this.HttpHeaders.ContainsKey("Content-Length"))
            {
                content_len = Convert.ToInt32(this.HttpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                    throw new Exception(String.Format("POST Content-Length({0}) too big for this simple server", content_len));

                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", to_read);

                    int numread = this.m_InputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Console.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                            break;
                        else
                            throw new Exception("client disconnected during post");
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            Srv.HandlePostRequest(this, new StreamReader(ms));

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
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n')
                    break;
                if (next_char == '\r')
                    continue;
                if (next_char == -1)
                {
                    Thread.Sleep(1);
                    continue;
                };
                data += Convert.ToChar(next_char);
            }
            return data;
        }

        #endregion Internal services
    }
}
