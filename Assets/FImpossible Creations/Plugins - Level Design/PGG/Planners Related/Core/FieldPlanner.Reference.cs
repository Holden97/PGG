using FIMSpace.Generating.Checker;
using UnityEngine;

namespace FIMSpace.Generating.Planning.GeneratingLogics
{
    /// <summary> Helper structure to support async operations and simplify some scripts. 
    /// Its used by sub-fields feature and by the planner nodes. </summary>
    public struct FieldPlannerReference
    {
        public FieldPlanner OwnerPlanner;
        public CheckerField3D FreeChecker;
        public bool ForcedNull;

        /// <summary> 
        /// X - FieldPlanner index on Build list
        /// Y - Duplicate Index (Instance-1). It's -1 if its no duplicate
        /// Z - Sub-Field Index. It's -1 if its no sub-field
        /// </summary>
        public Vector3Int NumberedID;
        public void SetNumberedID(int plannerId, int duplicateId = -1, int subFieldId = -1) { NumberedID = new Vector3Int(plannerId, duplicateId, subFieldId); }

        public int UniquePlannerID { get { return NumberedID.x; } set { Vector3Int n = NumberedID; n.x = value; NumberedID = n; } }
        public int DuplicatePlannerID { get { return NumberedID.y; } set { Vector3Int n = NumberedID; n.y = value; NumberedID = n; } }
        public int SubFieldID { get { return NumberedID.z; } set { Vector3Int n = NumberedID; n.z = value; NumberedID = n; } }


        public FieldPlannerReference(FieldPlanner parent, CheckerField3D extraChecker)
        {
            OwnerPlanner = parent;
            FreeChecker = extraChecker;
            ForcedNull = false;
            NumberedID = new Vector3Int(-1, -1, -1);
        }


        /// <summary> When using just numbered ID : it's not null and not containing any checker / planner reference </summary>
        public bool IsDefault { get { return ForcedNull == false && OwnerPlanner == null && FreeChecker == null; } }
        public bool UsingNumberedID { get { return IsDefault; } }
        public bool IsNull { get { return ForcedNull; } }
        public bool IsAnyReferenceContained { get { return OwnerPlanner != null || FreeChecker != null; } }
        public bool IsFreeChecker { get { return OwnerPlanner == null && FreeChecker != null; } }
        public bool IsRootChecker { get { return OwnerPlanner != null && FreeChecker == null && !OwnerPlanner.IsDuplicate && !OwnerPlanner.IsSubField; } }
        public bool IsDuplicate { get { return OwnerPlanner != null && FreeChecker == null && OwnerPlanner.IsDuplicate; } }
        public bool IsSubField { get { return OwnerPlanner != null && FreeChecker != null && OwnerPlanner.IsSubField; } }



        public CheckerField3D GetCheckerReference()
        {
            if (IsNull) return null;
            if (IsFreeChecker) return FreeChecker;
            if ( OwnerPlanner == null ) return null;
            return OwnerPlanner.LatestChecker;
        }

        /// <summary> Trying to use OwnerPlanner, if null then trying to get planner from the checker reference, otherwise returning null </summary>
        public FieldPlanner GetFieldPlannerReference(bool checkFreeCheckerToo = true)
        {
            if (IsNull) return null;
            if (!checkFreeCheckerToo) { if (IsFreeChecker) return null; } else { if (FreeChecker != null) if (FreeChecker.SubFieldPlannerReference) return FreeChecker.SubFieldPlannerReference; }
            return OwnerPlanner;
        }

        public FieldPlanner GetFieldPlannerReference(FieldPlanner defaultPlanner, bool checkFreeCheckerToo = true)
        {
            if (IsNull) return null;
            if (!checkFreeCheckerToo) { if (IsFreeChecker) return null; } else { if (FreeChecker != null) if (FreeChecker.SubFieldPlannerReference) return FreeChecker.SubFieldPlannerReference; }
            if (OwnerPlanner == null) return defaultPlanner == null ? FieldPlanner.CurrentGraphExecutingPlanner : defaultPlanner;
            return OwnerPlanner;
        }

        public static bool operator ==(FieldPlannerReference a, FieldPlannerReference b) { return a.Equals(b); }
        public static bool operator !=(FieldPlannerReference a, FieldPlannerReference b) { return a.Equals(b); }

        public override bool Equals(object obj)
        {
            if ( (obj is FieldPlannerReference) == false ) return false;
            FieldPlannerReference fRef = (FieldPlannerReference)obj;

            if (OwnerPlanner == fRef.OwnerPlanner && FreeChecker == fRef.FreeChecker &&
                ForcedNull == fRef.ForcedNull && NumberedID == fRef.NumberedID)
                return true;

            return false;
        }


        public int GetSubFieldID()
        {
            if (IsSubField == false) return -1;
            if (OwnerPlanner == null) return -1;
            return OwnerPlanner.GetSubFieldID();
        }


        public override int GetHashCode() { return base.GetHashCode(); }

    }


    /// <summary>
    /// Interface implementing main required classes reference in order to be used during Build Planning
    /// </summary>
    public interface IFieldPlanningContainer
    {
        FieldPlanner ParentPlanner { get; }
        CheckerField3D LastestChecker { get; }
        PlannerResult LastestResult { get; }

    }

}