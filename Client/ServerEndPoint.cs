using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client
{
    class ServerEndPoint
    {
        private readonly string _name;
        private readonly Uri uri;

        public string Server { get { return uri.Host; } }
        public int Port { get { return uri.Port; } }
        public string Name { get { return _name; } }

        public Uri Uri { get { return uri; } }
    
        public int Id
        {
            get { return Server.GetHashCode() + Port; }
        }
        public ServerEndPoint(string name, string url)
        {
            uri = new Uri(url);
            _name = name;
        }
        public override string ToString()
        {
            return uri.ToString();
        }
    }
}
