#if UNITY_EDITOR
using FIMSpace.FEditor;
using UnityEditor;
#endif
using FIMSpace.Generating.Planner.Nodes;
using FIMSpace.Graph;
using System;
using System.Collections.Generic;
using UnityEngine;
using FIMSpace.Generating.Rules.QuickSolutions;
using FIMSpace.Generating.Checker;

namespace FIMSpace.Generating.Planning.PlannerNodes
{

    /// <summary>
    /// It's always sub-asset -> it's never project file asset
    /// </summary>
    public abstract partial class PlannerRuleBase : PGGPlanner_NodeBase
    {
        public static bool Debugging = false;

        /// <summary> Warning! Duplicate is refering to root project planner, In the nodes logics you can use CurrentExecutingPlanner for instance planner reference </summary>
        [HideInInspector] public FieldPlanner ParentPlanner;
        public FieldPlanner CurrentExecutingPlanner { get { return FieldPlanner.CurrentGraphExecutingPlanner; } }

        [HideInInspector] public ScriptableObject ParentNodesContainer;

        public string DebuggingInfo { get; protected set; }
        public Action DebuggingGizmoEvent { get; protected set; }

        //public virtual string TitleName() { return GetType().Name; }
        public virtual string Tooltip() { string tooltipHelp = "(" + GetType().Name; return tooltipHelp + ")"; }

        public override Vector2 NodeSize { get { return new Vector2(232, 90); } }
        /// <summary> PlannerRuleBase by default is true </summary>
        public override bool DrawInputConnector { get { return true; } }

        public bool GetPlannerPort_IsContainingMultiple(PGGPlannerPort port)
        {
            if (port.ContainsMultiple) return true;
            return false;
        }

        /// <summary>
        /// You can use port.ContainsMultiplePlanners to identify multiple planners port transport
        /// </summary>
        private static List<FieldPlanner> _multiplePlannersContainer = new List<FieldPlanner>();
        public List<FieldPlanner> GetPlannersFromPort(PGGPlannerPort port, bool nullIfNoMultiple = false, bool callRead = true, bool newListInstance = true)
        {
            List<FieldPlanner> list;

            #region List Definition

            if (newListInstance) list = new List<FieldPlanner>();
            else
            {
                _multiplePlannersContainer.Clear();
                list = _multiplePlannersContainer;
            }

            #endregion



            for (int c = 0; c < port.Connections.Count; c++)
            {
                var conn = port.Connections[c];
                PGGPlannerPort comm = null;

                #region Define planner port connection

                if (conn != null)
                {
                    if (conn.PortReference != null)
                    {
                        if (conn.PortReference == port) UnityEngine.Debug.Log("(Same port - it's wrong)");
                        else
                        {
                            if (conn.PortReference is PGGPlannerPort) comm = conn.PortReference as PGGPlannerPort;
                        }
                    }
                }

                #endregion


                if (comm != null) // Planner port
                {
                    if (comm.ContainsMultiple)
                    {
                        comm.TransportContainedPlannersInto(list);
                    }
                    else
                    {
                        FieldPlanner pl = GetPlannerFromPort(port, callRead);
                        if (pl) if (pl.Available) list.Add(pl);
                    }
                }
                else // No Planner Port
                {
                    FieldPlanner plannerOutOfOtherPort = null;

                    #region Define cell port connection

                    if (conn != null)
                    {
                        if (conn.PortReference is PGGCellPort)
                        {
                            plannerOutOfOtherPort = PGGPlannerPort.GetPlannerFromCellPort(conn.PortReference);
                        }
                    }

                    #endregion

                    // No planner port and no cell port - try other types
                    if (plannerOutOfOtherPort == null) plannerOutOfOtherPort = GetPlannerFromPort(port, callRead);

                    if (plannerOutOfOtherPort) if (plannerOutOfOtherPort.Available) list.Add(plannerOutOfOtherPort);
                }
            }

            if (port.IsOutput)
            {
                if (port.ContainsMultiple)
                {
                    port.TransportContainedPlannersInto(list);
                }
            }

            if (port.Connections.Count == 0)
            {
                FieldPlanner pl = GetPlannerFromPort(port, callRead);
                if (pl) if (pl.Available) list.Add(pl);
            }

            port.AssignPlannersList(list);

            if (list.Count > 0) return list;

            if (nullIfNoMultiple)
                return null;
            else
                return list;
        }

