﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace IoC.Framework.Test.Classes {
    public interface ITestComponentWithArrayDependency {
        ITestService[] Services { get; }
    }
}