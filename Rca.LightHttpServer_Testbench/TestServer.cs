using Rca.LightHttpServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Rca.LightHttpServer_Testbench
{
    public class TestServer : HttpServer
    {
        #region Constructor
        /// <summary>
        /// Empty constructor of <seealso cref="TestServer"/>
        /// </summary>
        public TestServer(IPAddress ip, int port) : base(ip, port)
        {

        }

        public override void HandleGetRequest(HttpProcessor p)
        {
            throw new NotImplementedException();
        }

        public override void HandlePostRequest(HttpProcessor p, StreamReader inputData)
        {
            throw new NotImplementedException();
        }

        #endregion Constructor

    }
}
