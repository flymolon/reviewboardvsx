﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace ReviewBoardVsx.Package.Tracker
{
    /// <summary>
    /// Ideas taken from:
    /// Project.cs
    /// 
    /// See Also:
    /// http://msdn.microsoft.com/en-us/library/bb165701.aspx
    /// 
    /// What is w/ these classes?
    /// Microsoft.VisualStudio.Package.ProjectPackage.cs
    /// Microsoft.VisualStudio.Package.SolutionListenerForProjectEvents.cs
    /// ...ProjectDocumentsListener.cs
    /// </summary>
    public class MySolutionTracker : SolutionListener
    {
        /// <summary>
        /// Private mutable collection of detected solution/project changes.
        /// </summary>
        private readonly PostReview.SubmitItemCollection changes = new PostReview.SubmitItemCollection();

        /// <summary>
        /// Public read-only collection of detected solution/project changes
        /// </summary>
        public PostReview.SubmitItemReadOnlyCollection Changes { get { return changes.AsReadOnly(); } }

        /// <summary>
        /// Map of solution/project file paths to solution/project names.
        /// This allows us to easily find the solution/project name given just the file path.
        /// </summary>
        private readonly Dictionary<string, string> mapItemProjects = new Dictionary<string,string>();

        public readonly BackgroundWorker BackgroundInitialSolutionCrawl;

        private MyProjectTracker projectTracker;
        private MyFileTracker fileTracker;

        public MySolutionTracker(IServiceProvider serviceProvider, RunWorkerCompletedEventHandler runWorkerCompleted)
            : base(serviceProvider)
        {
            // The BackgroundWorker is created and managed by this class because
            // it needs to re-fire every time a new Solution is opened.
            // TODO:(pv) It would be nice to move BackgroundInitialSolutionCrawl and Changes outside of this class.
            //      This could be done by firing public events for SolutionOpen and SolutionClose.
            BackgroundInitialSolutionCrawl = new BackgroundWorker();
            BackgroundInitialSolutionCrawl.WorkerReportsProgress = true;
            BackgroundInitialSolutionCrawl.DoWork += backgroundInitialSolutionCrawl_DoWork;
            BackgroundInitialSolutionCrawl.RunWorkerCompleted += BackgroundInitialSolutionCrawl_RunWorkerCompleted;
            if (runWorkerCompleted != null)
            {
                BackgroundInitialSolutionCrawl.RunWorkerCompleted += runWorkerCompleted;
            }

            // Subscribes to Solution events
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                MyLog.DebugEnter(this, "Dispose(" + disposing + ")");
                DisposeProjectAndFileTrackers();
                base.Dispose(disposing);
            }
            finally
            {
                MyLog.DebugLeave(this, "Dispose(" + disposing + ")");
            }
        }

        protected void DisposeProjectAndFileTrackers()
        {
                if (fileTracker != null)
                {
                    fileTracker.Dispose();
                    fileTracker = null;
                }
                if (projectTracker != null)
                {
                    projectTracker.Dispose();
                    projectTracker = null;
                }
        }

        /// <summary>
        /// Clears the changes collection and crawls every item in the solution looking for current changes and subscribing to future changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void backgroundInitialSolutionCrawl_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                MyLog.DebugEnter(this, "backgroundInitialSolutionCrawl_DoWork");

                lock (changes)
                {
                    changes.Clear();
                }

                try
                {
                    MyLog.DebugEnter(this, "EnumHierarchyItems");
                    EnumHierarchyItems((IVsHierarchy)Solution, VSConstants.VSITEMID_ROOT, 0, true, true);
                }
                finally
                {
                    MyLog.DebugLeave(this, "EnumHierarchyItems");
                }
            }
            finally
            {
                MyLog.DebugLeave(this, "backgroundInitialSolutionCrawl_DoWork");
            }
        }

        /// <summary>
        /// Clear the changes collection if the solution crawl encountered an error or was canceled.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BackgroundInitialSolutionCrawl_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                lock (changes)
                {
                    changes.Clear();
                }
            }
        }

        #region event handlers

        public override int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterOpenSolution(" + pUnkReserved + ", " + fNewSolution + ")");

                DisposeProjectAndFileTrackers();

                // Subscribe to Project events
                projectTracker = new MyProjectTracker(this);
                projectTracker.Initialize();

                // Each file encountered during the crawl will be separately tracked for future changes.
                fileTracker = new MyFileTracker(this);

                // TODO:(pv) What if another crawl is already running?
                BackgroundInitialSolutionCrawl.RunWorkerAsync();

                return VSConstants.S_OK;
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterOpenSolution(" + pUnkReserved + ", " + fNewSolution + ")");
            }
        }

        public override int OnQueryCloseSolution(object pUnkReserved, ref int cancel)
        {
            try
            {
                MyLog.DebugEnter(this, "OnQueryCloseSolution(" + pUnkReserved + ", " + cancel + ")");
                return VSConstants.S_OK; // ignore
            }
            finally
            {
                MyLog.DebugLeave(this, "OnQueryCloseSolution(" + pUnkReserved + ", " + cancel + ")");
            }
        }

        public override int OnBeforeCloseSolution(object pUnkReserved)
        {
            try
            {
                MyLog.DebugEnter(this, "OnBeforeCloseSolution(" + pUnkReserved + ")");
                return VSConstants.S_OK; // ignore
            }
            finally
            {
                MyLog.DebugLeave(this, "OnBeforeCloseSolution(" + pUnkReserved + ")");
            }
        }

        public override int OnAfterCloseSolution(object reserved)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterCloseSolution(" + reserved + ")");

                DisposeProjectAndFileTrackers();

                if (BackgroundInitialSolutionCrawl.IsBusy)
                {
                    BackgroundInitialSolutionCrawl.CancelAsync();
                }

                return VSConstants.S_OK;
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterCloseSolution(" + reserved + ")");
            }
        }

        /// <summary>
        /// The project has been opened.
        /// </summary>
        /// <param name="hierarchy">Pointer to the IVsHierarchy interface of the project being loaded.</param>
        /// <param name="added">1 if the project is added to the solution after the solution is opened. 0 if the project is added to the solution while the solution is being opened.</param>
        /// <returns></returns>
        public override int OnAfterOpenProject(IVsHierarchy hierarchy, int added)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterOpenProject(" + hierarchy + ", " + added + ")");
                // TODO:(pv) Start crawling the project items
                return VSConstants.S_OK;
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterOpenProject(" + hierarchy + ", " + added + ")");
            }
        }

        public override int OnAfterRenameProject(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterRenameProject(" + hierarchy + ")");
                // TODO:(pv) Rename the project item
                return VSConstants.S_OK;
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterRenameProject(" + hierarchy + ")");
            }
        }

        /// <summary>
        /// The project is about to be closed.
        /// </summary>
        /// <param name="hierarchy">Pointer to the IVsHierarchy interface of the project being closed.</param>
        /// <param name="removed">1 if the project was removed from the solution before the solution was closed. 0 if the project was removed from the solution while the solution was being closed.</param>
        /// <returns></returns>
        public override int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
        {
            try
            {
                MyLog.DebugEnter(this, "OnBeforeCloseProject(" + hierarchy + ", " + removed + ")");
                // TODO:(pv) Remove the project items
                return VSConstants.S_OK;
            }
            finally
            {
                MyLog.DebugLeave(this, "OnBeforeCloseProject(" + hierarchy + ", " + removed + ")");
            }
        }

        public override int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel)
        {
            try
            {
                MyLog.DebugEnter(this, "OnQueryCloseProject(" + hierarchy + ", " + removing + ", " + cancel + ")");
                return VSConstants.S_OK; // ignore
            }
            finally
            {
                MyLog.DebugLeave(this, "OnQueryCloseProject(" + hierarchy + ", " + removing + ", " + cancel + ")");
            }
        }

        public override int OnAfterClosingChildren(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterClosingChildren(" + hierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterClosingChildren(" + hierarchy + ")");
            }
        }

        public override int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterLoadProject(" + stubHierarchy + ", " + realHierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterLoadProject(" + stubHierarchy + ", " + realHierarchy + ")");
            }
        }

        public override int OnAfterMergeSolution(object pUnkReserved)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterMergeSolution(" + pUnkReserved + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterMergeSolution(" + pUnkReserved + ")");
            }
        }

        public override int OnAfterOpeningChildren(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterOpeningChildren(" + hierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterOpeningChildren(" + hierarchy + ")");
            }
        }

        public override int OnBeforeClosingChildren(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnBeforeClosingChildren(" + hierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnBeforeClosingChildren(" + hierarchy + ")");
            }
        }

        public override int OnBeforeOpeningChildren(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnBeforeOpeningChildren(" + hierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnBeforeOpeningChildren(" + hierarchy + ")");
            }
        }

        public override int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnBeforeUnloadProject(" + realHierarchy + ", " + stubHierarchy + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnBeforeUnloadProject(" + realHierarchy + ", " + stubHierarchy + ")");
            }
        }

        public override int OnQueryUnloadProject(IVsHierarchy realHierarchy, ref int cancel)
        {
            try
            {
                MyLog.DebugEnter(this, "OnQueryUnloadProject(" + realHierarchy + ", " + cancel + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnQueryUnloadProject(" + realHierarchy + ", " + cancel + ")");
            }
        }

        public override int OnAfterAsynchOpenProject(IVsHierarchy hierarchy, int added)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterAsynchOpenProject(" + hierarchy + ", " + added + ")");
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterAsynchOpenProject(" + hierarchy + ", " + added + ")");
            }
        }

        public override int OnAfterChangeProjectParent(IVsHierarchy hierarchy)
        {
            try
            {
                MyLog.DebugEnter(this, "OnAfterChangeProjectParent(" + hierarchy + ")");
                // log this to see if needed
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnAfterChangeProjectParent(" + hierarchy + ")");
            }
        }

        public override int OnQueryChangeProjectParent(IVsHierarchy hierarchy, IVsHierarchy newParentHier, ref int cancel)
        {
            try
            {
                MyLog.DebugEnter(this, "OnQueryChangeProjectParent(" + hierarchy + ", " + newParentHier + ", " + cancel + ")");
                // log this to see if needed
                return VSConstants.S_OK; // We are not interested in this one
            }
            finally
            {
                MyLog.DebugLeave(this, "OnQueryChangeProjectParent(" + hierarchy + ", " + newParentHier + ", " + cancel + ")");
            }
        }

        #endregion

        #region crawler method(s)

        /// <summary>
        /// Code almost 100% taken from VS SDK Example: SolutionHierarchyTraversal
        /// </summary>
        /// <param name="hierarchy"></param>
        /// <param name="itemid"></param>
        /// <param name="recursionLevel"></param>
        /// <param name="hierIsSolution"></param>
        /// <param name="visibleNodesOnly"></param>
        /// <param name="changes"></param>
        /// <returns>true if the caller should continue, false if the caller should stop</returns>
        private bool EnumHierarchyItems(IVsHierarchy hierarchy, uint itemid, int recursionLevel, bool hierIsSolution, bool visibleNodesOnly)//, PostReview.SubmitItemCollection changes)
        {
            if (BackgroundInitialSolutionCrawl != null && BackgroundInitialSolutionCrawl.CancellationPending)
            {
                return false;
            }

            int hr;
            IntPtr nestedHierarchyObj;
            uint nestedItemId;
            Guid hierGuid = typeof(IVsHierarchy).GUID;

            // Check first if this node has a nested hierarchy. If so, then there really are two 
            // identities for this node: 1. hierarchy/itemid 2. nestedHierarchy/nestedItemId.
            // We will recurse and call EnumHierarchyItems which will display this node using
            // the inner nestedHierarchy/nestedItemId identity.
            hr = hierarchy.GetNestedHierarchy(itemid, ref hierGuid, out nestedHierarchyObj, out nestedItemId);
            if (VSConstants.S_OK == hr && IntPtr.Zero != nestedHierarchyObj)
            {
                IVsHierarchy nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHierarchyObj) as IVsHierarchy;
                Marshal.Release(nestedHierarchyObj);    // we are responsible to release the refcount on the out IntPtr parameter
                if (nestedHierarchy != null)
                {
                    // Display name and type of the node in the Output Window
                    EnumHierarchyItems(nestedHierarchy, nestedItemId, recursionLevel, false, visibleNodesOnly);
                }
            }
            else
            {
                object pVar;

                // Display name and type of the node in the Output Window
                ProcessNode(hierarchy, itemid, recursionLevel);

                recursionLevel++;

                // Get the first child node of the current hierarchy being walked
                // NOTE: to work around a bug with the Solution implementation of VSHPROPID_FirstChild,
                // we keep track of the recursion level. If we are asking for the first child under
                // the Solution, we use VSHPROPID_FirstVisibleChild instead of _FirstChild. 
                // In VS 2005 and earlier, the Solution improperly enumerates all nested projects
                // in the Solution (at any depth) as if they are immediate children of the Solution.
                // Its implementation _FirstVisibleChild is correct however, and given that there is
                // not a feature to hide a SolutionFolder or a Project, thus _FirstVisibleChild is 
                // expected to return the identical results as _FirstChild.
                hr = hierarchy.GetProperty(itemid,
                    ((visibleNodesOnly || (hierIsSolution && recursionLevel == 1) ?
                        (int)__VSHPROPID.VSHPROPID_FirstVisibleChild : (int)__VSHPROPID.VSHPROPID_FirstChild)),
                    out pVar);
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
                if (VSConstants.S_OK == hr)
                {
                    // We are using Depth first search so at each level we recurse to check if the node has any children
                    // and then look for siblings.
                    uint childId = MyPackage.GetItemId(pVar);
                    while (childId != VSConstants.VSITEMID_NIL)
                    {
                        if (!EnumHierarchyItems(hierarchy, childId, recursionLevel, false, visibleNodesOnly))
                        {
                            break;
                        }

                        // NOTE: to work around a bug with the Solution implementation of VSHPROPID_NextSibling,
                        // we keep track of the recursion level. If we are asking for the next sibling under
                        // the Solution, we use VSHPROPID_NextVisibleSibling instead of _NextSibling. 
                        // In VS 2005 and earlier, the Solution improperly enumerates all nested projects
                        // in the Solution (at any depth) as if they are immediate children of the Solution.
                        // Its implementation   _NextVisibleSibling is correct however, and given that there is
                        // not a feature to hide a SolutionFolder or a Project, thus _NextVisibleSibling is 
                        // expected to return the identical results as _NextSibling.
                        hr = hierarchy.GetProperty(childId,
                            ((visibleNodesOnly || (hierIsSolution && recursionLevel == 1)) ?
                                (int)__VSHPROPID.VSHPROPID_NextVisibleSibling : (int)__VSHPROPID.VSHPROPID_NextSibling),
                            out pVar);
                        if (VSConstants.S_OK == hr)
                        {
                            childId = MyPackage.GetItemId(pVar);
                        }
                        else
                        {
                            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
                            break;
                        }
                    }
                }
            }

            return (BackgroundInitialSolutionCrawl == null || !BackgroundInitialSolutionCrawl.CancellationPending);
        }

        private void ProcessNode(IVsHierarchy hierarchy, uint itemId, int recursionLevel)
        {
            try
            {
                MyLog.DebugEnter(this, "ProcessNode(hierarchy, " + itemId + ", " + recursionLevel + ")");

                int hr;

                Object oRootName;
                hr = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out oRootName);
                if (ErrorHandler.Failed(hr))
                {
                    MyPackage.OutputGeneral("ERROR: Could not get root name of item #" + itemId);
                    ErrorHandler.ThrowOnFailure(hr);
                }
                string rootName = oRootName as string;
                if (String.IsNullOrEmpty(rootName))
                {
                    rootName = Resources.RootUnknown;
                }
                Debug.WriteLine("rootName=" + rootName);

                string itemName;
                hr = hierarchy.GetCanonicalName(itemId, out itemName);
                if (ErrorHandler.Failed(hr))
                {
                    switch (hr)
                    {
                        case VSConstants.E_NOTIMPL:
                            // ignore; Nothing to do if we cannot get the file name, but below logic can handle null/empty name...
                            itemName = null;
                            break;
                        default:
                            MyPackage.OutputGeneral("ERROR: Could not get canonical name of item #" + itemId);
                            ErrorHandler.ThrowOnFailure(hr);
                            break;
                    }
                }
                Debug.WriteLine("itemName=\"" + itemName + "\"");

#if DEBUG && false
                if (BackgroundWorker != null && !String.IsNullOrEmpty(itemName))
                {
                    // Temporary until we call AddFilePathIfChanged after we find out what the item type is
                    BackgroundWorker.ReportProgress(0, itemName);
                }
#endif

                Guid guidTypeNode;
                hr = hierarchy.GetGuidProperty(itemId, (int)__VSHPROPID.VSHPROPID_TypeGuid, out guidTypeNode);
                if (ErrorHandler.Failed(hr))
                {
                    switch (hr)
                    {
                        case VSConstants.E_NOTIMPL:
                            Debug.WriteLine("Guid property E_NOTIMPL for item #" + itemId + " \"" + itemName + "\"; assuming virtual/reference item and ignoring");
                            // ignore; Below logic can handle Guid.Empty
                            guidTypeNode = Guid.Empty;
                            break;
                        case VSConstants.DISP_E_MEMBERNOTFOUND:
                            Debug.WriteLine("Guid property DISP_E_MEMBERNOTFOUND for item #" + itemId + " \"" + itemName + "\"; assuming reference item and ignoring");
                            guidTypeNode = Guid.Empty;
                            break;
                        default:
                            MyPackage.OutputGeneral("ERROR: Could not get type guid of item #" + itemId + " \"" + itemName + "\"");
                            ErrorHandler.ThrowOnFailure(hr);
                            break;
                    }
                }
                Debug.WriteLine("guidTypeNode=" + guidTypeNode);

                //
                // Intentionally ordered from most commonly expected to least commonly expected...
                //
                if (Guid.Equals(guidTypeNode, VSConstants.GUID_ItemType_PhysicalFile))
                {
                    AddFilePathIfChanged(itemName, rootName);
                }
                else if (itemId == VSConstants.VSITEMID_ROOT)
                {
                    IVsProject project = hierarchy as IVsProject;
                    if (project != null)
                    {
                        string projectFile;
                        project.GetMkDocument(VSConstants.VSITEMID_ROOT, out projectFile);
                        AddFilePathIfChanged(projectFile, rootName);
                    }
                    else
                    {
                        IVsSolution solution = hierarchy as IVsSolution;
                        if (solution != null)
                        {
                            rootName = Resources.RootSolution;

                            string solutionDirectory, solutionFile, solutionUserOptions;
                            ErrorHandler.ThrowOnFailure(solution.GetSolutionInfo(out solutionDirectory, out solutionFile, out solutionUserOptions));
                            AddFilePathIfChanged(solutionFile, rootName);
                        }
                        else
                        {
                            MyPackage.OutputGeneral("ERROR: itemid==VSITEMID_ROOT, but hierarchy is neither Solution or Project");
                            ErrorHandler.ThrowOnFailure(VSConstants.E_UNEXPECTED);
                        }
                    }
                }
#if DEBUG
                else if (Guid.Equals(guidTypeNode, VSConstants.GUID_ItemType_PhysicalFolder))
                {
                    Debug.WriteLine("ignoring GUID_ItemType_PhysicalFolder");
                    // future enumeration will handle any individual subitems in this folder...
                }
                else if (Guid.Equals(guidTypeNode, VSConstants.GUID_ItemType_VirtualFolder))
                {
                    Debug.WriteLine("ignoring GUID_ItemType_VirtualFolder");
                    // future enumeration will handle any individual subitems in this virtual folder...
                }
                else if (Guid.Equals(guidTypeNode, VSConstants.GUID_ItemType_SubProject))
                {
                    Debug.WriteLine("ignoring GUID_ItemType_SubProject");
                    // future enumeration will handle any individual subitems in this sub project...
                }
                else if (Guid.Equals(guidTypeNode, Guid.Empty))
                {
                    Debug.WriteLine("ignoring itemName=" + itemName + "; guidTypeNode == Guid.Empty");
                    // future enumeration will handle any individual subitems in this item...
                }
                else
                {
                    MyPackage.OutputGeneral("ERROR: Unhandled node item/type itemName=" + itemName + ", guidTypeNode=" + guidTypeNode);
                    ErrorHandler.ThrowOnFailure(VSConstants.E_UNEXPECTED);
                }
#endif
            }
            finally
            {
                MyLog.DebugLeave(this, "ProcessNode(hierarchy, " + itemId + ", " + recursionLevel + ")");
            }
        }

        public void AddFilePathIfChanged(string filePath)
        {
            string project;
            if (!mapItemProjects.TryGetValue(filePath.ToLower(), out project))
            {
                project = Resources.RootUnknown;
            }
            AddFilePathIfChanged(filePath, project);
        }

        public void AddFilePathIfChanged(string filePath, string project)
        {
            try
            {
                MyLog.DebugEnter(this, "AddFilePathIfChanged(\"" + filePath + "\", \"" + project + "\")");

                filePath = MyUtils.GetCasedFilePath(filePath);
                if (String.IsNullOrEmpty(filePath))
                {
                    // If we got this far then the *Solution* says there is a file.
                    // However, the file can be an external/symbolic link/reference, not an actual file.
                    // Ignore this situation and just continue the enumeration.
                    return;
                }

                if (BackgroundInitialSolutionCrawl != null && BackgroundInitialSolutionCrawl.IsBusy)
                {
                    // Percent is currently always 0, since our progress is indeterminate
                    // TODO:(pv) Find some way to determine # of nodes in tree *before* processing
                    //      Maybe do a quick first pass w/ no post-review?
                    // NOTE:(pv) I did once have the debugger halt here complaining invalid state that the form is not active
                    BackgroundInitialSolutionCrawl.ReportProgress(0, filePath);
                }

                string diff;
                PostReview.DiffType diffType = PostReview.DiffFile(filePath, out diff);

                switch (diffType)
                {
                    case PostReview.DiffType.Added:
                    case PostReview.DiffType.Changed:
                    case PostReview.DiffType.Modified:
                        PostReview.SubmitItem change = new PostReview.SubmitItem(filePath, project, diffType, diff);
                        lock (changes)
                        {
                            changes.Add(change);
                        }
                        break;
                    case PostReview.DiffType.External:
                        // TODO:(pv) Even add External items?
                        // Doesn't make much sense, since they won't diff during post-review.exe submit.
                        // Maybe could add them and group them at bottom as disabled.
                        // This also doesn't make sense, since it assumes post-review has ability to add files to SCM.
                        break;
                    case PostReview.DiffType.Normal:
                        // TODO:(pv) Even add normal items?
                        // Doesn't make much sense, since they won't diff during post-review.exe submit.
                        // Maybe could add them and group them at bottom as disabled.
                        // This also doesn't make sense, since it assumes post-review has ability to add files to SCM.
                        break;
                    default:
                        string message = String.Format("Unhandled DiffType={0}", diffType);
                        throw new ArgumentOutOfRangeException("diffType", message);
                }

                filePath = filePath.ToLower();

                // *Always* track the file for *future* changes, even if there are no *current* changes.
                // This is a no-op if the path is already being tracked.
                fileTracker.Subscribe(filePath, true);

                // Map the file path to a project so that future file changes can find out what project the file is in given just the file path.
                // This will overwrite any existing value...which seems fine for now.
                mapItemProjects[filePath] = project;
            }
            finally
            {
                MyLog.DebugLeave(this, "AddFilePathIfChanged(\"" + filePath + "\", \"" + project + "\")");
            }
        }

        #endregion crawler method(s)
    }
}