        public static FieldPlanner GetPlannerFromPortS(PGGPlannerPort port, bool callRead = true)
        {
            if (callRead) port.GetPortValueCall();
            int plannerId = port.GetPlannerIndex();
            int duplicateId = port.GetPlannerDuplicateIndex();

            FieldPlanner portPlanner = port.GetPlannerFromPort();
            if (portPlanner != null) return portPlanner;

            return GetFieldPlannerByID(plannerId, duplicateId);
        }

        /// <summary> Getting connected planner, if there is no planner, it will return self planner </summary>
        public FieldPlanner GetPlannerFromPortAlways(PGGPlannerPort port, bool callRead = true)
        {
            FieldPlanner planner = GetPlannerFromPort(port, callRead);
            if (planner == null) planner = CurrentExecutingPlanner;
            return planner;
        }

        public FieldPlanner GetPlannerFromPort(PGGPlannerPort port, bool callRead = true)
        {
            if (callRead) port.TriggerReadPort(true);

            if (port.UsingNumberedID == false)
            {
                if (port.ContainsJustChecker) return null;
            }

            FieldPlanner portPlanner = port.GetPlannerFromPort();

            if (portPlanner != null)
            {
                return portPlanner;
            }
            else
            {
                if (port.MinusOneReturnsSelf == false && port.UniquePlannerID < 0) return null;
            }

            int plannerId = port.GetPlannerIndex();
            int duplicateId = port.GetPlannerDuplicateIndex();
            int subId = port.GetPlannerSubFieldIndex();

            portPlanner = GetPlannerByID(plannerId, duplicateId, subId);
            return portPlanner;
        }



        public CheckerField3D GetCheckerFromPort(PGGPlannerPort port, bool callRead = true)
        {
            if (callRead) port.TriggerReadPort(true);

            CheckerField3D portPlanner = port.GetInputCheckerSafe;
            if (portPlanner != null)
            {
                return portPlanner;
            }

            FieldPlanner planner = GetPlannerFromPort(port, callRead);
            if (planner == null) return null;
            if (planner.Available == false) return null;
            return planner.LatestChecker;
        }

        public FieldPlanner GetPlannerByID(int plannerId, int duplicateId = -1, int subId = -1)
        {
            FieldPlanner planner = GetFieldPlannerByID(plannerId, duplicateId, subId);
            if (planner == null) return ParentPlanner;
            return planner;
        }

        public static bool _debug = false;
        public static FieldPlanner GetFieldPlannerByID(int plannerId, int duplicateId = -1, int subFieldID = -1, bool selfOnUndefined = true)
        {

            FieldPlanner planner = FieldPlanner.CurrentGraphExecutingPlanner;
            if (planner == null) { planner = null; }

            if (plannerId < 0 && selfOnUndefined == false)
            {
                return null;
            }

            BuildPlannerPreset build = null;
            if (planner != null) build = planner.ParentBuildPlanner;

            if (build == null)
            {
                return null;
            }

            if (plannerId >= 0 && plannerId < build.BasePlanners.Count)
            {
                planner = build.BasePlanners[plannerId];

                bool dup = false;

                var duplList = planner.GetDuplicatesPlannersList();

                if (planner.IsDuplicate == false) if (duplicateId >= 0) if (duplList != null) if (duplicateId < duplList.Count)
                            {
                                planner = duplList[duplicateId];
                                dup = true;

                                if (planner.GetSubFieldsCount > 0)
                                    if (subFieldID != -1)
                                    {
                                        if (subFieldID >= planner.GetSubFieldsCount) return null;
                                        planner = planner.GetSubField(subFieldID);
                                    }
                            }

                if (!dup)
                {
                    if (subFieldID > -1)
                        if (planner.GetSubFieldsCount > 0)
                            planner = planner.GetSubField(subFieldID);
                }
            }

            if (planner.Discarded)
            {
                FieldPlanner getPl = planner;

                if (duplicateId == -1) // if discarded then get first not discarded duplicate planner
                {

                    var duplList = planner.GetDuplicatesPlannersList();

                    if (duplList != null)
                        if (planner.IsDuplicate == false)
                            for (int i = 0; i < duplList.Count; i++)
                            {
                                var plan = duplList[i];
                                if (plan == null) continue;
                                if (plan.Available == false) continue;
                                getPl = plan;

                                if (subFieldID != -1)
                                    if (getPl.GetSubFieldsCount > 0)
                                    {
                                        if (subFieldID >= getPl.GetSubFieldsCount) return null;
                                        getPl = getPl.GetSubField(subFieldID);
                                    }

                                break;
                            }
                }

                return getPl;
            }

            return planner;
        }

