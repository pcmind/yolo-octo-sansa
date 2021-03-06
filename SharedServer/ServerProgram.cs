﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using SharedInterface;
using System.Threading;
using System.Runtime.Serialization.Formatters;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml;

namespace SharedServer
{
    [Serializable]
    public class VersionableValue
    {
        private readonly int _version = 0;
        private readonly IValue _value;
        public VersionableValue(IValue value) { _value = value; }
        public VersionableValue(VersionableValue v_v, IValue value) { _value = value; _version = v_v.Version+1;  }
        public VersionableValue(VersionableValue v_v) { _value = v_v.Value; _version = v_v.Version+1; }
        public IValue Value { get { return _value; } }
        public int Version { get { return _version; } }
    }

    [Serializable]
    public class UnloadKeyValue
    {
        string key;
        IValue value;
        public UnloadKeyValue(){}
        public UnloadKeyValue(string key, IValue value) { this.key = key; this.value = value; }
    }

    class ValueHolder
    {
        private volatile VersionableValue _value = null;
        private ServerEndPoint _owner = null;
        //lazy
        private HashSet<ServerEndPoint> serverClients = null;

        //usado para transmitir para os clientes
        public IValue Value { 
            get{ return _value.Value; }
            set { _value = new VersionableValue(_value, value); } 
        }
        //usado para transferir entre os servidores
        public VersionableValue VersionValue { get { return _value; } set{ _value = value; } }

        public ServerEndPoint Owner { get { return _owner; } set { _owner = value; } }


        public ValueHolder(IValue value)
        {
            _value = new VersionableValue(value);
        }
        public ValueHolder(VersionableValue value)
        {
            VersionValue = value;
        }
        public ValueHolder(ServerEndPoint owner, VersionableValue value)
            : this(value)
        {
            Owner = owner;
        }
        public IEnumerable<ServerEndPoint> GetClients()
        {
            if(serverClients!=null)
                return serverClients;
            return (serverClients = new HashSet<ServerEndPoint>());
        }
        public void AddClient(ServerEndPoint requester)
        {
            if(requester==null) return;

            if (serverClients == null)
            {
                //lazy initialisation
                serverClients = new HashSet<ServerEndPoint>();
            }
            else if (serverClients.Contains(requester))
            {
                return;
            }
            serverClients.Add(requester);
        }
        public bool IsLocal()
        {
            return Owner == null;
        }
    }
    [Serializable]
    public struct SerKeyValuePair<K, V> {
        public K Key { get; set; }
        public V Value { get; set; }
    }

    interface IPrivateServer : IPublicServer
    {
        bool SrvUpdateValue(string key, VersionableValue value);
        void SrvDeleteValue(string key);
        bool SrvSetNewOwner(string key, string new_owner);
        VersionableValue SrvGetLocalValue(string key, string servername);
        void SrvAlive(string server_name);
        void SrvShutdown(string server_name);
    }

    class ServerDictionary
    {
        public static readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private readonly Dictionary<string, ValueHolder> key_value_db = new Dictionary<string, ValueHolder>();

