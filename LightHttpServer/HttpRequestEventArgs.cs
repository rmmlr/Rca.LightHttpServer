using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rca.LightHttpServer
{
    public class HttpRequestEventArgs : EventArgs
    {
        #region Member


        #endregion Member

        #region Properties
        public string Message { get; set; }

        #endregion Properties

        #region Constructor
        /// <summary>
        /// Empty constructor for HttpRequestEventArgs
        /// </summary>
        public HttpRequestEventArgs(string message)
        {
            Message = message;
        }

        #endregion Constructor

        #region Services


        #endregion Services

        #region Internal services


        #endregion Internal services

        #region Events


        #endregion Events
    }
}
