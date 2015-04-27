///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

using com.espertech.esper.client;
using com.espertech.esper.compat.collections;
using com.espertech.esper.compat.logging;
using com.espertech.esper.compat.threading;
using com.espertech.esper.core.context.factory;
using com.espertech.esper.core.context.mgr;
using com.espertech.esper.core.service;
using com.espertech.esper.epl.expression.table;
using com.espertech.esper.epl.script;
using com.espertech.esper.epl.view;
using com.espertech.esper.events;
using com.espertech.esper.filter;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.util;
using com.espertech.esper.view;

namespace com.espertech.esper.core.context.util
{
    public class StatementAgentInstanceUtil
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void HandleFilterFault(EventBean theEvent, long version, EPServicesContext servicesContext, IDictionary<int, ContextControllerTreeAgentInstanceList> agentInstanceListMap)
        {
            foreach (var agentInstanceEntry in agentInstanceListMap)
            {
                if (agentInstanceEntry.Value.FilterVersionAfterAllocation > version)
                {
                    EvaluateEventForStatement(servicesContext, theEvent, null, agentInstanceEntry.Value.AgentInstances);
                }
            }
        }

        public static void StopAgentInstances(IList<AgentInstance> agentInstances, IDictionary<String, Object> terminationProperties, EPServicesContext servicesContext, bool isStatementStop, bool leaveLocksAcquired)
        {
            if (agentInstances == null)
            {
                return;
            }
            foreach (var instance in agentInstances)
            {
                StopAgentInstance(instance, terminationProperties, servicesContext, isStatementStop, leaveLocksAcquired);
            }
        }
    
        public static void StopAgentInstance(AgentInstance agentInstance, IDictionary<String, Object> terminationProperties, EPServicesContext servicesContext, bool isStatementStop, bool leaveLocksAcquired)
        {
            if (terminationProperties != null)
            {
                var contextProperties = (MappedEventBean)agentInstance.AgentInstanceContext.ContextProperties;
            }
            Stop(agentInstance.StopCallback, agentInstance.AgentInstanceContext, agentInstance.FinalView, servicesContext, isStatementStop, leaveLocksAcquired);
        }
    
        public static void StopSafe(ICollection<StopCallback> terminationCallbacks, StopCallback[] stopCallbacks, StatementContext statementContext)
        {
            var terminationArr = terminationCallbacks.ToArray();
            StopSafe(terminationArr, statementContext);
            StopSafe(stopCallbacks, statementContext);
        }
    
        public static void StopSafe(StopCallback[] stopMethods, StatementContext statementContext)
        {
            foreach (var stopCallback in stopMethods)
            {
                StopSafe(stopCallback, statementContext);
            }
        }
    
        public static void StopSafe(StopCallback stopMethod, StatementContext statementContext)
        {
            try
            {
                stopMethod.Invoke();
            }
            catch (Exception e)
            {
                Log.Warn("Failed to perform statement stop for statement '" + statementContext.StatementName +
                        "' expression '" + statementContext.Expression + "' : " + e.Message, e);
            }
        }
    
        public static void Stop(StopCallback stopCallback, AgentInstanceContext agentInstanceContext, Viewable finalView, EPServicesContext servicesContext, bool isStatementStop, bool leaveLocksAcquired)
        {
            using (Instrument.With(
                i => i.QContextPartitionDestroy(agentInstanceContext),
                i => i.AContextPartitionDestroy()))
            {
                // obtain statement lock
                var iLock = agentInstanceContext.EpStatementAgentInstanceHandle.StatementAgentInstanceLock;

                using (var iLockEnd = iLock.WriteLock.Acquire(!leaveLocksAcquired))
                {
                    try
                    {
                        if (finalView is OutputProcessViewTerminable && !isStatementStop)
                        {
                            var terminable = (OutputProcessViewTerminable) finalView;
                            terminable.Terminated();
                        }

                        StopSafe(stopCallback, agentInstanceContext.StatementContext);

                        if (servicesContext.SchedulableAgentInstanceDirectory != null)
                        {
                            servicesContext.SchedulableAgentInstanceDirectory.Remove(
                                agentInstanceContext.StatementContext.StatementId, agentInstanceContext.AgentInstanceId);
                        }

                        // indicate method resolution
                        agentInstanceContext.StatementContext.MethodResolutionService.DestroyedAgentInstance(
                            agentInstanceContext.AgentInstanceId);

                        // release resource
                        agentInstanceContext.StatementContext.StatementAgentInstanceRegistry.Deassign(
                            agentInstanceContext.AgentInstanceId);

                        // cause any remaining schedules, that may concide with the caller's schedule, to be ignored
                        agentInstanceContext.EpStatementAgentInstanceHandle.IsDestroyed = true;

                        // cause any filters, that may concide with the caller's filters, to be ignored
                        agentInstanceContext.EpStatementAgentInstanceHandle.StatementFilterVersion.StmtFilterVersion =
                            long.MaxValue;

                        if (agentInstanceContext.StatementContext.ExtensionServicesContext != null &&
                            agentInstanceContext.StatementContext.ExtensionServicesContext.StmtResources != null)
                        {
                            agentInstanceContext.StatementContext.ExtensionServicesContext.StmtResources.EndContextPartition
                                (agentInstanceContext.AgentInstanceId);
                        }
                    }
                    finally
                    {
                        if (!leaveLocksAcquired)
                        {
                            if (agentInstanceContext.StatementContext.EpStatementHandle.HasTableAccess)
                            {
                                agentInstanceContext.TableExprEvaluatorContext.ReleaseAcquiredLocks();
                            }
                        }
                    }
                }
            }
        }

