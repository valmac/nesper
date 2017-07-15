﻿///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.epl.expression.core;

namespace com.espertech.esper.epl.expression.funcs.cast
{
    /// <summary>Casting and parsing computer.</summary>
    public class StringXFormComputer : CasterParserComputer
    {
        public Object Compute(Object input, EventBean[] eventsPerStream, bool newData, ExprEvaluatorContext exprEvaluatorContext)
        {
            return input.ToString();
        }

        public bool IsConstantForConstInput
        {
            get { return true; }
        }
    }
}