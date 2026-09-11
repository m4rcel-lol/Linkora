using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLMData.Enums
{
    /// <summary>The steps of setting up and tearing down a call.</summary>
    public enum CallSignalType
    {
        /// <summary>Caller is asking to start a call.</summary>
        invite = 0,

        /// <summary>Callee picked up.</summary>
        accept = 1,

        /// <summary>Callee turned the call down.</summary>
        decline = 2,

        /// <summary>Either side hung up, or the caller gave up before it was answered.</summary>
        end = 3,

        /// <summary>The other party is already in a call.</summary>
        busy = 4,

        /// <summary>The other party is not signed in.</summary>
        unavailable = 5
    }
}
