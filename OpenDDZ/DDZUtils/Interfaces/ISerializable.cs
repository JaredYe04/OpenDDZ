using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDDZ.DDZUtils.Interfaces
{
    internal interface ISerializable
    {
        string Serialize();
        void Deserialize(string data);
    }
}
