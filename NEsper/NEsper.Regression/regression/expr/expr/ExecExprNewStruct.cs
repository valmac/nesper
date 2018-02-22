///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Avro.Generic;
using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.client.soda;
using com.espertech.esper.compat;
using com.espertech.esper.supportregression.bean;
using com.espertech.esper.supportregression.execution;
using com.espertech.esper.util;
using NEsper.Avro.Extensions;
using NEsper.Avro.Util.Support;
using static com.espertech.esper.supportregression.util.SupportMessageAssertUtil;

using NUnit.Framework;

namespace com.espertech.esper.regression.expr.expr
{
    using Map = IDictionary<string, object>;

    public class ExecExprNewStruct : RegressionExecution {
        public override void Run(EPServiceProvider epService) {
            epService.EPAdministrator.Configuration.AddEventType<SupportBean>();
            RunAssertionNewWRepresentation(epService);
            RunAssertionDefaultColumnsAndSODA(epService);
            RunAssertionNewWithCase(epService);
            RunAssertionInvalid(epService);
        }
    
        private void RunAssertionNewWRepresentation(EPServiceProvider epService) {
            foreach (EventRepresentationChoice rep in EnumHelper.GetValues<EventRepresentationChoice>()) {
                TryAssertionNewWRepresentation(epService, rep);
            }
        }
    
        private void RunAssertionDefaultColumnsAndSODA(EPServiceProvider epService) {
            string epl = "select " +
                    "case theString" +
                    " when \"A\" then new{theString=\"Q\",intPrimitive,col2=theString||\"A\"}" +
                    " when \"B\" then new{theString,intPrimitive=10,col2=theString||\"B\"} " +
                    "end as val0 from SupportBean as sb";
    
            EPStatement stmt = epService.EPAdministrator.CreateEPL(epl);
            var listener = new SupportUpdateListener();
            stmt.Events += listener.Update;
            TryAssertionDefault(epService, stmt, listener);
    
            EPStatementObjectModel model = epService.EPAdministrator.CompileEPL(epl);
            Assert.AreEqual(epl, model.ToEPL());
            stmt = epService.EPAdministrator.Create(model);
            stmt.Events += listener.Update;
            TryAssertionDefault(epService, stmt, listener);
    
            // test to-expression string
            epl = "select " +
                    "case theString" +
                    " when \"A\" then new{theString=\"Q\",intPrimitive,col2=theString||\"A\" }" +
                    " when \"B\" then new{theString,intPrimitive = 10,col2=theString||\"B\" } " +
                    "end from SupportBean as sb";
            stmt = epService.EPAdministrator.CreateEPL(epl);
            stmt.Events += listener.Update;
            Assert.AreEqual("case theString when \"A\" then new{theString=\"Q\",intPrimitive,col2=theString||\"A\"} when \"B\" then new{theString,intPrimitive=10,col2=theString||\"B\"} end", stmt.EventType.PropertyNames[0]);
            stmt.Dispose();
        }
    
        private void TryAssertionDefault(EPServiceProvider epService, EPStatement stmt, SupportUpdateListener listener) {
    
            Assert.AreEqual(typeof(Map), stmt.EventType.GetPropertyType("val0"));
            FragmentEventType fragType = stmt.EventType.GetFragmentType("val0");
            Assert.IsFalse(fragType.IsIndexed);
            Assert.IsFalse(fragType.IsNative);
            Assert.AreEqual(typeof(string), fragType.FragmentType.GetPropertyType("theString"));
            Assert.AreEqual(typeof(int?), fragType.FragmentType.GetPropertyType("intPrimitive"));
            Assert.AreEqual(typeof(string), fragType.FragmentType.GetPropertyType("col2"));
    
            string[] fieldsInner = "theString,intPrimitive,col2".Split(',');
            epService.EPRuntime.SendEvent(new SupportBean("E1", 1));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{null, null, null});
    
            epService.EPRuntime.SendEvent(new SupportBean("A", 2));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{"Q", 2, "AA"});
    
