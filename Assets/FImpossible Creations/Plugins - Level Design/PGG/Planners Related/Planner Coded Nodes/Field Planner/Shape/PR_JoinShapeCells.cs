using FIMSpace.Generating.Checker;
using FIMSpace.Graph;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.Field.Shape
{

    public class PR_JoinShapeCells : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "   Join Field-Shape Cells" : "Join Field-Shape Cells"; }
        public override string GetNodeTooltipDescription { get { return "Joining cells of one Field Shape with another."; } }
        public override Color GetNodeColor() { return new Color(1.0f, 0.75f, 0.25f, 0.9f); }
        public override bool IsFoldable { get { return true; } }

        public override Vector2 NodeSize { get { return new Vector2(_EditorFoldout ? 230 : 210, _EditorFoldout ? 124 : 86); } }

        [Port(EPortPinType.Input, 1)] public PGGPlannerPort JoinWith;
        [HideInInspector][Port(EPortPinType.Input, 1)] public PGGPlannerPort ApplyTo;

        [Tooltip("Aligning center of joined shape with field with which shape is joined")]
        [HideInInspector] public bool AlignWithTargetField = false;
        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.CellsManipulation; } }

        public override void Execute(PlanGenerationPrint print, PlannerResult newResult)
        {
            ApplyTo.TriggerReadPort(true);
            JoinWith.TriggerReadPort(true);

            FieldPlanner plan = GetPlannerFromPort(ApplyTo, false);
            CheckerField3D myChe = ApplyTo.GetInputCheckerSafe;
            if (plan) myChe = plan.LatestResult.Checker;
            if (myChe == null) { return; }

            List<CheckerField3D> checkers = JoinWith.GetAllInputCheckers();
            if (checkers.Count == 0) { return; }

            for (int c = 0; c < checkers.Count; c++)
            {
                var chec = checkers[c];
                if (AlignWithTargetField) chec.RootPosition = myChe.RootPosition;
                myChe.Join(chec);
            }

            if (plan) plan.LatestResult.Checker = myChe;

            #region Debugging Gizmos
#if UNITY_EDITOR
            if (Debugging)
            {
                DebuggingInfo = "Joining fields cells";
                CheckerField3D myChec = myChe.Copy(false);
                CheckerField3D oChec = checkers[0].Copy(false);

                DebuggingGizmoEvent = () =>
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                    myChec.DrawFieldGizmos(true, false);
                    //for (int i = 0; i < myChec.ChildPositionsCount; i++)
                    //    Gizmos.DrawCube(myChec.GetWorldPos(i), myChe.RootScale);
                    Gizmos.color = new Color(0f, 0f, 1f, 0.5f);
                    oChec.DrawFieldGizmos(true, false);
                    //for (int i = 0; i < oChec.ChildPositionsCount; i++)
                    //    Gizmos.DrawCube(oChec.GetWorldPos(i), oChec.RootScale);
                };
            }
#endif
            #endregion

        }

#if UNITY_EDITOR

        private UnityEditor.SerializedProperty sp = null;
        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            baseSerializedObject.Update();
            base.Editor_OnNodeBodyGUI(setup);

            if (_EditorFoldout)
            {
                ApplyTo.AllowDragWire = true;
                GUILayout.Space(1);

                if (sp == null) sp = baseSerializedObject.FindProperty("ApplyTo");
                UnityEditor.SerializedProperty scp = sp.Copy();
                UnityEditor.EditorGUILayout.PropertyField(scp);
                scp.Next(false); UnityEditor.EditorGUILayout.PropertyField(scp);
            }
            else
            {
                ApplyTo.AllowDragWire = false;
            }

            baseSerializedObject.ApplyModifiedProperties();
        }

        public override void Editor_OnAdditionalInspectorGUI()
        {
            EditorGUILayout.LabelField("Debugging:", EditorStyles.helpBox);
            CheckerField3D chA = ApplyTo.GetInputCheckerSafe;
            if (chA != null) GUILayout.Label("Planner Cells: " + chA.ChildPositionsCount);

            CheckerField3D chB = JoinWith.GetInputCheckerSafe;
            if (chB != null) GUILayout.Label("JoinWith Cells: " + chB.ChildPositionsCount);
        }

#endif

    }
}