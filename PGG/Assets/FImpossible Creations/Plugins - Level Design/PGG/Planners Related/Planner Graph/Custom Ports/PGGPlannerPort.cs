using FIMSpace.Generating;
using FIMSpace.Generating.Checker;
using FIMSpace.Generating.Planning;
using FIMSpace.Generating.Planning.GeneratingLogics;
using FIMSpace.Generating.Planning.PlannerNodes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace.Graph
{
    [System.Serializable]
    public class PGGPlannerPort : NodePortBase
    {
        /// <summary> Graph runtime References </summary>
        private FieldPlannerReference Containing;



        #region "Containing" Reference Utilities / Shortcuts

        /// <summary> 
        /// If it's input port with no connection, it will return field of this ID or self if value is lower than zero 
        /// This value is not just Graph Runtime but also saved in node!
        /// </summary>
        [HideInInspector] public int UniquePlannerID = -1;
        [NonSerialized] public int DuplicatePlannerID = -1;
        [NonSerialized] public int SubFieldID = -1;


        /// <summary> Change if you want to return null planner on undefined value instead of returning current executing planner </summary>
        [HideInInspector] public bool MinusOneReturnsSelf = true;

        /// <summary> Set false if you DONT want to return self planner / choosed ID by default, but return null </summary>
        [HideInInspector] public bool DefaultValueIsNumberedID = true;

        [HideInInspector] public bool OnlyReferenceContainer = false;//{ get { return !DefaultValueIsNumberedID; } set { DefaultValueIsNumberedID = !value; } }
        [HideInInspector] public bool UsingNumberedID { get {/* if (!DefaultValueIsNumberedID) return false;*/ return Containing.UsingNumberedID; } }


        /// Updating 'Containing' reference with current  'UniquePlannerID', 'DuplicatePlannerID', 'SubFieldID' values 
        void RefreshNumberedID()
        {
            SetNumberedID(UniquePlannerID, DuplicatePlannerID, SubFieldID);
        }

        /// <summary> Setting 'UniquePlannerID', 'DuplicatePlannerID', 'SubFieldID' and updating 'Containing' reference </summary>
        void SetNumberedID(int plannerId, int duplicateId = -1, int subFieldId = -1)
        {
            UniquePlannerID = plannerId;
            DuplicatePlannerID = duplicateId;
            SubFieldID = subFieldId;

            FieldPlannerReference cont = Containing;
            cont.SetNumberedID(plannerId, duplicateId, subFieldId);
            Containing = cont;
        }

        #endregion



        #region Editor Related Variables and methods

        public bool Editor_DisplayVariableName = true;

        /// <summary> Text display in port if it's input with no connection </summary>
        [NonSerialized] public string Editor_DefaultValueInfo = "(Self)";

        public override Color GetColor()
        {
            return new Color(0.9f, 0.7f, .3f, 1f);
        }

        #endregion



        #region Handling Multiple Fields


        private List<FieldPlannerReference> MultipleContaining = null;
        public bool ContainsMultiple
        {
            get
            {
                if (IsInput && IsConnected) return GetContainedCount() > 1;
                if (MultipleContaining == null) return false; return MultipleContaining.Count > 1;
            }
        }

        /// <summary> Returns true if containing checker or multiple checkers and none planner reference </summary>
        public bool ContainsJustCheckers
        {
            get
            {
                if (ContainsMultiple)
                {
                    bool was = false;

                    for (int i = 0; i < MultipleContaining.Count; i++)
                    {
                        if (MultipleContaining[i].FreeChecker == null) return false;
                        if (MultipleContaining[i].OwnerPlanner != null) return false;
                        was = true;
                    }

                    return was;
                }

                return false;
            }
        }


        public int GetContainedCount()
        {
            int count = 0;

            if (IsInput && IsConnected)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    if (Connections[i] == null) continue;

                    PGGPlannerPort oPort = Connections[i].PortReference as PGGPlannerPort;

                    if (oPort == null)
                    {
                        PGGCellPort cPort = Connections[i].PortReference as PGGCellPort;
                        if (cPort != null)
                        {
                            if (GetCheckerFromCellPort(cPort) != null) count += 1;
                            continue;
                        }
                    }
                    else
                        count += oPort.GetContainedCount();
                }
            }
            else
            {
                if (MultipleContaining != null)
                {
                    for (int i = 0; i < MultipleContaining.Count; i++)
                    {
                        /*if (MultipleContaining[i] != Containing) */
                        count += 1;
                    }
                }
                else
                {
                    if (!Containing.IsNull) count += 1;
                }
            }

            return count;
        }


        internal void TransportContainedPlannersInto(List<FieldPlanner> list)
        {
            if (list == null) return;

            if (MultipleContaining == null || MultipleContaining.Count == 0)
            {
                FieldPlanner srcPlanner = Containing.GetFieldPlannerReference();
                if (srcPlanner)
                {
                    if (!list.Contains(srcPlanner)) list.Add(srcPlanner);
                }

                return;
            }

            for (int i = 0; i < MultipleContaining.Count; i++)
            {
                var planner = MultipleContaining[i].GetFieldPlannerReference(false);

                if (planner == null) continue;
                if (list.Contains(planner)) continue;

                list.Add(planner);
            }

        }



        internal void AssignPlannersList(List<FieldPlanner> list)
        {
            if (list == null) return;
            if (MultipleContaining == null) MultipleContaining = new List<FieldPlannerReference>();
            Clear();

            for (int i = 0; i < list.Count; i++)
            {
                FieldPlannerReference nRef = new FieldPlannerReference(list[i], null);
                MultipleContaining.Add(nRef);
            }
        }

        internal void AssignCheckersList(List<CheckerField3D> list)
        {
            if (list == null) return;
            if (MultipleContaining == null) MultipleContaining = new List<FieldPlannerReference>();
            Clear();

            for (int i = 0; i < list.Count; i++)
            {
                FieldPlannerReference nRef = new FieldPlannerReference(null, list[i]);
                MultipleContaining.Add(nRef);
            }
        }



        #endregion



        #region Handling Contained Info Helper Methods

        public override System.Type GetPortValueType
        {
            get
            {
                if (UsingNumberedID) return typeof(int);
                if (Containing.IsFreeChecker) return typeof(CheckerField3D);
                if (Containing.IsSubField) return typeof(FieldPlanner);
                return typeof(object);
            }
        }

        public bool ContainsForcedNull
        {
            get { if (ContainsMultiple) return false; return Containing.IsNull; }
            set { var cnt = Containing; cnt.ForcedNull = value; Containing = cnt; }
        }


        public bool ContainsSubField
        {
            get
            {
                if (ContainsMultiple)
                {
                    if (Containing.IsSubField) return true;
                    for (int i = 0; i < MultipleContaining.Count; i++) if (MultipleContaining[i].IsSubField) return true;
                }

                return Containing.IsSubField;
            }
        }

        public int ContainedSubFieldID { get { RefreshNumberedID(); return Containing.GetSubFieldID(); } }

        public string GetNumberedIDArrayString()
        {
            string subStr = "";
            if (SubFieldID != -1) subStr = "SUB(" + SubFieldID + "):";

            string mainStr;

            if (UniquePlannerID <= -1)
            {
                if (ContainsJustChecker) mainStr = "(Shape)";
                else
                mainStr = Editor_DefaultValueInfo; 
            }
            else mainStr = "[" + UniquePlannerID + "]";
            

            if (DuplicatePlannerID <= -1) return subStr + mainStr;

            return subStr + mainStr + "[" + DuplicatePlannerID + "]";
        }

        public bool ContainsAnyReference { get { return Containing.IsAnyReferenceContained; } }

        public bool ContainsJustChecker
        {
            get
            {
                if (ContainsMultiple)
                {
                    if (Containing.IsFreeChecker) return true;
                    for (int i = 0; i < MultipleContaining.Count; i++) if (MultipleContaining[i].IsFreeChecker) return true;
                }

                return Containing.IsFreeChecker;
            }
        }


        #endregion



        #region Basic Connection Handling


        public override bool AllowConnectionWithValueType(IFGraphPort other)
        {
            if (FGenerators.CheckIfIsNull(other)) return false;
            if ((other is PGGPlannerPort)) return true;
            if (FGenerators.CheckIfIsNull(other.GetPortValue)) return false; // If null then allow connect only with PGGPlannerPort
            if (other.GetPortValue.GetType() == typeof(int)) return true;
            if (other.GetPortValue.GetType() == typeof(float)) return true;
            if (other.GetPortValue.GetType() == typeof(CheckerField3D)) return true;
            if (other.GetPortValue.GetType() == typeof(FieldPlanner)) return true;
            if (other.GetPortValue.GetType() == typeof(Vector2)) return true;
            if (other.GetPortValue.GetType() == typeof(Vector2Int)) return true;
            if (other.GetPortValue.GetType() == typeof(Vector3)) return true;
            if (other.GetPortValue.GetType() == typeof(Vector3Int)) return true;
            return base.AllowConnectionWithValueType(other);
        }

        public override bool CanConnectWith(IFGraphPort toPort)
        {
            if (toPort is PGGCellPort) return true;
            //if (toPort is PGGPlannerPort) return true;
            bool can = base.CanConnectWith(toPort);
            return can;
        }

        public override object GetPortValueCall(bool onReadPortCall = true)
        {
            if (IsInput) if (IsNotConnected) { SetNumberedID(UniquePlannerID); }

            var val = base.GetPortValueCall(onReadPortCall);
            if (val == null) return val;
            if (val is FieldPlanner) return val;
            if (val is CheckerField3D) return val;
            ReadValue(val);
            return val;
        }

        void ReadValue(object val)
        {
            if (val.GetType() == typeof(int))
            {
                SetNumberedID((int)val);
            }
            else
            if (val.GetType() == typeof(float)) SetNumberedID(Mathf.RoundToInt((float)val));
            else
            if (val.GetType() == typeof(Vector2))
            {
                Vector2 v2 = (Vector2)val;
                SetNumberedID(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
            }
            else if (val.GetType() == typeof(Vector2Int))
            {
                Vector2Int v2 = (Vector2Int)val;
                SetNumberedID(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
            }
            else if (val.GetType() == typeof(Vector3))
            {
                Vector3 v3 = (Vector3)val;
                SetNumberedID(Mathf.RoundToInt(v3.x), Mathf.RoundToInt(v3.y), Mathf.RoundToInt(v3.z));
            }
            else if (val.GetType() == typeof(Vector3Int))
            {
                Vector3Int v3 = (Vector3Int)val;
                SetNumberedID(Mathf.RoundToInt(v3.x), Mathf.RoundToInt(v3.y), Mathf.RoundToInt(v3.z));
            }
            else if (val.GetType() == typeof(PGGCellPort.Data))
            {
                PGGCellPort.Data dt = (PGGCellPort.Data)val;

                if (dt.ParentResult != null)
                    if (dt.ParentResult.ParentFieldPlanner)
                        SetIDsOfPlanner(dt.ParentResult.ParentFieldPlanner);
            }
            else if (val.GetType() == typeof(FieldPlanner))
            {
                FieldPlanner dt = (FieldPlanner)val;
                if (dt) SetIDsOfPlanner(dt);
            }
            else if (val.GetType() == typeof(CheckerField3D))
            {
                CheckerField3D dt = (CheckerField3D)val;
                if (dt != null)
                {
                    if (dt.SubFieldPlannerReference)
                        SetIDsOfPlanner(dt.SubFieldPlannerReference);
                    else
                        ProvideShape(dt);
                }
            }
        }


        /// <summary> Using float,int,Vector2/int,Vector3/int to set numbered ID values </summary>
        public void SetIDsFromNumberVar(object numberVar)
        {
            if (numberVar == null) return;
            ReadValue(numberVar);
        }

        internal void SetIDsOfPlanner(FieldPlanner planner)
        {
            FieldPlannerReference cont = Containing;

            if (planner == null)
            {
                cont.SetNumberedID(-1);
                cont.OwnerPlanner = null;
                cont.FreeChecker = null;
                cont.ForcedNull = true;
                Containing = cont;
                return;
            }

            cont.ForcedNull = false;
            UniquePlannerID = planner.IndexOnPreset;
            DuplicatePlannerID = planner.IndexOfDuplicate;
            SubFieldID = planner.IsSubField ? (planner.GetSubFieldID()) : -1;

            Containing = cont;
        }


        #endregion



        #region Value Helper Methods


        internal void Clear()
        {
            SetIDsOfPlanner(null);
            if (MultipleContaining != null) MultipleContaining.Clear();
            ContainsForcedNull = false;
        }

        /// <summary>
        /// Planner Index (-1 if use self) and Duplicate Index (in most cases just zero)
        /// </summary>
        public override object DefaultValue
        {
            get
            {
                if (Containing.IsFreeChecker) return Containing.FreeChecker;
                if (Containing.IsRootChecker) return Containing.OwnerPlanner;
                if (Containing.IsSubField) return Containing.OwnerPlanner;
                if (UsingNumberedID == false) if ( !DefaultValueIsNumberedID) return null;

                return new Vector3Int(UniquePlannerID, DuplicatePlannerID, SubFieldID);
            }
        }

        public object AcquireObjectReferenceFromInput()
        {
            for (int c = 0; c < Connections.Count; c++)
            {
                var conn = Connections[c];
                var value = conn.PortReference.GetPortValue;
                if (value != null) return value;
            }

            return null;
        }


        /// <summary>
        ///  -1 is use self else use index in field planner
        /// </summary>
        public int GetPlannerIndex()
        {
            if (UniquePlannerID < -1) return -1;
            //if (PortState() != EPortPinState.Connected) return -1;
            return UniquePlannerID;
        }

        /// <summary>
        /// Index of duplicated planner field
        /// </summary>
        public int GetPlannerDuplicateIndex()
        {
            if (DuplicatePlannerID < 0) return -1;
            return DuplicatePlannerID;
        }

        public int GetPlannerSubFieldIndex()
        {
            if (SubFieldID < 0) return -1;
            return SubFieldID;
        }



        #endregion



        #region Target Operations



        // Complex Read Referece Methods   START   ------------------------------------------

        public CheckerField3D GetInputCheckerSafe
        {
            get
            {
                FieldPlanner planner;

                if (PortState() == EPortPinState.Empty) // Not Connected
                {
                    planner = GetPlannerFromPort(false);
                    if (planner) return planner.LatestChecker;
                }
                else // Connected
                {
                    // Getting connected checker to the input port

                    if (BaseConnection != null && BaseConnection.PortReference != null)
                    {
                        if (BaseConnection.PortReference is PGGPlannerPort)
                        {
                            PGGPlannerPort plannerPort = BaseConnection.PortReference as PGGPlannerPort;
                            if (plannerPort.Containing.IsFreeChecker) return plannerPort.shape;
                        }
                        else if (BaseConnection.PortReference is PGGCellPort)
                        {
                            PGGCellPort cellPort = BaseConnection.PortReference as PGGCellPort;
                            var cellChecker = GetCheckerFromCellPort(cellPort);
                            if (cellChecker != null) return cellChecker;
                        }
                    }

                    CheckerField3D containedChecker = Containing.GetCheckerReference();
                    if (containedChecker != null) return containedChecker;
                }

                object val = GetPortValueSafe;

                if (val is PGGCellPort.Data)
                {
                    PGGCellPort.Data data = (PGGCellPort.Data)val;
                    if (FGenerators.CheckIfExist_NOTNULL(data.CellRef))
                        if (FGenerators.CheckIfExist_NOTNULL(data.ParentChecker))
                            return data.ParentChecker;
                }

                planner = GetPlannerFromPort(false);
                if (planner) return planner.LatestChecker;

                return null;
            }
        }


        /// <summary> Single list instance for returning multiple checkers </summary>
        static List<CheckerField3D> _checkersReturnList = new List<CheckerField3D>();

        /// <summary> Returning static list instance with contained checkers in all port input connections (returns single element list if there is not connection) </summary>
        public List<CheckerField3D> GetAllInputCheckers(bool newListInstance = false, bool getFieldsCheckers = true)
        {
            _checkersReturnList.Clear();
            List<CheckerField3D> list = _checkersReturnList;
            if (newListInstance) list = new List<CheckerField3D>();

            if (PortState() == EPortPinState.Empty) // Not Connected - self checker return if possible
            {
                //checker = GetInputCheckerSafe;
                //if (checker != null) list.Add(checker);
                CheckerField3D checker = Containing.GetCheckerReference();
                if (checker != null) list.Add(checker);
                else if (getFieldsCheckers)
                {
                    var pl = GetPlannerFromPort(false);
                    if (pl != null) list.Add(pl.LatestChecker);
                }
            }
            else // Connected
            {
                // Getting connected checkers to the input port
                if (BaseConnection != null && BaseConnection.PortReference != null)
                {
                    for (int c = 0; c < Connections.Count; c++)
                    {
                        var connPort = BaseConnection.PortReference;
                        if (connPort == null) continue;

                        if (connPort is PGGPlannerPort)
                        {
                            PGGPlannerPort plannerPort = BaseConnection.PortReference as PGGPlannerPort;
                            if (plannerPort.ContainsMultiple)
                            {
                                for (int m = 0; m < plannerPort.MultipleContaining.Count; m++)
                                {
                                    CheckerField3D checker = plannerPort.MultipleContaining[m].GetCheckerReference();
                                    if (checker != null) list.Add(checker);
                                }
                            }
                            else
                            {
                                CheckerField3D checker = plannerPort.Containing.GetCheckerReference();
                                if (checker != null) list.Add(checker);
                            }
                        }
                        else if (connPort is PGGCellPort)
                        {
                            PGGCellPort cellPort = BaseConnection.PortReference as PGGCellPort;
                            var cellChecker = GetCheckerFromCellPort(cellPort);
                            if (cellChecker != null) list.Add(cellChecker);
                        }
                    }
                }
            }

            return list;
        }



        public FieldPlanner GetPlannerFromPort(bool callRead = true)
        {
            if (callRead) GetPortValueCall();


            if (IsInput && IsNotConnected && UniquePlannerID < 0 && OnlyReferenceContainer == false)
            {
                DuplicatePlannerID = -1;
                SubFieldID = -1;
            }


            int plannerId = GetPlannerIndex();
            int duplicateId = GetPlannerDuplicateIndex();
            int subFieldID = GetPlannerSubFieldIndex();

            if (Containing.UsingNumberedID)
            {
                if (Connections.Count == 0)
                {
                    if (UniquePlannerID > -1)
                    {
                        return PlannerRuleBase.GetFieldPlannerByID(UniquePlannerID, DuplicatePlannerID, SubFieldID, MinusOneReturnsSelf);
                    }
                }
                else
                {
                    if (BaseConnection.PortReference != null)
                    {
                        PGGCellPort cellPrt = BaseConnection.PortReference as PGGCellPort;

                        if (cellPrt != null) // Cell Port
                        {
                            var cellField = cellPrt.GetInputPlannerIfPossible;
                            if (cellField) return cellField;
                        }
                        else // Planner port
                        {
                            PGGPlannerPort fPort = BaseConnection.PortReference as PGGPlannerPort;
                            if (fPort != null)
                            {
                                return PlannerRuleBase.GetFieldPlannerByID(fPort.UniquePlannerID, fPort.DuplicatePlannerID, fPort.SubFieldID, MinusOneReturnsSelf);
                            }
                        }
                    }
                }

            }
            else // Containing references
            {
                return Containing.GetFieldPlannerReference();
            }

            return PlannerRuleBase.GetFieldPlannerByID(plannerId, duplicateId, subFieldID, MinusOneReturnsSelf);
        }


        // Complex Read Referece Methods   END   ------------------------------------------


        internal void ProvideShape(CheckerField3D newChecker, Vector3? extraOffset = null)
        {
            var contained = Containing;
            contained.FreeChecker = newChecker;
            contained.OwnerPlanner = null;
            contained.ForcedNull = false;
            Containing = contained;
        }

        public CheckerField3D shape
        {
            get
            {
                if (!IsOutput)
                    if (PortState() == EPortPinState.Connected)
                    {
                        var port = FirstConnectedPortOfType(typeof(PGGPlannerPort));
                        if (port != null)
                        {
                            var plPrt = port as PGGPlannerPort;
                            if (plPrt.Containing.IsFreeChecker) return plPrt.shape;
                        }
                        else
                        {
                            var cellPrt = FirstConnectedPortOfType(typeof(PGGCellPort));
                            if (cellPrt != null) return GetCheckerFromCellPort(cellPrt);
                        }
                    }

                return Containing.GetCheckerReference();
            }
        }





        // CELL RELATED   START   ---------------------------------------------------


        /// <summary> Checker from CELL Port </summary>
        public static CheckerField3D GetCheckerFromCellPort(IFGraphPort cellPrt)
        {
            PGGCellPort plPrt = cellPrt as PGGCellPort;
            PGGCellPort.Data cData = plPrt.CellData;

            if (FGenerators.NotNull(cData.CellRef))
            {
                if (cData.ParentChecker != null)
                {
                    CheckerField3D ch = new CheckerField3D();
                    ch.CopyParamsFrom(cData.ParentChecker);
                    ch.AddLocal(cData.CellRef.Pos);
                    return ch;
                }
            }

            return null;
        }


        /// <summary> Field from CELL Port </summary>
        public static FieldPlanner GetPlannerFromCellPort(IFGraphPort cellPrt)
        {
            PGGCellPort cPort = cellPrt as PGGCellPort;

            if (cPort != null)
            {
                return cPort.GetInputPlannerIfPossible;
            }

            return null;
        }


        // CELL RELATED   END   ---------------------------------------------------



        #endregion


    }
}