        public static StatementAgentInstanceFactoryResult Start(
            EPServicesContext servicesContext,
            ContextControllerStatementBase statement,
            bool isSingleInstanceContext,
            int agentInstanceId,
            MappedEventBean agentInstanceProperties,
            AgentInstanceFilterProxy agentInstanceFilterProxy,
            bool isRecoveringResilient)
        {
            var statementContext = statement.StatementContext;
    
            // make a new lock for the agent instance or use the already-allocated default lock
            IReaderWriterLock agentInstanceLock;
            if (isSingleInstanceContext) {
                agentInstanceLock = statementContext.DefaultAgentInstanceLock;
            }
            else
            {
                agentInstanceLock = servicesContext.StatementLockFactory.GetStatementLock(
                    statementContext.StatementName, 
                    statementContext.Annotations, 
                    statementContext.IsStatelessSelect);
            }
    
            // share the filter version between agent instance handle (callbacks) and agent instance context
            var filterVersion = new StatementAgentInstanceFilterVersion();
    
            // create handle that comtains lock for use in scheduling and filter callbacks
            var agentInstanceHandle = new EPStatementAgentInstanceHandle(statementContext.EpStatementHandle, agentInstanceLock, agentInstanceId, filterVersion);
    
            // create agent instance context
            AgentInstanceScriptContext agentInstanceScriptContext = null;
            if (statementContext.DefaultAgentInstanceScriptContext != null) {
                agentInstanceScriptContext = new AgentInstanceScriptContext();
            }
            var agentInstanceContext = new AgentInstanceContext(statementContext, agentInstanceHandle, agentInstanceId, agentInstanceFilterProxy, agentInstanceProperties, agentInstanceScriptContext);
            var statementAgentInstanceLock = agentInstanceContext.EpStatementAgentInstanceHandle.StatementAgentInstanceLock;

            using(Instrument.With(
                i => i.QContextPartitionAllocate(agentInstanceContext),
                i => i.AContextPartitionAllocate()))
            {
                using (statementAgentInstanceLock.WriteLock.Acquire())
                {
                    try
                    {
                        // start
                        var startResult = statement.Factory.NewContext(agentInstanceContext, isRecoveringResilient);

                        // hook up with listeners+subscribers
                        startResult.FinalView.AddView(statement.MergeView); // hook output to merge view

                        // assign agents for expression-node based strategies
                        var aiExprSvc = statementContext.StatementAgentInstanceRegistry.AgentInstanceExprService;
                        var aiAggregationSvc =
                            statementContext.StatementAgentInstanceRegistry.AgentInstanceAggregationService;

                        // allocate aggregation service
                        if (startResult.OptionalAggegationService != null)
                        {
                            aiAggregationSvc.AssignService(agentInstanceId, startResult.OptionalAggegationService);
                        }

                        // allocate subquery
                        foreach (var item in startResult.SubselectStrategies)
                        {
                            var node = item.Key;
                            var strategyHolder = item.Value;

                            aiExprSvc.GetSubselectService(node).AssignService(agentInstanceId, strategyHolder.Stategy);
                            aiExprSvc.GetSubselectAggregationService(node)
                                .AssignService(agentInstanceId, strategyHolder.SubselectAggregationService);

                            // allocate prior within subquery
                            foreach (var priorEntry in strategyHolder.PriorStrategies)
                            {
                                aiExprSvc.GetPriorServices(priorEntry.Key)
                                    .AssignService(agentInstanceId, priorEntry.Value);
                            }

                            // allocate previous within subquery
                            foreach (var prevEntry in strategyHolder.PreviousNodeStrategies)
                            {
                                aiExprSvc.GetPreviousServices(prevEntry.Key)
                                    .AssignService(agentInstanceId, prevEntry.Value);
                            }
                        }

                        // allocate prior-expressions
                        foreach (var item in startResult.PriorNodeStrategies)
                        {
                            aiExprSvc.GetPriorServices(item.Key).AssignService(agentInstanceId, item.Value);
                        }

                        // allocate previous-expressions
                        foreach (var item in startResult.PreviousNodeStrategies)
                        {
                            aiExprSvc.GetPreviousServices(item.Key).AssignService(agentInstanceId, item.Value);
                        }

                        // allocate match-recognize previous expressions
                        var regexExprPreviousEvalStrategy = startResult.RegexExprPreviousEvalStrategy;
                        aiExprSvc.GetMatchRecognizePrevious()
                            .AssignService(agentInstanceId, regexExprPreviousEvalStrategy);

                        // allocate table-access-expressions
                        foreach (
                            KeyValuePair<ExprTableAccessNode, ExprTableAccessEvalStrategy> item in
                                startResult.TableAccessEvalStrategies)
                        {
                            aiExprSvc.GetTableAccessServices(item.Key).AssignService(agentInstanceId, item.Value);
                        }

                        // execute preloads, if any
                        foreach (var preload in startResult.PreloadList)
                        {
                            preload.ExecutePreload();
                        }

                        if (statementContext.ExtensionServicesContext != null &&
                            statementContext.ExtensionServicesContext.StmtResources != null)
                        {
                            statementContext.ExtensionServicesContext.StmtResources.StartContextPartition(
                                startResult, agentInstanceId);
                        }

                        // instantiate
                        return startResult;
                    }
                    finally
                    {
                        if (agentInstanceContext.StatementContext.EpStatementHandle.HasTableAccess)
                        {
                            agentInstanceContext.TableExprEvaluatorContext.ReleaseAcquiredLocks();
                        }
                    }
                }
            }
        }
    
