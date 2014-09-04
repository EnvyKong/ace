﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grit.CQRS
{
    public class Event : DomainMessage, IEvent
    {
        public Event(bool outer = true)
        {
            this.Outer = outer;
        }
        public bool Outer { get; set; }
    }
}
