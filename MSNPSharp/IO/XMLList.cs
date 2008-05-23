﻿#define TRACE

namespace MSNPSharp.IO
{
    using System;
    using System.IO;
    using System.Xml;
    using System.Text;    
    using System.Collections;
    using System.Collections.Generic;
    using System.Xml.Serialization;

    /// <summary>
    /// <remarks>This class can't inherit from SerializableDictionary ,
    /// or only the content of dictionary will be serialized, 
    /// unless implementing IXmlSerializable in the class.</remarks>
    /// </summary>
    [XmlRoot("ContactList"), Serializable]
    public abstract class XMLContactList
    {
        [NonSerialized]
        protected bool noCompress;

        [NonSerialized]
        private string fileName;

        protected DateTime lastChange;
        private SerializableDictionary<string, ContactInfo> contacts = new SerializableDictionary<string, ContactInfo>(0);

        protected XMLContactList()
            : base()
        {
        }

        public SerializableDictionary<string, ContactInfo> Contacts
        {
            get
            {
                return contacts;
            }
            set
            {
                contacts = value;
            }
        }

        protected string FileName
        {
            get
            {
                return fileName;
            }
            set
            {
                fileName = value;
            }
        }

        protected bool NoCompress
        {
            get
            {
                return noCompress;
            }
            set
            {
                noCompress = value;
            }
        }
        /*
        public ContactInfo this[string key]
        {
            get
            {
                return contacts[key];
            }
            set
            {
                contacts[key] = value;
            }
        }
         * */

        public DateTime LastChange
        {
            get
            {
                return lastChange;
            }
            set
            {
                lastChange = value;
            }
        }

        public virtual void Add(Dictionary<string, ContactInfo> range)
        {
            foreach (string account in range.Keys)
            {
                if (contacts.ContainsKey(account))
                {
                    if (contacts[account].LastChanged.CompareTo(range[account].LastChanged) <= 0)
                    {
                        contacts[account] = range[account];
                    }
                }
                else
                {
                    contacts.Add(account, range[account]);
                }
            }
        }


        public abstract void Save(string filename);
        public abstract void Save();

        protected virtual void SaveToHiddenMCL(string filename)
        {
            XmlSerializer ser = new XmlSerializer(this.GetType());
            MemoryStream ms = new MemoryStream();
            ser.Serialize(ms, this);
            MCLFile file = MCLFileManager.GetFile(filename, noCompress);
            file.Content = ms.ToArray();
            MCLFileManager.Save(file, true);
            ms.Close();

        }

        public static XMLContactList LoadFromFile(string filename, Type targetType, bool noCompress)
        {
            XMLContactList rtnobj = (XMLContactList)Activator.CreateInstance(targetType);
            if (File.Exists(filename))
            {
                MCLFile file = MCLFileManager.GetFile(filename, noCompress);
                if (file.Content != null)
                {
                    MemoryStream mem = new MemoryStream(file.Content);
                    rtnobj = (XMLContactList)new XmlSerializer(targetType).Deserialize(mem);
                    mem.Close();
                }
            }
            ((XMLContactList)rtnobj).NoCompress = noCompress;
            ((XMLContactList)rtnobj).FileName = filename;
            return rtnobj;
        }

    }
};
