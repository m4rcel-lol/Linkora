using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMData.Enums
{
    /// <summary>Outcome of a request to change the name an account signs in with.</summary>
    public enum UsernameChangeResultCode
    {
        success = 0,

        /// <summary>Empty, too long, or containing characters that are not allowed.</summary>
        invalid = 1,

        /// <summary>Another account already uses it.</summary>
        taken = 2,

        /// <summary>The database could not be updated; nothing was changed.</summary>
        failed = 3
    }
}
