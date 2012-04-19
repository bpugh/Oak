﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Massive;

namespace BorrowedGames.Models
{
    public class WantedGames : DynamicRepository
    {
        public WantedGames()
        {
            Projection = d => new WantedGame(d);
        }
    }
}