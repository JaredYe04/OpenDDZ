
using OpenDDZ.DDZUtils.Entities;
using OpenDDZ.DDZUtils.Enums;
using OpenDDZ.DDZUtils.GameIOs;
using OpenDDZ.DDZUtils.Interfaces;
using System;

namespace OpenDDZ.DDZUtils.Players
{
    public class ConsoleRealPlayer : RealPlayer
    {
        public ConsoleRealPlayer(string name) : base(name, new ConsoleIO()) { }

        public ConsoleRealPlayer(string name, IGameIO gameIO) : base(name, gameIO) { }
    }
}