        public static void EvaluateEventForStatement(EPServicesContext servicesContext, EventBean theEvent, IDictionary<String, Object> optionalTriggeringPattern, IList<AgentInstance> agentInstances)
        {
            if (theEvent != null) {
                EvaluateEventForStatementInternal(servicesContext, theEvent, agentInstances);
            }
            if (optionalTriggeringPattern != null)
            {
                // evaluation order definition is up to the originator of the triggering pattern
                foreach (var entry in optionalTriggeringPattern)
                {
                    if (entry.Value is EventBean)
                    {
                        EvaluateEventForStatementInternal(servicesContext, (EventBean) entry.Value, agentInstances);
                    }
                    else if (entry.Value is EventBean[])
                    {
                        var eventsArray = (EventBean[]) entry.Value;
                        foreach (var eventElement in eventsArray)
                        {
                            EvaluateEventForStatementInternal(servicesContext, eventElement, agentInstances);
                        }
                    }
                }
            }
        }
    
        private static void EvaluateEventForStatementInternal(EPServicesContext servicesContext, EventBean theEvent, IList<AgentInstance> agentInstances)
        {
            // context was created - reevaluate for the given event
            var callbacks = new ArrayDeque<FilterHandle>(2);
            servicesContext.FilterService.Evaluate(theEvent, callbacks);   // evaluates for ALL statements
            if (callbacks.IsEmpty()) {
                return;
            }
    
            // there is a single callback and a single context, if they match we are done
            if (agentInstances.Count == 1 && callbacks.Count == 1) {
                var agentInstance = agentInstances[0];
                if (agentInstance.AgentInstanceContext.StatementId.Equals(callbacks.First.StatementId)) {
                    Process(agentInstance, servicesContext, callbacks.Unwrap<EPStatementHandleCallback>(), theEvent);
                }
                return;
            }
    
            // use the right sorted/unsorted Map keyed by AgentInstance to sort
            var isPrioritized = servicesContext.ConfigSnapshot.EngineDefaults.ExecutionConfig.IsPrioritized;
            IDictionary<AgentInstance, Object> stmtCallbacks;
            if (!isPrioritized) {
                stmtCallbacks = new Dictionary<AgentInstance, Object>();
            }
            else {
                stmtCallbacks = new SortedDictionary<AgentInstance, Object>(AgentInstanceComparator.INSTANCE);
            }
    
            // process all callbacks
            foreach (var filterHandle in callbacks)
            {
                // determine if this filter entry applies to any of the affected agent instances
                var statementId = filterHandle.StatementId;
                AgentInstance agentInstanceFound = null;
                foreach (var agentInstance in agentInstances)
                {
                    if (agentInstance.AgentInstanceContext.StatementId.Equals(statementId))
                    {
                        agentInstanceFound = agentInstance;
                        break;
                    }
                }
                if (agentInstanceFound == null)
                {
                    // when the callback is for some other stmt
                    continue;
                }

                var handleCallback = (EPStatementHandleCallback) filterHandle;
                var handle = handleCallback.AgentInstanceHandle;

                // Self-joins require that the internal dispatch happens after all streams are evaluated.
                // Priority or preemptive settings also require special ordering.
                if (handle.CanSelfJoin || isPrioritized)
                {
                    var stmtCallback = stmtCallbacks.Get(agentInstanceFound);
                    if (stmtCallback == null)
                    {
                        stmtCallbacks.Put(agentInstanceFound, handleCallback);
                    }
                    else if (stmtCallback is ArrayDeque<EPStatementHandleCallback>)
                    {
                        var q = (ArrayDeque<EPStatementHandleCallback>) stmtCallback;
                        q.Add(handleCallback);
                    }
                    else
                    {
                        var q = new ArrayDeque<EPStatementHandleCallback>(4);
                        q.Add((EPStatementHandleCallback) stmtCallback);
                        q.Add(handleCallback);
                        stmtCallbacks.Put(agentInstanceFound, q);
                    }
                    continue;
                }

                // no need to be sorted, process
                Process(agentInstanceFound, servicesContext, Collections.SingletonList<EPStatementHandleCallback>(handleCallback), theEvent);
            }

            if (stmtCallbacks.IsEmpty()) {
                return;
            }
    
            // Process self-join or sorted prioritized callbacks
            foreach (var entry in stmtCallbacks)
            {
                var agentInstance = entry.Key;
                var callbackList = entry.Value;
                if (callbackList is ICollection<EPStatementHandleCallback>)
                {
                    Process(agentInstance, servicesContext, (ICollection<EPStatementHandleCallback>)callbackList, theEvent);
                }
                else
                {
                    Process(agentInstance, servicesContext, Collections.SingletonList((EPStatementHandleCallback) callbackList), theEvent);
                }
                if (agentInstance.AgentInstanceContext.EpStatementAgentInstanceHandle.IsPreemptive)
                {
                    return;
                }
            }
        }
    
