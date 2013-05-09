using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedInterface;

namespace SharedServer {
    public class Servidor : MarshalByRefObject, IPrivateServer {
        private static readonly bool DEBUG = true;
        internal static ServerEndPoint meuendpoint = null;
        private static Dictionary<string, ServerEndPoint> servidores = new Dictionary<string, ServerEndPoint>();
        private static ServerDictionary db = new ServerDictionary();
        private static readonly ReaderWriterLock _lock = new ReaderWriterLock();

        public Servidor() {
            //dados para cada servidor ter algumas chaves dele proprio
            db.TryAddValue(new MyKey(meuendpoint.Name, 1).HashString, new Valor(meuendpoint.Name, new int[] { 1 }));
            db.TryAddValue(new MyKey(meuendpoint.Name, 2).HashString, new Valor(meuendpoint.Name, new int[] { 2 }));
            db.TryAddValue(new MyKey(meuendpoint.Name, 3).HashString, new Valor(meuendpoint.Name, new int[] { 3 }));
        }

        // ========================================================================
        //          SERVIDORES
        // ========================================================================
        private bool GetAndCacheFromRemote(IKey key, out VersionableValue value) {
            List<Action> actions = new List<Action>();
            foreach (ServerEndPoint ep in servidores.Values)
                actions.Add(() => ep.Execute((x) => {
                    VersionableValue v = x.SrvGetLocalValue(key.HashString, meuendpoint.Name);
                    if (v != null)
                        db.TryAddValue(key, v, ep);
                }));
            AsyncCallAndWait(actions);
            return db.TryGetVersionValue(key, out value);
        }

        public bool SrvUpdateValue(string key, VersionableValue value) {
            debug("SrvUpdateValue a actualisar chave: " + key);
            ValueHolder v;
            if (db.TryGetValue(key, out v)) {
                if (v.IsLocal()) {
                    if (db.TryUpdValue(key, value)) {
                        DeleteFromRemoteClients(key);
                        return true;
                    }
                }
            }
            return false;
        }

        private void DeleteFromRemoteClients(string key) {
            //optimistic, So existe lista de cliente se a cahve for local
            foreach (ServerEndPoint ep in db.TryGetClients(key))
                AsyncCallNoWait(() => ep.Execute((x) => x.SrvDeleteValue(key)));
        }

        public void SrvDeleteValue(string key) {
            debug("SrvDeleteValue: a remover chave: " + key);
            ValueHolder v;
            if (!db.TryDelValue(key, out v))
                return;
            DeleteFromRemoteClients(key); //garantido que so apaga se for local
        }
        
        public bool SrvSetNewOwner(string key, string new_owner) {
            ServerEndPoint serv;
            if (new_owner == meuendpoint.Name) {
                serv = null;
            }
            else if (!servidores.TryGetValue(new_owner, out serv)) {
                debug("SetNewOwner: novo dono desconhecido");
                return false;
            }

            if (!db.TryChown(key, serv)) {
                debug("SetNewOwner: nao tenho esta chave");
                return false;
            }
            debug("SetNewOwner: a alterar para novo dono");
            return true;
        }
        
        public VersionableValue SrvGetLocalValue(string key, string servername) {
            VersionableValue v;
            ServerEndPoint serv;
            debug("GetLocalKey: A solicitar chave local: " + key);

            if (!servidores.TryGetValue(servername, out serv)) {
                debug("GetLocalKey: servidor cliente nao encontrado");
                return null;
            }

            if (!db.TryGetVersionValue(key, out v, serv)) {
                debug("GetLocalKey: Chave nao existe " + key);
                return null;
            }

            return v;

        }
        
        public void SrvAlive(string server_name) {
            debug("SrvAlive: servidor " + server_name + " ligado");
            ServerEndPoint s;
            if (servidores.TryGetValue(server_name, out s)) {
                s.Status = ServerEndPointStatus.ALIVE;
            }
        }
        
        public void SrvShutdown(string server_name) {
            debug("SrvAlive: servidor " + server_name + " a desligar");
            ServerEndPoint s;
            if (servidores.TryGetValue(server_name, out s)) {
                s.Status = ServerEndPointStatus.OFFLINE;
            }
        }

        // ========================================================================
        //          CLIENTES
        // ========================================================================
        public void storePair(IKey key, IValue value) {
            VersionableValue v;
            if (db.TryGetVersionValue(key, out v) || GetAndCacheFromRemote(key, out v)) {
                updatePair(key, value);
            }
            else {
                debug("A criar um pare novo com chave: " + key.HashString);
                db.TryAddValue(key, value);
            }
        }

        public IValue readPair(IKey key) {
            db.unloadStore();
            if (DEBUG)
                Console.WriteLine("A a ler a chave: " + key.HashString);
            VersionableValue value;
            if (db.TryGetVersionValue(key, out value)) {
                debug("Chave local");
                return value.Value;
            }
            if (GetAndCacheFromRemote(key, out value)) {
                debug("Chave remota");
                return value.Value;
            }
            return null;
        }

        public void updatePair(IKey key, IValue newValue) {
            VersionableValue v_v;
            debug("updatePair: A actualizar a chave: " + key.HashString);
            if (db.TryGetVersionValue(key, out v_v) || GetAndCacheFromRemote(key, out v_v)) {
                ValueHolder v;
                v_v = new VersionableValue(v_v, newValue);
                if (db.TryUpdValue(key, new VersionableValue(v_v)) && db.TryGetValue(key, out v)) {
                    if (v.IsLocal()) {
                        DeleteFromRemoteClients(key.HashString);
                    }
                    else {
                        debug("updatePair: chave remota");
                        //TODO: se o servidor falar, alterar o Owner para local.
                        AsyncCallNoWait(() => v.Owner.Execute((x) => x.SrvUpdateValue(key.HashString, v_v)));
                    }
                }
            }
            else {
                debug("Chave nao encontrada");
            }
        }
        