        /// <summary> [Base is empty] </summary>
        public virtual void PreGeneratePrepare() { }


        /// <summary> [Base is not empty] Preparing initial debug message </summary>
        public virtual void Prepare(PlanGenerationPrint print)
        {
#if UNITY_EDITOR
            DebuggingInfo = "Debug Info not Assigned";
#endif
        }

        /// <summary> [Base is empty] </summary>
        public virtual void Execute(PlanGenerationPrint print, PlannerResult newResult)
        {
            // Node Procedures Code
        }


        protected void CallOtherExecution(FGraph_NodeBase otherNode, PlanGenerationPrint print)
        {
            if (otherNode == null) return;

            if (ParentPlanner == null)
            {
                if (print == null)
                {
                    if (otherNode is PlannerRuleBase)
                        MG_ModGraph.CallExecution(otherNode as PlannerRuleBase);
                }

                return;
            }

            if (otherNode is PlannerRuleBase)
                ParentPlanner.CallExecution(otherNode as PlannerRuleBase, print);
        }

        protected void CallOtherExecutionWithConnector(int altId, PlanGenerationPrint print)
        {
            for (int c = 0; c < OutputConnections.Count; c++)
            {
                if (OutputConnections[c].ConnectionFrom_AlternativeID == altId)
                {
                    CallOtherExecution(OutputConnections[c].GetOther(this), print);
                }
            }
        }


        #region Editor related


#if UNITY_EDITOR

        public virtual void OnGUIModify()
        {

        }

        [HideInInspector]
        public bool _editor_drawRule = true;
        protected UnityEditor.SerializedObject inspectorViewSO = null;

        protected virtual void DrawGUIHeader(int i)
        {
            if (inspectorViewSO == null) inspectorViewSO = new UnityEditor.SerializedObject(this);
            EditorGUILayout.BeginHorizontal(FGUI_Resources.BGInBoxLightStyle, GUILayout.Height(20)); // 1

            Enabled = EditorGUILayout.Toggle(Enabled, GUILayout.Width(24));


            string foldout = FGUI_Resources.GetFoldSimbol(_editor_drawRule);
            string tip = Tooltip();


            if (GUILayout.Button(new GUIContent(foldout + "  " + GetDisplayName() + "  " + foldout, tip), FGUI_Resources.HeaderStyle))
            {
                bool rmb = false;
                if (rmb == false) _editor_drawRule = !_editor_drawRule;
            }

            int hh = 18;

            if (i > 0) if (GUILayout.Button(new GUIContent(FGUI_Resources.Tex_ArrowUp), FGUI_Resources.ButtonStyle, GUILayout.Width(18), GUILayout.Height(hh))) { FGenerators.SwapElements(ParentPlanner.FProcedures, i, i - 1); return; }
            if (i < ParentPlanner.FProcedures.Count - 1) if (GUILayout.Button(new GUIContent(FGUI_Resources.Tex_ArrowDown), FGUI_Resources.ButtonStyle, GUILayout.Width(18), GUILayout.Height(hh))) { FGenerators.SwapElements(ParentPlanner.FProcedures, i, i + 1); return; }

            if (GUILayout.Button("X", FGUI_Resources.ButtonStyle, GUILayout.Width(24), GUILayout.Height(hh)))
            {
                ParentPlanner.RemoveRuleFromPlanner(this);
                return;
            }

            EditorGUILayout.EndHorizontal(); // 1
        }

        protected virtual void DrawGUIFooter()
        {
            EditorGUILayout.EndVertical();

            if (inspectorViewSO.ApplyModifiedProperties())
            {
                OnStartReadingNode();
            }
        }