        public static bool EvaluateFilterForStatement(EPServicesContext servicesContext, EventBean theEvent, AgentInstanceContext agentInstanceContext, FilterHandle filterHandle)
        {
            // context was created - reevaluate for the given event
            var callbacks = new ArrayDeque<FilterHandle>();
            servicesContext.FilterService.Evaluate(theEvent, callbacks, agentInstanceContext.StatementContext.StatementId);
    
            try
            {
                servicesContext.VariableService.SetLocalVersion();
    
                // sub-selects always go first
                foreach (var handle in callbacks)
                {
                    if (handle == filterHandle) {
                        return true;
                    }
                }
    
                agentInstanceContext.EpStatementAgentInstanceHandle.InternalDispatch();
    
            }
            catch (Exception ex) {
                servicesContext.ExceptionHandlingService.HandleException(ex, agentInstanceContext.EpStatementAgentInstanceHandle);
            }
    
            return false;
        }
    
        public static StopCallback GetStopCallback(IList<StopCallback> stopCallbacks, AgentInstanceContext agentInstanceContext)
        {
            var stopCallbackArr = stopCallbacks.ToArray();
            return () => StopSafe(
                agentInstanceContext.TerminationCallbackRO, stopCallbackArr,
                agentInstanceContext.StatementContext);
        }

        private static void Process(
            AgentInstance agentInstance,
            EPServicesContext servicesContext,
            ICollection<EPStatementHandleCallback> callbacks,
            EventBean theEvent)
        {
            var agentInstanceContext = agentInstance.AgentInstanceContext;
            using (agentInstance.AgentInstanceContext.AgentInstanceLock.WriteLock.Acquire())
            {
                try
                {
                    servicesContext.VariableService.SetLocalVersion();

                    // sub-selects always go first
                    foreach (var handle in callbacks)
                    {
                        var callback = (EPStatementHandleCallback) handle;
                        if (callback.AgentInstanceHandle != agentInstanceContext.EpStatementAgentInstanceHandle)
                        {
                            continue;
                        }
                        callback.FilterCallback.MatchFound(theEvent, null);
                    }

                    agentInstanceContext.EpStatementAgentInstanceHandle.InternalDispatch();
                }
                catch (Exception ex)
                {
                    servicesContext.ExceptionHandlingService.HandleException(
                        ex, agentInstanceContext.EpStatementAgentInstanceHandle);
                }
                finally
                {
                    if (agentInstanceContext.StatementContext.EpStatementHandle.HasTableAccess)
                    {
                        agentInstanceContext.TableExprEvaluatorContext.ReleaseAcquiredLocks();
                    }
                }
            }
        }
    }
}