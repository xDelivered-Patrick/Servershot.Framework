﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Azure.Workflow.Core.Architecture;
using Azure.Workflow.Core.Entities;
using Azure.Workflow.Core.Interfaces;
using Azure.Workflow.Core.Plugins;

namespace Azure.Workflow.Core.Implementation
{
    public class WorkflowSession
    {
        protected WorkflowSession()
        {
            RunningModules = new ObservableCollection<IWorkflowModule>();
            Modules = new List<IWorkflowModule>();
            Plugins = new List<WorkflowSessionPluginBase>();

            HookRunningModules();
        }

        public ObservableCollection<IWorkflowModule> RunningModules { get; set; }
        public List<IWorkflowModule> Modules { get; set; }
        internal List<WorkflowSessionPluginBase> Plugins { get; set; }
        internal ICloudQueueFactory CloudQueueFactory { get; set; }

        public DateTime Started { get; private set; }
        public DateTime Ended { get; private set; }

        public TimeSpan TotalDuration
        {
            get { return Ended.Subtract(Started); }
        }

        public WorkflowSessionSettings Settings { get; set; }

        public Guid Guid
        {
            get { return Guid.NewGuid(); }
        }

        public string SessionName { get; set; }
        public event Action<WorkflowSession> OnSessionFinished;
        public event Action<IWorkflowModule, string> OnFailure;

        public async Task Start()
        {
            ValidateStart();
            Started = DateTime.Now;

            //validate the plugins
            Plugins.ForEach(ValidatePlugin);

            //inform plugins we have started so they can hook to events
            Plugins.ForEach(x => x.OnSessionStarted(this));
            Modules.ForEach(x => RunningModules.Add(x));

            //run the modules
            await Task.WhenAll(Modules.Select(x => x.StartAsync()));

            //inform session has finished
            RegisterFinished(this);
            Ended = DateTime.Now;
        }

        #region Helpers

        private void ValidateStart()
        {
            if (Settings == null)
            {
                Settings = new WorkflowSessionSettings();
            }

            if (CloudQueueFactory == null)
            {
                throw new AzureWorkflowConfigurationException("There must be a CloudQueueFactory attached in order to start the session", null);
            }
        }

        public void ValidatePlugin(WorkflowSessionPluginBase plugin)
        {
            string message = "";
            if ((message = plugin.Validate(this)) != null)
            {
                throw new AzureWorkflowConfigurationException(message, null);
            }
        }

        private void HookRunningModules()
        {
            RunningModules.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    var newItem = args.NewItems[0] as IWorkflowModule;
                    newItem.Queue = CloudQueueFactory.CreateQueue(newItem);
                    newItem.Session = this;
                    HookModule(newItem);
                }
            };
        }

        private void HookModule(IWorkflowModule newItem)
        {
            newItem.OnFailure += (s, exceptions) =>
            {
                if (OnFailure != null)
                {
                    OnFailure(newItem, "Module " + newItem.QueueName + " failed : " + s);
                }
            };
        }

        private void RegisterFinished(WorkflowSession session)
        {
            if (OnSessionFinished != null)
            {
                OnSessionFinished(session);
            }
        }

        #endregion

        #region Builder

        public static WorkflowSessionBuilder StartBuild()
        {
            return new WorkflowSessionBuilder(new WorkflowSession());
        }

        public static WorkflowSessionBuilder StartBuildWithSession(WorkflowSession session)
        {
            return new WorkflowSessionBuilder(session);
        }

        #endregion

        #region Mediator

        public virtual void AddToQueue(Type workflowModuleType, IEnumerable<object> batch)
        {
            Type type = workflowModuleType;
            IWorkflowModule module = RunningModules.SingleOrDefault(x => x.GetType() == type);
            module.Queue.AddToAsync(batch);
        }

        #endregion
    }
}