        public void deletePair(IKey key) {
            debug("A apagar a chave: " + key.HashString);
            VersionableValue v_v;
            if (db.TryGetVersionValue(key, out v_v) || GetAndCacheFromRemote(key, out v_v)) {
                ValueHolder v;
                List<Action> actions = new List<Action>();
                if (db.TryDelValue(key, out v)) {
                    if (v.IsLocal()) {
                        DeleteFromRemoteClients(key.HashString);
                    }
                    else {
                        actions.Add(() => v.Owner.Execute((x) => x.SrvDeleteValue(key.HashString)));
                    }
                }
                AsyncCallAndWait(actions);
            }
        }
        
        public void sayHelllo() {
            Console.WriteLine("hello");
        }

        public static void AsyncCallAndWait(List<Action> actions) {
            List<IAsyncResult> result = new List<IAsyncResult>();
            foreach (Action action in actions) {
                result.Add(action.BeginInvoke(null, null));
            }
            for (int i = 0; i < actions.Count; i++)
                actions.ElementAt(i).EndInvoke(result.ElementAt(i));
        }

        public static bool AsyncCallAndWait(Action action) {
            IAsyncResult result = action.BeginInvoke(null, null);
            try {
                action.EndInvoke(result);
            }
            catch (Exception) {
                return false;
            }
            return true;
        }

        public static void AsyncCallNoWait(Action action) {
            action.BeginInvoke(null, null);
        }

        private static void backupServer(string name) {
            db.SerializeDB(name + ".xml");
        }

        private static void restoreServer(string name) {
            db.DeserializeDB(name + ".xml");
        }

        public static void debug(Action action) {
            if (DEBUG)
                action();
        }

        public static void debug(string msg) {
            if (DEBUG)
                Console.WriteLine(msg);
        }

        static void Main(string[] args) {
            if (args.Length != 1) {
                Console.Error.WriteLine("Falta defenir o nome do servidor");
                System.Environment.Exit(1);
            }
            string nome_servidor = args[0];
            if (DEBUG) {
                foreach (Object seting in ConfigurationSettings.AppSettings) {
                    Console.WriteLine(seting);
                }
            }
            //carregar tabela com links para servidores
            foreach (DictionaryEntry server in (Hashtable)ConfigurationSettings.GetConfig("servidores")) {
                ServerEndPoint ep = new ServerEndPoint((string)server.Key, (string)server.Value);
                if (ep.Name == nome_servidor) {
                    //current endpoint 
                    meuendpoint = ep;
                }
                else {
                    servidores.Add(ep.Name, ep);
                }
            }
            if (meuendpoint == null) {
                Console.Error.WriteLine("Nao foi encontrado configurações validas para o servidor " + nome_servidor);
            }
            else {
                IServerChannelSinkProvider serverProv;
                IClientChannelSinkProvider clientProv;
                IChannel hch;
                IDictionary props = new Hashtable();
                props["port"] = meuendpoint.Port;
                switch (meuendpoint.Uri.Scheme) {
                    case "http":
                    case "https":
                        // Creating a custom formatter for a HttpChannel sink chain.
                        serverProv = new SoapServerFormatterSinkProvider();
                        ((SoapServerFormatterSinkProvider)serverProv).TypeFilterLevel =
                            System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
                        clientProv = new SoapClientFormatterSinkProvider();
                        hch = new HttpChannel(props, clientProv, serverProv);

                        break;
                    case "tcp":
                        // Creating a custom formatter for a TcpChannel sink chain.
                        serverProv = new BinaryServerFormatterSinkProvider();
                        ((BinaryServerFormatterSinkProvider)serverProv).TypeFilterLevel =
                            System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;
                        clientProv = new BinaryClientFormatterSinkProvider();
                        hch = new TcpChannel(props, clientProv, serverProv);
                        break;
                    default:
                        throw new Exception("Tipo de url nao implementado");
                }

                ChannelServices.RegisterChannel(hch, false);
                RemotingConfiguration.RegisterWellKnownServiceType(
                    typeof(Servidor),
                    meuendpoint.Uri.PathAndQuery.Substring(1),
                    WellKnownObjectMode.Singleton
                    );


                Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++");
                Console.WriteLine("Servidor: " + meuendpoint.Name);
                Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++");
                debug("A arrancar servidor: " + meuendpoint);
                debug("http port: " + meuendpoint.Port);
                debug("tcp port:  " + (meuendpoint.Port + 10000));
                debug("endpoint:  " + meuendpoint.Uri.PathAndQuery.Substring(1));

                Thread.Sleep(100);

                foreach (ServerEndPoint ep in servidores.Values) {
                    AsyncCallNoWait(() => {
                        if (!ep.Execute((x) => x.SrvAlive(meuendpoint.Name)))
                            ep.Status = ServerEndPointStatus.OFFLINE;
                    });
                }

                do {
                    Console.Write("Accao pretendida: ");
                    string cmd = Console.ReadLine();
                    switch (cmd) {
                        case "shutdown":
                            Console.WriteLine("Prima enter para terminar o servidor!");
                            Console.ReadLine();
                            return;

                        case "backup":
                            Console.WriteLine("Saving....");
                            backupServer(nome_servidor);
                            break;

                        case "restore":
                            Console.WriteLine("Loading....");
                            restoreServer(nome_servidor);
                            break;

                        default:
                            Console.WriteLine("Commando invalido");
                            Console.WriteLine("Commandos possiveis: shutdown, backup, restore");
                            break;
                    }
                } while (true);
            }
        }
    }
}