            epService.EPRuntime.SendEvent(new SupportBean("B", 3));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{"B", 10, "BB"});
    
            stmt.Dispose();
        }
    
        private void RunAssertionNewWithCase(EPServiceProvider epService) {
            string epl = "select " +
                    "case " +
                    "  when theString = 'A' then new { col1 = 'X', col2 = 10 } " +
                    "  when theString = 'B' then new { col1 = 'Y', col2 = 20 } " +
                    "  when theString = 'C' then new { col1 = null, col2 = null } " +
                    "  else new { col1 = 'Z', col2 = 30 } " +
                    "end as val0 from SupportBean sb";
            TryAssertion(epService, epl);
    
            epl = "select " +
                    "case theString " +
                    "  when 'A' then new { col1 = 'X', col2 = 10 } " +
                    "  when 'B' then new { col1 = 'Y', col2 = 20 } " +
                    "  when 'C' then new { col1 = null, col2 = null } " +
                    "  else new{ col1 = 'Z', col2 = 30 } " +
                    "end as val0 from SupportBean sb";
            TryAssertion(epService, epl);
        }
    
        private void TryAssertion(EPServiceProvider epService, string epl) {
            var listener = new SupportUpdateListener();
            EPStatement stmt = epService.EPAdministrator.CreateEPL(epl);
            stmt.Events += listener.Update;
            Assert.AreEqual(typeof(Map), stmt.EventType.GetPropertyType("val0"));
            FragmentEventType fragType = stmt.EventType.GetFragmentType("val0");
            Assert.IsFalse(fragType.IsIndexed);
            Assert.IsFalse(fragType.IsNative);
            Assert.AreEqual(typeof(string), fragType.FragmentType.GetPropertyType("col1"));
            Assert.AreEqual(typeof(int?), fragType.FragmentType.GetPropertyType("col2"));
    
            string[] fieldsInner = "col1,col2".Split(',');
            epService.EPRuntime.SendEvent(new SupportBean("E1", 1));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{"Z", 30});
    
            epService.EPRuntime.SendEvent(new SupportBean("A", 2));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{"X", 10});
    
            epService.EPRuntime.SendEvent(new SupportBean("B", 3));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{"Y", 20});
    
            epService.EPRuntime.SendEvent(new SupportBean("C", 4));
            EPAssertionUtil.AssertPropsMap((Map) listener.AssertOneGetNewAndReset().Get("val0"), fieldsInner, new object[]{null, null});
    
            stmt.Dispose();
        }
    
        private void RunAssertionInvalid(EPServiceProvider epService) {
            string epl;
    
            epl = "select case when true then new { col1 = 'a' } else 1 end from SupportBean";
            TryInvalid(epService, epl, "Error starting statement: Failed to validate select-clause expression 'case when true then new{col1=\"a\"} e...(44 chars)': Case node 'when' expressions require that all results either return a single value or a Map-type (new-operator) value, check the else-condition [select case when true then new { col1 = 'a' } else 1 end from SupportBean]");
    
            epl = "select case when true then new { col1 = 'a' } when false then 1 end from SupportBean";
            TryInvalid(epService, epl, "Error starting statement: Failed to validate select-clause expression 'case when true then new{col1=\"a\"} w...(55 chars)': Case node 'when' expressions require that all results either return a single value or a Map-type (new-operator) value, check when-condition number 1 [select case when true then new { col1 = 'a' } when false then 1 end from SupportBean]");
    
            epl = "select case when true then new { col1 = 'a' } else new { col1 = 1 } end from SupportBean";
            TryInvalid(epService, epl, "Error starting statement: Failed to validate select-clause expression 'case when true then new{col1=\"a\"} e...(54 chars)': Incompatible case-when return types by new-operator in case-when number 1: Type by name 'Case-when number 1' in property 'col1' expected class System.String but receives class " + Name.Of<int>() + " [select case when true then new { col1 = 'a' } else new { col1 = 1 } end from SupportBean]");
    
            epl = "select case when true then new { col1 = 'a' } else new { col2 = 'a' } end from SupportBean";
            TryInvalid(epService, epl, "Error starting statement: Failed to validate select-clause expression 'case when true then new{col1=\"a\"} e...(56 chars)': Incompatible case-when return types by new-operator in case-when number 1: The property 'col1' is not provided but required [select case when true then new { col1 = 'a' } else new { col2 = 'a' } end from SupportBean]");
    
            epl = "select case when true then new { col1 = 'a', col1 = 'b' } end from SupportBean";
            TryInvalid(epService, epl, "Error starting statement: Failed to validate select-clause expression 'case when true then new{col1=\"a\",co...(46 chars)': Failed to validate new-keyword property names, property 'col1' has already been declared [select case when true then new { col1 = 'a', col1 = 'b' } end from SupportBean]");
        }
    
        private void TryAssertionNewWRepresentation(EPServiceProvider epService, EventRepresentationChoice rep) {
            string epl = rep.GetAnnotationText() + "select new { theString = 'x' || theString || 'x', intPrimitive = intPrimitive + 2} as val0 from SupportBean as sb";
    
            EPStatement stmt = epService.EPAdministrator.CreateEPL(epl);
            var listener = new SupportUpdateListener();
            stmt.Events += listener.Update;
    
            Assert.AreEqual(rep.IsAvroEvent() ? typeof(GenericRecord) : typeof(Map), stmt.EventType.GetPropertyType("val0"));
            FragmentEventType fragType = stmt.EventType.GetFragmentType("val0");
            Assert.IsFalse(fragType.IsIndexed);
            Assert.IsFalse(fragType.IsNative);
            Assert.AreEqual(typeof(string), fragType.FragmentType.GetPropertyType("theString"));
            Assert.AreEqual(typeof(int?), TypeHelper.GetBoxedType(fragType.FragmentType.GetPropertyType("intPrimitive")));
    
            string[] fieldsInner = "theString,intPrimitive".Split(',');
            epService.EPRuntime.SendEvent(new SupportBean("E1", -5));
            EventBean @event = listener.AssertOneGetNewAndReset();
            if (rep.IsAvroEvent()) {
                SupportAvroUtil.AvroToJson(@event);
                GenericRecord inner = (GenericRecord) @event.Get("val0");
                Assert.AreEqual("xE1x", inner.Get("theString"));
                Assert.AreEqual(-3, inner.Get("intPrimitive"));
            } else {
                EPAssertionUtil.AssertPropsMap((Map) @event.Get("val0"), fieldsInner, new object[]{"xE1x", -3});
            }
    
            stmt.Dispose();
        }
    }
} // end of namespace