        public bool TryGetValue(object key, out ValueHolder value)
        {
            _lock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                return key_value_db.TryGetValue(_key(key), out value);
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
        }
        public bool TryGetVersionValue(object key, out VersionableValue value)
        {
            return TryGetVersionValue(key, out value, null);
        }
        public bool TryGetVersionValue(object key, out VersionableValue value, ServerEndPoint client)
        {
            ValueHolder v;
            _lock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    value = v.VersionValue;
                }else{
                    value = null;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
            if (v != null && v.IsLocal())
            {
                TryAddClient(key, client);
            }
            if (value != null)
                return true;
            return false;
        }
        public bool TryAddValue(object key, IValue value)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    return false;
                }
                else
                {
                    key_value_db.Add(_key(key), new ValueHolder(value));
                    return true;
                }
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryAddValue(object key, VersionableValue value)
        {
            return TryAddValue(key, value, null);
        }
        public bool TryAddValue(object key, VersionableValue value, ServerEndPoint owner)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    return false;
                }
                else
                {
                    if (owner == null)
                        v = new ValueHolder(value);
                    else
                        v = new ValueHolder(owner, value);
                    key_value_db.Add(_key(key), v);
                    return true;
                }
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryUpdValue(object key, IValue value, out VersionableValue v_value)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    v.Value = value;
                    v_value = v.VersionValue;
                    return true;
                }
                else
                {
                    v_value = null;
                    return false;
                }
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryUpdValue(object key, VersionableValue value)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    v.VersionValue = value;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryDelValue(object key, out ValueHolder value)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                if (key_value_db.TryGetValue(_key(key), out value))
                {
                    key_value_db.Remove(_key(key));
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryChown(object key, ServerEndPoint owner)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    v.Owner = owner;
                    return true;
                }
                else
                {
                    return false;
                }

            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public bool TryAddClient(object key, ServerEndPoint client)
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                ValueHolder v;
                if (key_value_db.TryGetValue(_key(key), out v))
                {
                    if (v.IsLocal())
                    {
                        v.AddClient(client);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }
        public string _key(object k)
        {
            return k is string ? (string)k : ((IKey)k).HashString;
        }

        internal List<ServerEndPoint> TryGetClients(object key)
        {
            _lock.AcquireReaderLock(Timeout.Infinite);
            List<ServerEndPoint> list = new List<ServerEndPoint>();
            try
            {
                ValueHolder value;
                if (key_value_db.TryGetValue(_key(key), out value))
                {
                    if (value.IsLocal())
                        list.AddRange(value.GetClients());
                }
            }finally
            {
                _lock.ReleaseReaderLock();
            }
            return list;
        }
        
        public void SerializeDB(string filename) {
            List<Type> types = new List<Type>();
            List<string> typeNames = new List<string>();
            List<SerKeyValuePair<string, object>> ownItems = new List<SerKeyValuePair<string, object>>();
            _lock.AcquireReaderLock(Timeout.Infinite);
            try {
                foreach (KeyValuePair<string, ValueHolder> kp in key_value_db) {
                    if (kp.Value.IsLocal()) {
                        if (!types.Exists(item => item.Equals(kp.Value.Value.GetType()))) {
                            typeNames.Add(kp.Value.Value.GetType().AssemblyQualifiedName);
                            types.Add(kp.Value.Value.GetType());
                        }
                        SerKeyValuePair<string, object> serKP = new SerKeyValuePair<string, object>();
                        serKP.Key = kp.Key;
                        serKP.Value = kp.Value.Value;
                        ownItems.Add(serKP);
                    }
                }
            } finally {
                _lock.ReleaseReaderLock();
            }
            
            XmlSerializer xmlSerializer = new XmlSerializer(typeNames.GetType());
            using (FileStream fs = File.Create(filename + "_extraTypes.xml")) {
                xmlSerializer.Serialize(fs, typeNames);
            }

            Type[] typesArr = types.ToArray<Type>();
            xmlSerializer = new XmlSerializer(ownItems.GetType(), typesArr);
            using (FileStream fs = File.Create(filename + ".xml")) {
                xmlSerializer.Serialize(fs, ownItems);
            }
        }

        public void DeserializeDB(string filename) {
            Type[] typesArr; 
            if (!File.Exists(filename + ".xml") || !File.Exists(filename + "_extraTypes.xml")) {
                return;
            }
            using (FileStream fs = File.OpenRead(filename + "_extraTypes.xml")) {
                List<string> typeNames = new List<string>();

                XmlSerializer xmlSerializer = new XmlSerializer(typeNames.GetType());
                typeNames = (List<string>)xmlSerializer.Deserialize(fs);
                typesArr = new Type[typeNames.Count];
                int i = 0;
                foreach (string typeName in typeNames) {
                    typesArr[i++] = Type.GetType(typeName);
                }
            }
            using (FileStream fs = File.OpenRead(filename + ".xml")) {
                List<SerKeyValuePair<string, object>> ownItems = new List<SerKeyValuePair<string, object>>();

                var xmlReaderSettings = new XmlReaderSettings();
                xmlReaderSettings.CheckCharacters = false;
                XmlReader xmlReader = XmlTextReader.Create(fs, xmlReaderSettings);

                XmlSerializer xmlSerializer = new XmlSerializer(ownItems.GetType(), typesArr);
                ownItems = (List<SerKeyValuePair<string, object>>)xmlSerializer.Deserialize(xmlReader);
                _lock.AcquireWriterLock(Timeout.Infinite);
                foreach (SerKeyValuePair<string, object> serKP in ownItems) {
                    TryAddValue(serKP.Key, (IValue)serKP.Value);
                }
            }
        }

        public void unloadStore()
        {
            List<UnloadKeyValue> l = new List<UnloadKeyValue>();
            foreach (KeyValuePair<string, ValueHolder> kp in key_value_db)
            {
                l.Add(new UnloadKeyValue(kp.Key, kp.Value.Value));
            }
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(typeof(List<UnloadKeyValue>));
            x.Serialize(Console.Out, l);
        }
    }
}
