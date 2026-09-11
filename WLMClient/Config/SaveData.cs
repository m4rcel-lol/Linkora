using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMClient.Config
{
    class SaveData
    {
        public bool rememberId { get; set; }
        public bool rememberPassword { get; set; }
        public bool autoLogin { get; set; }
        public string saveId { get; set; }
        public string savePass { get; set; }

        /// <summary>
        /// The server the user last signed in to, as typed on the sign in page. Empty means fall
        /// back to whatever Messenger.config specifies.
        /// </summary>
        public string saveServer { get; set; }

        public SaveData(bool rememberId, bool rememberPassword, bool autoLogin,
            string saveId, string savePass)
            : this(rememberId, rememberPassword, autoLogin, saveId, savePass, "")
        {
        }

        public SaveData(bool rememberId, bool rememberPassword, bool autoLogin,
            string saveId, string savePass, string saveServer)
        {
            this.rememberId = rememberId;
            this.rememberPassword = rememberPassword;
            this.autoLogin = autoLogin;
            this.saveId = saveId;
            this.savePass = savePass;
            this.saveServer = saveServer;
        }
    }
}
