using Rca.LightHttpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rca.LightHttpServer_Testbench
{
    class Program
    {
        static void Main(string[] args)
        {
            IPAddress ip;
            int port;
            try
            {
                ip = IPAddress.Parse(Properties.Settings.Default.LocalServerIp);
                port = Properties.Settings.Default.LocalServerPort > 0 ? Properties.Settings.Default.LocalServerPort : throw new ArgumentException("Port must be greater than 0!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Invalid configuration!");
                Console.WriteLine(ex);
                Console.ReadKey();
                return;
            }

            HttpServer httpServer = new TestServer(ip, port);

            httpServer.HttpRequest += HttpServer_HttpRequest;
            httpServer.HttpProcessorError += HttpServer_HttpProcessorError;

            Thread thread = new Thread(new ThreadStart(httpServer.Listen));
            thread.Start();


        }

        private static void HttpServer_HttpProcessorError(object sender, HttpProcessorErrorEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Exception);
            Console.ResetColor();
        }

        private static void HttpServer_HttpRequest(object sender, HttpRequestEventArgs e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
