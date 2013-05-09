using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SharedInterface
{
    [Serializable]
    public class MyKey : IKey
    {
        public string HashString
        {
            get
            {
                HashAlgorithm halg = new SHA1Managed();
                byte[] hash = halg.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Name + idade.ToString()));
                return new string(System.Text.Encoding.UTF8.GetChars(hash));
            }
        }
        public string Name;
        public int idade;
        public MyKey()
        {
        }
        public MyKey(string name, int idade)
        {
            this.Name = name; this.idade = idade;
        }
    }
    [Serializable]
    public class DemoKey : IKey
    {
        private string Key{ get; set; } 
        public DemoKey() { }
        public DemoKey(string key) { Key = key; }
        public string HashString
        {
            get
            {
                /*HashAlgorithm halg = new SHA1Managed();
                byte[] hash = halg.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Key));
                return new string(System.Text.Encoding.UTF8.GetChars(hash));*/
                return Key;
            }
        }

    }
    [Serializable]
    public class DemoValue : IValue
    {
        private string Value { get; set; }
        public DemoValue() { }
        public DemoValue(string value) { Value = value; }
        public override string ToString()
        {
            return "(DemoValue)"+Value;
        }
    }
    [Serializable]
    public class Valor : IValue
    {
        public string someText;
        public int[] notas;

        public Valor() {
            someText = "";
            this.notas = new int[]{};
        }

        public Valor(string txt, int[] notas)
        {
            this.someText = txt;
            this.notas = notas;
        }
    }

    public interface IKey
    {
        //Permite obter um valor de hash do objecto Chave
        string HashString { get; }
    }

    public interface IValue
    {
    }

    public interface IPublicServer
    {
        void storePair(IKey key, IValue value);
        IValue readPair(IKey key);
        void updatePair(IKey key, IValue newValue);
        void deletePair(IKey key);
        void sayHelllo();
    }
}
