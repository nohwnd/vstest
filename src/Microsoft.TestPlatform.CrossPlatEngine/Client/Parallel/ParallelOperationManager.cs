// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;

    /// <summary>
    /// Abstract class having common parallel manager implementation
    /// </summary>
    internal abstract class ParallelOperationManager<T, U> : IParallelOperationManager, IDisposable
    {
        #region ConcurrentManagerInstanceData

        protected Func<string, T> CreateNewConcurrentManager { get; set; }

        /// <summary>
        /// Gets a value indicating whether hosts are shared.
        /// </summary>
        protected bool SharedHosts { get; private set; }

        /// <summary>
        /// Holds all active managers, so we can do actions on all of them, like initialize, run, cancel or close.
        /// </summary>
        private IDictionary<T, U> concurrentManagerHandlerMap;

        /// <summary>
        /// Singleton Instance of this class
        /// </summary>
        protected static T instance = default(T);

        /// <summary>
        /// Default number of Processes
        /// </summary>
        private int currentParallelLevel = 0;

        protected int MaxParallelLevel { get; private set; }

        #endregion

        #region Concurrency Keeper Objects

        /// <summary>
        /// LockObject to iterate our sourceEnumerator in parallel
        /// We can use the sourceEnumerator itself as lockObject, but since its a changing object - it's risky to use it as one
        /// </summary>
        protected object sourceEnumeratorLockObject = new object();

        #endregion

        protected ParallelOperationManager(Func<string, T> createNewManager, int parallelLevel, bool sharedHosts)
        {
            // CreataNewConcurrentManager should probably take capabilities instead of just string, and we should add those same capabilities to the source? But it's kept this way so we don't have to change how we check if host can run given config.
            this.CreateNewConcurrentManager = createNewManager;
            // REVIEW: this option does not make sense when some host can be shared and other can't.
            // this.SharedHosts = sharedHosts;
            this.SharedHosts = false;

            // Update Parallel Level
            // REVIEW: this "pre-starts" testhosts so we have a pool of them, this is the reason the number or parallel hosts is capped to the amount of sources so we don't "pre-start" too many of them
            // instead we should take each source, look if it can be run by shared host, and if so try to grab a free host, run new one if we are below parallel level, or wait if we are at parallel level and everyone is busy
            // if we have non-shared host we do just the two last options, run new one if current count is under parallel level, or wait till we can run new one.
            // this.UpdateParallelLevel(parallelLevel);
            if (this.concurrentManagerHandlerMap == null) {
                this.concurrentManagerHandlerMap = new ConcurrentDictionary<T, U>();
            }

            this.MaxParallelLevel = parallelLevel;
        }

        /// <summary>
        /// Remove and dispose a manager from concurrent list of manager.
        /// </summary>
        /// <param name="manager">Manager to remove</param>
        public void RemoveManager(T manager)
        {
            this.concurrentManagerHandlerMap.Remove(manager);
        }

        /// <summary>
        /// Add a manager in concurrent list of manager.
        /// </summary>
        /// <param name="manager">Manager to add</param>
        /// <param name="handler">eventHandler of the manager</param>
        public void AddManager(T manager, U handler)
        {
            this.concurrentManagerHandlerMap.Add(manager, handler);
        }

        /// <summary>
        /// Update event handler for the manager.
        /// If it is a new manager, add this.
        /// </summary>
        /// <param name="manager">Manager to update</param>
        /// <param name="handler">event handler to update for manager</param>
        public void UpdateHandlerForManager(T manager, U handler)
        {
            if(this.concurrentManagerHandlerMap.ContainsKey(manager))
            {
                this.concurrentManagerHandlerMap[manager] = handler;
            }
            else
            {
                this.AddManager(manager, handler);
            }
        }

        /// <summary>
        /// Get the event handler associated with the manager.
        /// </summary>
        /// <param name="manager">Manager</param>
        public U GetHandlerForGivenManager(T manager)
        {
            return this.concurrentManagerHandlerMap[manager];
        }

        /// <summary>
        /// Get total number of active concurrent manager
        /// </summary>
        public int GetConcurrentManagersCount()
        {
            return this.concurrentManagerHandlerMap.Count;
        }

        /// <summary>
        /// Get instances of all active concurrent manager
        /// </summary>
        public IEnumerable<T> GetConcurrentManagerInstances()
        {
            return this.concurrentManagerHandlerMap.Keys.ToList();
        }


        /// <summary>
        /// Updates the Concurrent Executors according to new parallel setting
        /// </summary>
        /// <param name="newParallelLevel">Number of Parallel Executors allowed</param>
        public void UpdateParallelLevel(int newParallelLevel)
        {
            var a = true;
            if (this.concurrentManagerHandlerMap == null)
            {
                if (a)
                {
                    throw new Exception("This should not be used anymore, to pre-start hosts");
                }
                // not initialized yet
                // create rest of concurrent clients other than default one
                this.concurrentManagerHandlerMap = new ConcurrentDictionary<T, U>();
                for (int i = 0; i < newParallelLevel; i++)
                {
                    this.AddManager(this.CreateNewConcurrentManager(null), default(U));
                }
            }
            else if (this.currentParallelLevel != newParallelLevel)
            {
                // If number of concurrent clients is less than the new level
                // Create more concurrent clients and update the list
                if (this.currentParallelLevel < newParallelLevel)
                {
                    // This path does not even seem to be used anywhere.
                    if (a)
                    {
                        throw new Exception("This should not be used anymore, to ensure we add more hosts.");
                    }

                    for (int i = 0; i < newParallelLevel - this.currentParallelLevel; i++)
                    {
                        this.AddManager(this.CreateNewConcurrentManager(null), default(U));
                    }
                }
                else
                {
                    // If number of concurrent clients is more than the new level
                    // Dispose off the extra ones
                    int managersCount = currentParallelLevel - newParallelLevel;

                    foreach(var concurrentManager in this.GetConcurrentManagerInstances())
                    {
                        if (managersCount == 0)
                        {
                            break;
                        }
                        else
                        {
                            this.RemoveManager(concurrentManager);
                            managersCount--;
                        }
                    }
                }
            }

            // Update current parallel setting to new one
            this.currentParallelLevel = newParallelLevel;
        }

        public void Dispose()
        {
            if (this.concurrentManagerHandlerMap != null)
            {
                foreach (var managerInstance in this.GetConcurrentManagerInstances())
                {
                    this.RemoveManager(managerInstance);
                }
            }

            instance = default(T);
        }

        protected void DoActionOnAllManagers(Action<T> action, bool doActionsInParallel = false)
        {
            if (this.concurrentManagerHandlerMap != null && this.concurrentManagerHandlerMap.Count > 0)
            {
                int i = 0;
                var actionTasks = new Task[this.concurrentManagerHandlerMap.Count];
                foreach (var client in this.GetConcurrentManagerInstances())
                {
                    // Read the array before firing the task - beware of closures
                    if (doActionsInParallel)
                    {
                        actionTasks[i] = Task.Run(() => action(client));
                        i++;
                    }
                    else
                    {
                        this.DoManagerAction(() => action(client));
                    }
                }

                if (doActionsInParallel)
                {
                    this.DoManagerAction(() => Task.WaitAll(actionTasks));
                }
            }
        }

        private void DoManagerAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Exception can occur if we are trying to cancel a test run on an executor where test run is not even fired
                // we can safely ignore that as user is just canceling the test run and we don't care about additional parallel executors
                // as we will be disposing them off soon anyway
                if (EqtTrace.IsWarningEnabled)
                {
                    EqtTrace.Warning("AbstractParallelOperationManager: Exception while invoking an action on Proxy Manager instance: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Fetches the next data object for the concurrent executor to work on
        /// </summary>
        /// <param name="source">source data to work on - source file or testCaseList</param>
        /// <returns>True, if data exists. False otherwise</returns>
        protected bool TryFetchNextSource<Y>(IEnumerator enumerator, out Y source)
        {
            source = default(Y);
            var hasNext = false;
            lock (this.sourceEnumeratorLockObject)
            {
                if (enumerator != null && enumerator.MoveNext())
                {
                    source = (Y)enumerator.Current;
                    hasNext = source != null;
                }
            }

            return hasNext;
        }
    }
}

