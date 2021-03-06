///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.compat.logging;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.supportregression.bean;
using com.espertech.esper.supportregression.client;

using NUnit.Framework;

namespace com.espertech.esper.regression.resultset
{
    [TestFixture]
	public class TestOrderByEventPerGroup
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private EPServiceProvider _epService;
		private SupportUpdateListener _testListener;

        [SetUp]
	    public void SetUp()
	    {
	        var config = SupportConfigFactory.GetConfiguration();
	        _epService = EPServiceProviderManager.GetDefaultProvider(config);
	        _epService.Initialize();
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, this.GetType(), this.GetType().FullName);}
	    }

        [TearDown]
	    public void TearDown() {
	        if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest();}
	        _testListener = null;
	    }

        [Test]
	    public void TestNoHavingNoJoin()
		{
			var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	                                typeof(SupportMarketDataBean).FullName + "#length(20) " +
	                                "group by Symbol " +
	                                "output every 6 events " +
	                                "order by sum(Price), Symbol";
	        var statement = _epService.EPAdministrator.CreateEPL(statementString);

	        RunAssertionNoHaving(statement);
	    }

        [Test]
	    public void TestHavingNoJoin()
	    {
			var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	                                    typeof(SupportMarketDataBean).FullName + "#length(20) " +
	                                    "group by Symbol " +
	                                    "having sum(Price) > 0 " +
	                                    "output every 6 events " +
	                                    "order by sum(Price), Symbol";
	        var statement = _epService.EPAdministrator.CreateEPL(statementString);
	        RunAssertionHaving(statement);
		}

        [Test]
	    public void TestNoHavingJoin()
	    {
	    	var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	                            typeof(SupportMarketDataBean).FullName + "#length(20) as one, " +
	                            typeof(SupportBeanString).FullName + "#length(100) as two " +
	                            "where one.Symbol = two.TheString " +
	                            "group by Symbol " +
	                            "output every 6 events " +
	                            "order by sum(Price), Symbol";
	        var statement = _epService.EPAdministrator.CreateEPL(statementString);

	        _epService.EPRuntime.SendEvent(new SupportBeanString("CAT"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("IBM"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CMU"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("KGB"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("DOG"));

	        RunAssertionNoHaving(statement);
	    }

        [Test]
	    public void TestHavingJoin()
	    {
	        var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	            typeof(SupportMarketDataBean).FullName + "#length(20) as one, " +
	            typeof(SupportBeanString).FullName + "#length(100) as two " +
	            "where one.Symbol = two.TheString " +
	            "group by Symbol " +
	            "having sum(Price) > 0 " +
	            "output every 6 events " +
	            "order by sum(Price), Symbol";

	        var statement = _epService.EPAdministrator.CreateEPL(statementString);
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CAT"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("IBM"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CMU"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("KGB"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("DOG"));

	        RunAssertionHaving(statement);
	    }

        [Test]
	    public void TestHavingJoinAlias()
	    {
	        var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	            typeof(SupportMarketDataBean).FullName + "#length(20) as one, " +
	            typeof(SupportBeanString).FullName + "#length(100) as two " +
	            "where one.Symbol = two.TheString " +
	            "group by Symbol " +
	            "having sum(Price) > 0 " +
	            "output every 6 events " +
	            "order by mysum, Symbol";

	        var statement = _epService.EPAdministrator.CreateEPL(statementString);
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CAT"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("IBM"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CMU"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("KGB"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("DOG"));

	        RunAssertionHaving(statement);
	    }

        [Test]
		public void TestLast()
		{
			var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	                                    typeof(SupportMarketDataBean).FullName + "#length(20) " +
	                                    "group by Symbol " +
	                                    "output last every 6 events " +
	                                    "order by sum(Price), Symbol";
	        var statement = _epService.EPAdministrator.CreateEPL(statementString);
	        RunAssertionLast(statement);
	    }

        [Test]
	    public void TestLastJoin()
	    {
	        var statementString = "select irstream Symbol, sum(Price) as mysum from " +
	                                typeof(SupportMarketDataBean).FullName + "#length(20) as one, " +
	                                typeof(SupportBeanString).FullName + "#length(100) as two " +
	                                "where one.Symbol = two.TheString " +
	                                "group by Symbol " +
	                                "output last every 6 events " +
	                                "order by sum(Price), Symbol";

	        var statement = _epService.EPAdministrator.CreateEPL(statementString);

	        _epService.EPRuntime.SendEvent(new SupportBeanString("CAT"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("IBM"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CMU"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("KGB"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("DOG"));

	        RunAssertionLast(statement);
	    }

        [Test]
	    public void TestIteratorGroupByEventPerGroup()
		{
	        var fields = new string[] {"Symbol", "sumPrice"};
	        var statementString = "select Symbol, sum(Price) as sumPrice from " +
	    	            typeof(SupportMarketDataBean).FullName + "#length(10) as one, " +
	    	            typeof(SupportBeanString).FullName + "#length(100) as two " +
	                    "where one.Symbol = two.TheString " +
	                    "group by Symbol " +
	                    "order by Symbol";
	        var statement = _epService.EPAdministrator.CreateEPL(statementString);

	        _epService.EPRuntime.SendEvent(new SupportBeanString("CAT"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("IBM"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("CMU"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("KGB"));
	        _epService.EPRuntime.SendEvent(new SupportBeanString("DOG"));

	        SendEvent("CAT", 50);
	        SendEvent("IBM", 49);
	        SendEvent("CAT", 15);
	        SendEvent("IBM", 100);
	        EPAssertionUtil.AssertPropsPerRowAnyOrder(statement.GetEnumerator(), fields,
	                new object[][]{
	                         new object[] {"CAT", 65d},
	                         new object[] {"IBM", 149d}
	                });

	        SendEvent("KGB", 75);
	        EPAssertionUtil.AssertPropsPerRowAnyOrder(statement.GetEnumerator(), fields,
	                new object[][]{
	                         new object[] {"CAT", 65d},
	                         new object[] {"IBM", 149d},
	                         new object[] {"KGB", 75d}
	                });
	    }

	    private void SendEvent(string symbol, double price)
		{
		    var bean = new SupportMarketDataBean(symbol, price, 0L, null);
		    _epService.EPRuntime.SendEvent(bean);
		}

	    private void RunAssertionLast(EPStatement statement)
	    {
	        var fields = "Symbol,mysum".Split(',');
	        _testListener = new SupportUpdateListener();
	        statement.AddListener(_testListener);

	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 1);
	        SendEvent("CMU", 2);
	        SendEvent("CAT", 5);
	        SendEvent("CAT", 6);

	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"CMU", 3.0},  new object[] {"IBM", 7.0},  new object[] {"CAT", 11.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"CAT", null},  new object[] {"CMU", null},  new object[] {"IBM", null}});

	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 5);
	        SendEvent("CMU", 5);
	        SendEvent("DOG", 0);
	        SendEvent("DOG", 1);

	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"DOG", 1.0},  new object[] {"CMU", 13.0},  new object[] {"IBM", 14.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"DOG", null},  new object[] {"CMU", 3.0},  new object[] {"IBM", 7.0}});
	    }

	    private void RunAssertionNoHaving(EPStatement statement)
	    {
	        var fields = "Symbol,mysum".Split(',');

	        _testListener = new SupportUpdateListener();
	        statement.AddListener(_testListener);
	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 1);
	        SendEvent("CMU", 2);
	        SendEvent("CAT", 5);
	        SendEvent("CAT", 6);
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"CMU", 1.0},  new object[] {"CMU", 3.0},  new object[] {"IBM", 3.0},  new object[] {"CAT", 5.0},  new object[] {"IBM", 7.0},  new object[] {"CAT", 11.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"CAT", null},  new object[] {"CMU", null},  new object[] {"IBM", null},  new object[] {"CMU", 1.0},  new object[] {"IBM", 3.0},  new object[] {"CAT", 5.0}});
	        _testListener.Reset();

	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 5);
	        SendEvent("CMU", 5);
	        SendEvent("DOG", 0);
	        SendEvent("DOG", 1);
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"DOG", 0.0},  new object[] {"DOG", 1.0},  new object[] {"CMU", 8.0},  new object[] {"IBM", 10.0},  new object[] {"CMU", 13.0},  new object[] {"IBM", 14.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"DOG", null},  new object[] {"DOG", 0.0},  new object[] {"CMU", 3.0},  new object[] {"IBM", 7.0},  new object[] {"CMU", 8.0},  new object[] {"IBM", 10.0}});
	    }

	    private void RunAssertionHaving(EPStatement statement)
	    {
	        var fields = "Symbol,mysum".Split(',');
	        _testListener = new SupportUpdateListener();
	        statement.AddListener(_testListener);
	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 1);
	        SendEvent("CMU", 2);
	        SendEvent("CAT", 5);
	        SendEvent("CAT", 6);

	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"CMU", 1.0},  new object[] {"CMU", 3.0},  new object[] {"IBM", 3.0},  new object[] {"CAT", 5.0},  new object[] {"IBM", 7.0},  new object[] {"CAT", 11.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"CMU", 1.0},  new object[] {"IBM", 3.0},  new object[] {"CAT", 5.0}});
	        _testListener.Reset();

	        SendEvent("IBM", 3);
	        SendEvent("IBM", 4);
	        SendEvent("CMU", 5);
	        SendEvent("CMU", 5);
	        SendEvent("DOG", 0);
	        SendEvent("DOG", 1);
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastNewData, fields,
	                new object[][]{ new object[] {"DOG", 1.0},  new object[] {"CMU", 8.0},  new object[] {"IBM", 10.0},  new object[] {"CMU", 13.0},  new object[] {"IBM", 14.0}});
	        EPAssertionUtil.AssertPropsPerRow(_testListener.LastOldData, fields,
	                new object[][]{ new object[] {"CMU", 3.0},  new object[] {"IBM", 7.0},  new object[] {"CMU", 8.0},  new object[] {"IBM", 10.0}});
	    }
	}
} // end of namespace
