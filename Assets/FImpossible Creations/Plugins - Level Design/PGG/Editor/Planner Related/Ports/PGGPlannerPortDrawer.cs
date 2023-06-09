using UnityEditor;
using UnityEngine;

namespace FIMSpace.Graph
{

    [CustomPropertyDrawer(typeof(PGGPlannerPort))]
    public class PGGFieldPort_Drawer : NodePort_DrawerBase
    {
        PGGPlannerPort plPrt = null; PGGPlannerPort PlannerPort { get { if (plPrt == null) plPrt = port as PGGPlannerPort; return plPrt; } }
        protected override string InputTooltipText => "Field Planner " + base.InputTooltipText + "\n(Using " + DefaultValueInfo + " planner if not connected)";
        protected override string OutputTooltipText => "Field Planner " + base.OutputTooltipText + "\n(Using " + DefaultValueInfo + " planner if not connected)";
        string DefaultValueInfo { get { if (plPrt == null) return "(Self)"; else return plPrt.Editor_DefaultValueInfo; } }



        protected override void DrawLabel(Rect fieldRect)
        {
            if (port.PortState() == EPortPinState.Connected)
            {
                if (PlannerPort != null)
                {
                    displayContent.text = GetDisplayLabelPrefixText(PlannerPort);

                    SetLabelWidth();
                    EditorGUI.LabelField(fieldRect, displayContent);
                    RestoreLabelWidth();
                }
            }
            else // Disconnected
            {
                displayContent.text = GetDisplayLabelTextFull(PlannerPort);
                SetLabelWidth();
                EditorGUI.LabelField(fieldRect, displayContent.text);
                RestoreLabelWidth();
            }
        }



        protected override void DrawValueField(Rect fieldRect)
        {
            PlannerPort.UniquePlannerID = EditorGUI.IntField(fieldRect, GUIContent.none, PlannerPort.UniquePlannerID);
            if (PlannerPort.UniquePlannerID < -1) PlannerPort.UniquePlannerID = -1;
        }

        protected override void DrawValueFieldNoEditable(Rect fieldRect)
        {
            string suffix = "";

            // Drawing suffix of connected port
            if (port.IsInput)
            {
                PGGPlannerPort connected = GetConnectedPort(PlannerPort);
                if (connected != null)
                {
                    suffix = GetDisplayLabelSuffixText(connected);
                }
            }

            if (suffix == "") suffix = GetDisplayLabelSuffixText(PlannerPort);


            EditorGUI.LabelField(fieldRect, suffix);
        }


        protected override void DrawValueWithLabelField(Rect labelRect, Rect fieldRect, Rect bothRect)
        {
            if (port.IsOutput) // Output is not containing value field
            {
                displayContent.text = GetDisplayLabelPrefixText(PlannerPort);
                SetLabelWidth();
                EditorGUI.LabelField(labelRect, displayContent);
                EditorGUI.LabelField(fieldRect, GetDisplayLabelSuffixText(PlannerPort));
                RestoreLabelWidth();
                return;
            }

            // Input Port
            if (port.PortState() == EPortPinState.Connected)
            {
                displayContent.text = "";

                PGGPlannerPort otherPort = GetConnectedPort(port);
                if (otherPort != null) displayContent.text = GetDisplayLabelPrefixText(PlannerPort) + " " + GetDisplayLabelSuffixText(otherPort);

                if (displayContent.text == "") displayContent.text = GetDisplayLabelTextFull(PlannerPort);

                SetLabelWidth();
                PlannerPort.UniquePlannerID = EditorGUI.IntField(bothRect, displayContent, PlannerPort.UniquePlannerID);
                RestoreLabelWidth();
            }
            else // Disconnected
            {
                displayContent.text = GetDisplayLabelTextFull(PlannerPort);
                SetLabelWidth();
                PlannerPort.UniquePlannerID = EditorGUI.IntField(bothRect, displayContent, PlannerPort.UniquePlannerID);
                RestoreLabelWidth();
            }
        }

        PGGPlannerPort GetConnectedPort(NodePortBase port)
        {
            if (port != null) if (port.BaseConnection != null)
                {
                    PGGPlannerPort otherPort = port.BaseConnection.PortReference as PGGPlannerPort;
                    return otherPort;
                }

            return null;
        }


        string GetDisplayLabelTextFull(PGGPlannerPort port)
        {
            return GetDisplayLabelPrefixText(port) + " " + GetDisplayLabelSuffixText(port);
        }

        string GetDisplayLabelPrefixText(PGGPlannerPort port)
        {
            if (port == null) return "";
            if (!string.IsNullOrEmpty(port.OverwriteName)) return port.OverwriteName;
            if (port.Editor_DisplayVariableName) return port.DisplayName; else return "";
        }

        string GetDisplayLabelSuffixText(PGGPlannerPort port)
        {
            if (port == null) return "";

            if (port.UsingNumberedID)
            {
                if (port.ContainsMultiple)
                {
                    if (port.ContainsJustCheckers)
                    {
                        return "Shapes (" + plPrt.GetContainedCount() + ")";
                    }
                    else
                    {
                        return "Multiple (" + plPrt.GetContainedCount() + ")";
                    }
                }

                if (!string.IsNullOrEmpty(port.OverwriteName)) return "";
                return port.GetNumberedIDArrayString();
            }
            else // Containing
            {
                if (port.ContainsMultiple)
                {
                    
                    if (port.ContainsJustCheckers)
                    {
                        return "Shapes (" + plPrt.GetContainedCount() + ")";
                    }
                    else
                    {
                        return "Multiple (" + plPrt.GetContainedCount() + ")";
                    }
                }
                else
                {
                    if (port.OnlyReferenceContainer)
                        if (!port.ContainsAnyReference)
                        {
                            return "(None)";
                        }

                    if (port.ContainsForcedNull)
                    {
                        return "(None)";
                    }
                    else if (port.ContainsSubField)
                    {
                        return "SUB[" + port.ContainedSubFieldID + "] " + displayContent.text;
                    }
                    else if (port.ContainsJustChecker)
                    {
                        return (/*port.IsOutput ? "" : */ "(Shape)");
                    }
                }
            }

            return port.Editor_DefaultValueInfo;
        }


    }

}
