using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rca.LightHttpServer
{
    public class HttpProcessorErrorEventArgs : EventArgs
    {
        #region Member


        #endregion Member

        #region Properties

        public Exception Exception { get; set; }
        public string Message { get; set; }

        #endregion Properties

        #region Constructor
        /// <summary>
        /// Empty constructor for HttpProcessorErrorEventArgs
        /// </summary>
        public HttpProcessorErrorEventArgs(Exception ex, string message)
        {
            Exception = ex;
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
