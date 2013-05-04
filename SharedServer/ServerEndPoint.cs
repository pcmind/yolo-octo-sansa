using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedInterface;

namespace SharedServer
{
    class ServerEndPoint
    {
        private static readonly int MAX_RETRYS = 3;
        private readonly string _name;
        private readonly Uri uri;
        private int retryErrors = 0;
        private volatile Servidor servidor = null;
        private volatile ServerEndPointStatus _status = ServerEndPointStatus.UNKNOWN;
        public ServerEndPointStatus Status
        { 
            get{ return _status; }
            set{ _status = value; }
        }

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

        public bool Execute(Action<Servidor> exec)
        {
            if (Status == ServerEndPointStatus.OFFLINE || Status == ServerEndPointStatus.DEAD) return false;
            //Console.WriteLine("Trying to contact with server: " + this.Name);
            try
            {
                Servidor s = this.GetServer();
                if (s != null)
                {
                    exec(s);
                    return true;
                }
            }
            catch
            {
                this.Status = ServerEndPointStatus.UNKNOWN;
            }
            return false;
        }
        private Servidor GetServer()
        {
            
            //depois de MAX_RETRYS consideramos o servidor offline
            if (Status == ServerEndPointStatus.OFFLINE || Status == ServerEndPointStatus.DEAD) return null;
            
            //se aparentemente o servido está vivo retornamos a instancia já obtida anteriormente
            if (Status == ServerEndPointStatus.ALIVE && servidor != null) return servidor;

            int r = MAX_RETRYS;
            while (r > 0)
            {
                try
                {
                    servidor = (Servidor)Activator.GetObject(
                       typeof(IPublicServer),
                       this.ToString());
                    this.Status = ServerEndPointStatus.ALIVE;
                    this.retryErrors = 0;
                    return servidor;
                }
                catch (Exception)
                {
                    if (++retryErrors > 0)
                    {
                        Status = ServerEndPointStatus.DEAD;
                    }
                    else
                    {
                        Status = ServerEndPointStatus.UNKNOWN;
                    }
                }
            }
            return null;
        }
    }
    enum ServerEndPointStatus
    {
        UNKNOWN,    //ainda nao foi testado
        ALIVE,      //parece estar vivo
        OFFLINE,    //houve um pedido explicito
        DEAD        //parece estar morto
    }
}
