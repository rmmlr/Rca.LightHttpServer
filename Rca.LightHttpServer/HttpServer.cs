using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Rca.LightHttpServer
{
    public abstract class HttpServer
    {
        #region Member
        protected IPAddress m_IpAddress;
        protected int m_Port;
        TcpListener m_TcpListener;
        bool m_IsActive = true;

        #endregion Member

        #region Constructor
        /// <summary>
        /// Default constructor of <seealso cref="HttpServer"/>
        /// </summary>
        /// <param name="ip">IP address of the HTTP server</param>
        /// <param name="port">Port of the HTTP server</param>
        public HttpServer(IPAddress ip, int port)
        {
            this.m_IpAddress = ip;
            this.m_Port = port;
        }

        #endregion Constructor

        #region Services
        public void Listen()
        {
            m_TcpListener = new TcpListener(m_IpAddress, m_Port);
            m_TcpListener.Start();
            while (m_IsActive)
            {
                var s = m_TcpListener.AcceptTcpClient();
                var processor = new HttpProcessor(s, this);
                processor.HttpRequest += HttpRequest;
                processor.HttpProcessorError += HttpProcessorError;
                var thread = new Thread(new ThreadStart(processor.Process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public abstract void HandleGetRequest(HttpProcessor p);
        public abstract void HandlePostRequest(HttpProcessor p, StreamReader inputData);

        #endregion Services

        #region Events

        public event HttpRequestEventHandler HttpRequest;
        public event HttpProcessorErrorEventHandler HttpProcessorError;
        #endregion Events
    }
}
