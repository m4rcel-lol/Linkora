using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace WLMClient.Config
{
    /// <summary>
    /// The contacts a user has pinned to the Favourites group. Stored per account next to the
    /// other saved settings, so two people using the same installation keep separate lists.
    /// </summary>
    static class Favourites
    {
        private const string FileName = "Favourites.xml";

        private static readonly object locker = new object();
        private static readonly HashSet<string> pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string loadedForAccount;

        /// <summary>Raised when the pinned set changes, so the contact list can regroup.</summary>
        public static event Action Changed;

        private static string FilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(SaveDataManager.SaveFilePath) ?? "", FileName); }
        }

        /// <summary>Loads the list belonging to the signed in account.</summary>
        public static void LoadFor(string accountId)
        {
            lock (locker)
            {
                pinned.Clear();
                loadedForAccount = (accountId ?? "").Trim();

                if (loadedForAccount.Length == 0)
                {
                    return;
                }

                try
                {
                    if (!File.Exists(FilePath))
                    {
                        return;
                    }

                    XmlDocument document = new XmlDocument();
                    document.Load(FilePath);

                    XmlNode user = document.SelectSingleNode(
                        "/favourites/user[@id='" + EscapeForXPath(loadedForAccount) + "']");

                    if (user == null)
                    {
                        return;
                    }

                    foreach (XmlNode contact in user.SelectNodes("contact"))
                    {
                        string id = contact.InnerText.Trim();

                        if (id.Length > 0)
                        {
                            pinned.Add(id);
                        }
                    }
                }
                catch
                {
                    // A damaged file just means starting with an empty list.
                }
            }
        }

        public static bool IsFavourite(string contactId)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return false;
            }

            lock (locker)
            {
                return pinned.Contains(contactId.Trim());
            }
        }

        public static void Set(string contactId, bool favourite)
        {
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return;
            }

            lock (locker)
            {
                if (favourite)
                {
                    pinned.Add(contactId.Trim());
                }
                else
                {
                    pinned.Remove(contactId.Trim());
                }

                Save();
            }

            Action handler = Changed;

            if (handler != null)
            {
                handler();
            }
        }

        public static void Toggle(string contactId)
        {
            Set(contactId, !IsFavourite(contactId));
        }

        public static int Count
        {
            get
            {
                lock (locker)
                {
                    return pinned.Count;
                }
            }
        }

        /// <summary>Rewrites this account's entry, leaving other accounts' lists untouched.</summary>
        private static void Save()
        {
            if (string.IsNullOrEmpty(loadedForAccount))
            {
                return;
            }

            try
            {
                XmlDocument document = new XmlDocument();

                if (File.Exists(FilePath))
                {
                    try
                    {
                        document.Load(FilePath);
                    }
                    catch
                    {
                        document = new XmlDocument();
                    }
                }

                XmlNode root = document.SelectSingleNode("/favourites");

                if (root == null)
                {
                    root = document.CreateElement("favourites");
                    document.RemoveAll();
                    document.AppendChild(root);
                }

                XmlNode user = root.SelectSingleNode("user[@id='" + EscapeForXPath(loadedForAccount) + "']");

                if (user != null)
                {
                    root.RemoveChild(user);
                }

                XmlElement element = document.CreateElement("user");
                element.SetAttribute("id", loadedForAccount);

                foreach (string id in pinned.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    XmlElement contact = document.CreateElement("contact");
                    contact.InnerText = id;

                    element.AppendChild(contact);
                }

                root.AppendChild(element);

                document.Save(FilePath);
            }
            catch
            {
                // Losing the favourites list is not worth interrupting the user over.
            }
        }

        /// <summary>XPath has no escape syntax, so a quote in an id has to be handled directly.</summary>
        private static string EscapeForXPath(string value)
        {
            return value.Replace("'", "&apos;");
        }
    }
}
