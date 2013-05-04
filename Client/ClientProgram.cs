using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Configuration;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using SharedInterface;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters;

namespace Client
{
    class ClientProgram
    {
        private static readonly bool DEBUG = true;
        private static Hashtable servidores = new Hashtable();

        static void Main(string[] args)
        {
            ServerEndPoint servidor;
            //carregar tabela com links para servidores
            foreach (DictionaryEntry server in (Hashtable)ConfigurationSettings.GetConfig("servidores"))
                servidores.Add((string)server.Key, new ServerEndPoint((string)server.Key, (string)server.Value));

            if (args.Length != 1)
            {
                Random r = new Random();
                Console.WriteLine("A que servidor se pretende ligar?:");

                foreach(ServerEndPoint sp in servidores.Values)
                    Console.WriteLine(sp.Name);
                
                servidor = null;
                do
                {
                    string serv = Console.ReadLine();
                    servidor = (ServerEndPoint) servidores[serv];
                } while (servidor == null);
            }
            else
            {
                servidor = (ServerEndPoint)servidores[args[0]];
            }
            Console.WriteLine("A ligar ao servidor " + servidor.Name + " : " + servidor);
            IServerChannelSinkProvider serverProv;
            IClientChannelSinkProvider clientProv;
            IChannel hch;
            IDictionary props = new Hashtable();
            props["port"] = 0;
            switch (servidor.Uri.Scheme)
            {
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
            IPublicServer robj = (IPublicServer)Activator.GetObject(
               typeof(IPublicServer),
               servidor.ToString());

            StartClientConsole(robj);

            //testar obter uma chave de outro servidores
            if (DEBUG)
            {
                robj.sayHelllo();
                Valor v;
/*
                //teste single server
                IKey key = new MyKey("servidor1 key1", 10);
                IValue value = new Valor("servidor1 valor1", new int[] { 10, 10 });


                Console.WriteLine("Soring key1");
                robj.storePair(key, value);

                Console.WriteLine("Geting key1");
                v = (Valor) robj.readPair(key);


                Console.WriteLine("Deleting key1");
                robj.deletePair(key);

                Console.WriteLine("Soring key1");
                v = (Valor)robj.readPair(key);
                if (v != null) Console.Error.WriteLine("Obtido valor calor inesperado de key1");
            

                Console.WriteLine("geting key from server1");
                v = (Valor)robj.readPair(new MyKey("servidor1", 1));
                if (v != null)
                {
                    Console.WriteLine("obtida com sucesso");
                }
                else
                {
                    Console.WriteLine("obtida sem sucesso");
                }
                Console.WriteLine("geting key from server2");
                v = (Valor)robj.readPair(new MyKey("servidor2", 1));
                if (v != null)
                {
                    Console.WriteLine("obtida com sucesso");
                }
                else
                {
                    Console.WriteLine("obtida sem sucesso");
                }

                Console.WriteLine("geting key from server3");
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v != null)
                {
                    Console.WriteLine("obtida com sucesso");
                }
                else
                {
                    Console.WriteLine("obtida sem sucesso");
                }
                Console.WriteLine("A obter 3 veses a mesma chave remota");
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v == null) Console.WriteLine("erro");
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v == null) Console.WriteLine("erro");
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v == null) Console.WriteLine("erro");
                Console.WriteLine("fim das 3 obtencoes");
                */
                Console.WriteLine("Tentar atualizar um valor remoto");
                robj.sayHelllo();
                Thread.Sleep(1000);
                robj.readPair(new MyKey("servidor3", 1));
                robj.updatePair(new MyKey("servidor3", 1), new Valor("teste 3", new int[] { 10, 10 }));
                Thread.Sleep(100);
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v == null) Console.WriteLine("erro");

                robj.deletePair(new MyKey("servidor3", 1));
                v = (Valor)robj.readPair(new MyKey("servidor3", 1));
                if (v != null) Console.WriteLine("erro");

            }


            Console.WriteLine("get completed");
            Console.ReadLine();
        }

        private static string ConsoleReadKey()
        {
            string k;
            do
            {
                Console.Write("Chave: ");
                k = Console.ReadLine();
                if (k != null && k.Length > 0) return k;
                Console.WriteLine("A chave deve estar preenchida!");
            } while (true);
        }
        private static string ConsoleReadValue()
        {
            string k;
            do
            {
                Console.Write("Valor(str): ");
                k = Console.ReadLine();
                if (k != null && k.Length > 0) return k;
                Console.WriteLine("O Valor deve ser preenchido!");
            } while (true);
        }
        private static void StartClientConsole(IPublicServer server)
        {
            string cmd;
            do
            {
                Console.Write("Accao pretendida: ");
                cmd = Console.ReadLine();
                IKey key;
                IValue value;
                try
                {
                    switch (cmd)
                    {
                        case "exit":
                            return;
                        case "new":
                        case "update":
                            key = new DemoKey(ConsoleReadKey());
                            value = new DemoValue(ConsoleReadValue());
                            if (cmd.Equals("new"))
                                server.storePair(key, value);
                            else
                                server.updatePair(key, value);
                            break;
                        case "delete":
                            key = new DemoKey(ConsoleReadKey());
                            server.deletePair(key);
                            break;
                        case "read":
                            key = new DemoKey(ConsoleReadKey());
                            value = server.readPair(key);
                            if (value != null)
                            {
                                Console.WriteLine("IValue obtido é do tipo " + value.GetType());
                                Console.WriteLine("" + value.ToString());
                            }
                            else
                            {
                                Console.WriteLine("A chave nao existe");
                            }
                            break;
                        default:
                            if(!cmd.Equals("help")) Console.WriteLine("Commando invalido");
                            Console.WriteLine("Commandos possiveis: exit, new, update, read, help");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Erro na operacao \""+cmd+"\": " + e.Message);
                }
            } while (true);
        }
    }
}
