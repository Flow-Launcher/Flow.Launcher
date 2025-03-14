using System;
using System.IO;
using System.Net;

namespace Flow.Launcher.Helper;

public class DataWebRequestFactory : IWebRequestCreate
{
    class DataWebRequest : WebRequest
    {
        private readonly Uri _uri;

        public DataWebRequest(Uri uri)
        {
            _uri = uri;
        }

        public override WebResponse GetResponse()
        {
            return new DataWebResponse(_uri);
        }
    }

    class DataWebResponse : WebResponse
    {
        private readonly string _contentType;
        private readonly byte[] _data;

        public DataWebResponse(Uri uri)
        {
            string uriString = uri.AbsoluteUri;

            int commaIndex = uriString.IndexOf(',');
            var headers = uriString.Substring(0, commaIndex).Split(';');
            _contentType = headers[0];
            string dataString = uriString.Substring(commaIndex + 1);
            _data = Convert.FromBase64String(dataString);
        }

        public override string ContentType
        {
            get { return _contentType; }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long ContentLength
        {
            get { return _data.Length; }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override Stream GetResponseStream()
        {
            return new MemoryStream(_data);
        }
    }

    public WebRequest Create(Uri uri)
    {
        return new DataWebRequest(uri);
    }
}
