using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WLMClient.Compat;

namespace WLMClient.Locale
{
    /// <summary>
    /// How long the user has been idle, used to switch the status to Away automatically. The Win32
    /// GetLastInputInfo call is replaced by a per platform implementation in <see cref="Platform"/>.
    /// </summary>
    class LastUserInput
    {
        public static uint GetLastInputTime()
        {
            return Platform.GetIdleSeconds();
        }
    }
}
