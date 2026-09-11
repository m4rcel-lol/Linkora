using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace WLMClient.Config
{
    class SaveDataManager
    {
        private static string saveFilePath;

        /// <summary>
        /// Where the saved sign in details live. Next to the executable as on Windows, unless that
        /// location is read only (an installed macOS app bundle, for example), in which case the
        /// user's own configuration directory is used.
        /// </summary>
        public static string SaveFilePath
        {
            get
            {
                if (saveFilePath == null)
                {
                    saveFilePath = ResolveSaveFilePath();
                }

                return saveFilePath;
            }
        }

        private static string ResolveSaveFilePath()
        {
            const string fileName = "SaveData.xml";

            string beside = Path.Combine(AppContext.BaseDirectory, fileName);

            if (IsDirectoryWritable(AppContext.BaseDirectory))
            {
                return beside;
            }

            string root = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support")
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string directory = Path.Combine(root, "WLMClient");

            try
            {
                Directory.CreateDirectory(directory);

                return Path.Combine(directory, fileName);
            }
            catch
            {
                return beside;
            }
        }

        private static bool IsDirectoryWritable(string directory)
        {
            try
            {
                string probe = Path.Combine(directory, Path.GetRandomFileName());

                using (FileStream stream = File.Create(probe, 1, FileOptions.DeleteOnClose))
                {
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static SaveData GetConfiguration()
        {
            try
            {
                XmlDocument configDoc;
                XmlNode configNode;

                configDoc = new XmlDocument();

                configDoc.Load(SaveFilePath);
                configNode = configDoc.DocumentElement.SelectNodes("/config")[0];

                string rememberId = configNode.SelectSingleNode("OPTION_REMEMBER_ID").InnerText;
                string rememberPassword = configNode.SelectSingleNode("OPTION_REMEMBER_PASSWORD").InnerText;
                string autoLogin = configNode.SelectSingleNode("OPTION_LOGIN_AUTO").InnerText;
                string saveID = configNode.SelectSingleNode("SAVE_ID").InnerText;
                string savePass = Base64Decode(configNode.SelectSingleNode("SAVE_PASS").InnerText);

                // Added later, so a settings file written by an older build will not have it.
                string saveServer = ReadOptional(configNode, "SAVE_SERVER", "").Trim();
                string language = ReadOptional(configNode, "LANGUAGE", "").Trim();
                string darkTheme = ReadOptional(configNode, "THEME_DARK", "0").Trim();

                return new SaveData(Convert.ToBoolean(Convert.ToInt16(rememberId)), Convert.ToBoolean(Convert.ToInt16(rememberPassword)),
                    Convert.ToBoolean(Convert.ToInt16(autoLogin)), saveID, savePass, saveServer) { language = language, darkTheme = darkTheme == "1" };
            }
            catch
            {
                CreateXMLFile();

                return new SaveData(false, false, false, "", "");
            }
        }

        public static void CreateXMLFile()
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<?xml version=\"1.0\" encoding=\"ISO - 8859 - 1\"?><config><OPTION_REMEMBER_ID>0</OPTION_REMEMBER_ID><OPTION_REMEMBER_PASSWORD>0</OPTION_REMEMBER_PASSWORD><OPTION_LOGIN_AUTO>0</OPTION_LOGIN_AUTO><SAVE_ID></SAVE_ID><SAVE_PASS></SAVE_PASS><SAVE_SERVER></SAVE_SERVER><LANGUAGE></LANGUAGE><THEME_DARK>0</THEME_DARK></config>"); //Your string here
            
            // The writer owns the file handle, so it has to be closed before anything reads the
            // file back; without this the settings could be left unflushed.
            using (XmlTextWriter writer = new XmlTextWriter(SaveFilePath, null))
            {
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
            }
        }

        public static void SaveConfiguration(SaveData configuration)
        {
            try
            {
                XmlDocument configDoc;
                XmlNode configNode;

                configDoc = new XmlDocument();

                configDoc.Load(SaveFilePath);

                configNode = configDoc.DocumentElement.SelectNodes("/config")[0];

                configNode.SelectSingleNode("OPTION_REMEMBER_ID").InnerText = Convert.ToInt32(configuration.rememberId).ToString();
                configNode.SelectSingleNode("OPTION_REMEMBER_PASSWORD").InnerText = Convert.ToInt32(configuration.rememberPassword).ToString();
                configNode.SelectSingleNode("OPTION_LOGIN_AUTO").InnerText = Convert.ToInt32(configuration.autoLogin).ToString();
                configNode.SelectSingleNode("SAVE_ID").InnerText = configuration.saveId;
                configNode.SelectSingleNode("SAVE_PASS").InnerText = Base64Encode(configuration.savePass);
                WriteOptional(configDoc, configNode, "SAVE_SERVER", configuration.saveServer ?? "");
                WriteOptional(configDoc, configNode, "LANGUAGE", configuration.language ?? "");
                WriteOptional(configDoc, configNode, "THEME_DARK", configuration.darkTheme ? "1" : "0");

                configDoc.Save(SaveFilePath);
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>Reads a node that may not exist in settings written by an older build.</summary>
        private static string ReadOptional(XmlNode configNode, string name, string fallback)
        {
            XmlNode node = configNode.SelectSingleNode(name);

            return node == null ? fallback : node.InnerText;
        }

        /// <summary>Writes a node, adding it to the document if it is not present yet.</summary>
        private static void WriteOptional(XmlDocument document, XmlNode configNode, string name, string value)
        {
            XmlNode node = configNode.SelectSingleNode(name);

            if (node == null)
            {
                node = document.CreateElement(name);
                configNode.AppendChild(node);
            }

            node.InnerText = value;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);

            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
