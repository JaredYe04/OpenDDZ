
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.GameIOs;
using System;

namespace OpenDDZ.DDZUtils.Players
{
    public class ConsoleRealPlayer : RealPlayer
    {
        public ConsoleRealPlayer(string name) : base(name,new ConsoleIO()) { }


    }
}