        //public void DrawGUIStack(int i)
        //{
        //    DrawGUIHeader(i);

        //    Color preColor = GUI.color;

        //    if (inspectorViewSO != null)
        //        if (inspectorViewSO.targetObject != null)
        //            if (_editor_drawRule)
        //            {
        //                EditorGUILayout.BeginVertical(FGUI_Resources.BGInBoxStyle);
        //                if (Enabled == false) GUI.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
        //                inspectorViewSO.Update();
        //                DrawGUIBody();
        //                DrawGUIFooter();
        //            }

        //    GUI.color = preColor;
        //}

        /// <summary>
        /// Returns true if something changed in GUI - using EditorGUI.BeginChangeCheck();
        /// </summary>
        //protected virtual void DrawGUIBody(/*int i*/)
        //{
        //    UnityEditor.SerializedProperty sp = inspectorViewSO.GetIterator();
        //    sp.Next(true);
        //    sp.NextVisible(false);
        //    bool can = sp.NextVisible(false);

        //    if (can)
        //    {
        //        do
        //        {
        //            bool cont = false;
        //            if (cont) continue;

        //            UnityEditor.EditorGUILayout.PropertyField(sp);
        //        }
        //        while (sp.NextVisible(false) == true);
        //    }

        //    //    EditorGUILayout.EndVertical();

        //    //    so.ApplyModifiedProperties();
        //    //}


        //}

#endif

        #endregion


        #region Mod Graph Related

        /// <summary>  Current executing mod graph (for field modification graph) </summary>
        public SR_ModGraph MG_ModGraph { get { return SR_ModGraph.Graph_ModGraph; } }
        /// <summary> Current executing field mod (for field modification graph) </summary>
        public FieldSpawner MG_Spawner { get { return SR_ModGraph.Graph_Spawner; } }
        /// <summary> Current executing field modificator's spawner </summary>
        public FieldModification MG_Mod { get { return SR_ModGraph.Graph_Mod; } }
        /// <summary> Current executing mod spawner's spawn (for field modification graph) </summary>
        public SpawnData MG_Spawn { get { return SR_ModGraph.Graph_SpawnData; } }
        /// <summary> Current executing field setup preset (for field modification graph) </summary>
        public FieldSetup MG_Preset { get { return SR_ModGraph.Graph_Preset; } }
        /// <summary> Current executing field grid cell (for field modification graph) </summary>
        public FieldCell MG_Cell { get { return SR_ModGraph.Graph_Cell; } }
        /// <summary> Current executing field gridd (for field modification graph) </summary>
        public FGenGraph<FieldCell, FGenPoint> MG_Grid { get { return SR_ModGraph.Graph_Grid; } }
        public List<SpawnInstruction> MG_GridInstructions { get { return SR_ModGraph.Graph_Instructions; } }
        ///// <summary> Current executing field mod (for field modification graph) </summary>
        //public Vector3? Graph_RestrictDir { get { return SR_ModGraph.Graph_Mod; } }


        public ModificatorsPack MGGetParentPack()
        {
            SR_ModGraph owner = ParentNodesContainer as SR_ModGraph;
            if (owner == null) return null;
            return owner.TryGetParentModPack();
        }

        public UnityEngine.Object MGGetFieldSetup()
        {
            SR_ModGraph owner = ParentNodesContainer as SR_ModGraph;
            if (owner == null) return null;

            var fs = owner.TryGetParentFieldSetup();
            if (fs == null) return null;

            //if (fs)
            //{
            //    if (fs.InstantiatedOutOf) return fs.InstantiatedOutOf;
            //}

            return fs;
        }

        protected List<FieldVariable> MGGetVariables(UnityEngine.Object tgt)
        {
            if (tgt == null) return null;

            FieldSetup fs = tgt as FieldSetup;
            if (fs)
            {
                return fs.Variables;
            }

            ModificatorsPack mp = tgt as ModificatorsPack;
            if (mp)
            {
                return mp.Variables;
            }

            return null;
        }

        protected FieldVariable MGGetVariable(UnityEngine.Object tgt, int index)
        {
            var variables = MGGetVariables(tgt);
            if (variables == null) return null;
            if (variables.ContainsIndex(index)) return variables[index];
            return null;
        }


        #endregion
    }
}
