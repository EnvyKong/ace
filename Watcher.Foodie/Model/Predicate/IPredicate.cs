﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watcher.Foodie.Model
{
    public interface IPredicate
    {
        bool IsSatisfiedBy(int a, int b);
    }
}
