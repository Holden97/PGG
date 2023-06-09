using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FIMSpace.Generating.Checker
{
    public partial class CheckerField3D
    {
        public CheckerField3D GenerateMaskOutOfCheckers(List<CheckerField3D> ch, bool ignoreSelf, CheckerField3D additionalIgnore)
        {
            CheckerField3D mask = new CheckerField3D();

            for (int c = 0; c < ch.Count; c++)
            {
                var checker = ch[c];
                if (ignoreSelf) if (checker == this) continue;
                if (checker == additionalIgnore) continue;
                mask.Join(checker);
            }

            mask.RecalculateMultiBounds();

            return mask;
        }

        private List<FieldCell> _pathFind_openListC = new List<FieldCell>();
        private List<CheckerField3D> _pathFindListHelper = new List<CheckerField3D>();

        public CheckerField3D GeneratePathFindTowards(CheckerField3D start, CheckerField3D target, CheckerField3D worldSpaceCollision, PathFindParams findParams, Planning.FieldPlanner startField, Planning.FieldPlanner targetField, bool removeOverlappedCells = true, CheckerField3D pathParent = null)
        {
            _pathFindListHelper.Clear();
            if (worldSpaceCollision != null) _pathFindListHelper.Add(worldSpaceCollision);
            return GeneratePathFindTowards(start, target, _pathFindListHelper, findParams, startField, targetField, removeOverlappedCells, pathParent);
        }

        [NonSerialized] public FieldCell _GeneratePathFindTowards_FromStartCell;
        [NonSerialized] public FieldCell _GeneratePathFindTowards_OtherTargetCell;

        [NonSerialized] public FieldCell _GeneratePathFindTowards_PathBeginCell;
        [NonSerialized] public FieldCell _GeneratePathFindTowards_PathEndCell;


        [NonSerialized] public FieldCell _pathFind_cheapestNodeC = null;
        [NonSerialized] public FieldCell _pathFind_endCellOther = null;
        float _pathFind_cheapestCost = float.MaxValue;

        [NonSerialized] public FieldCell _pathFind_cheapestNodeDiscardedC = null;
        float _pathFind_cheapestDiscardedCost = float.MaxValue;

        bool _pathFind_skipStart = true;
        int steps = 0;

        /// <summary>
        /// Generating line towards target with collision detection
        /// </summary>
        public CheckerField3D GeneratePathFindTowards(CheckerField3D start, CheckerField3D target, List<CheckerField3D> worldSpaceCollision, PathFindParams findParams, Planning.FieldPlanner startField, Planning.FieldPlanner targetField, bool removeOverlappedCells = true, CheckerField3D pathParent = null)
        {
            FieldCell startCheckerBeginCell = start.GetNearestCellTowardsWorldPos3x3(start.GetFullBoundsWorldSpace().center);
            Vector3 startCellWorldPos = start.GetWorldPos(startCheckerBeginCell);

            FieldCell targetCell = target.GetNearestCellTo(start, true);
            Vector3 targetCellWorldPos = target.GetWorldPos(targetCell);
            steps = 0;
            _pathFind_skipStart = true; // To skip start field collision until leaving it

            if (worldSpaceCollision != null)
            {
                worldSpaceCollision.Remove(target);
            }

            if (findParams.IgnoreSelfCollision)
            {
                worldSpaceCollision.Remove(start);
                worldSpaceCollision.Remove(this);
            }
            else
            {
                if (pathParent != null) worldSpaceCollision.Add(pathParent);
            }



            if (findParams.StartOnSide)
            {
                Vector3Int startLocal = startCheckerBeginCell.Pos;
                Vector3Int newLocal = startCheckerBeginCell.Pos;

                float nearestDist = float.MaxValue;

                // Check left right front back directions, search for outside cell and check if it's nearest start cell towards target
                for (int i = 0; i < GetDefaultDirections.Count; i++)
                {
                    var dir = defaultLineFindDirections[i];

                    for (int c = 1; c < start.ChildPositionsCount; c++) // Search forward for free cell, search with limited distance
                    {
                        Vector3Int localPos = startLocal + dir.Dir * c;
                        var nCell = start.GetCell(localPos, false);

                        if (FGenerators.IsNull(nCell))
                        {
                            Vector3 worldPos = start.LocalToWorld(startLocal + dir.Dir * c);
                            if (!PathFind_IsCollidingInWorldPos(worldSpaceCollision, worldPos))
                            {
                                float distToTarget = Vector3.Distance(worldPos, targetCellWorldPos);
                                if (distToTarget < nearestDist)
                                {
                                    nearestDist = distToTarget;
                                    newLocal = localPos - dir.Dir;
                                }
                            }

                            break;
                        }
                    }
                }


                #region If using - Check if some command exists around - Start Cell Condition check

                if (findParams.DontAllowStartToo)
                    if (findParams.DontAllowFinishDist > 0)
                    {
                        if (startField && startField.LatestResult != null && startField.LatestResult.CellsInstructions != null)
                        {
                            var instructions = startField.LatestResult.CellsInstructions;
                            Vector3 pathInStartLocal = newLocal;
                            for (int s = 0; s < instructions.Count; s++)
                            {
                                var instr = instructions[s];
                                if (instr == null) continue;
                                if (findParams.DontAllowFinishId > -1) if (instr.Id != findParams.DontAllowFinishId) continue;

                                float distance = Vector3.Distance(pathInStartLocal, instr.pos);
                                if (distance < findParams.DontAllowFinishDist)
                                {
                                    newLocal = startLocal;
                                    break;
                                }
                            }
                        }
                    }

                #endregion


                if (startLocal != newLocal)
                {
                    FieldCell newStartCell = start.GetCell(newLocal);
                    if (FGenerators.NotNull(newStartCell)) { startCheckerBeginCell = newStartCell; }
                }

            }



            if (findParams.SnapToInstructionsOnDistance > 0f) // Snapping search points
            {
                // Swap start point to be in the same position as some previous start position
                if (startField && startField.LatestResult != null)
                {
                    SpawnInstructionGuide nearest = null;
                    float nearestDist = float.MaxValue;
                    float maxDist = findParams.SnapToInstructionsOnDistance * start.RootScale.x;

                    for (int i = 0; i < startField.LatestResult.CellsInstructions.Count; i++)
                    {
                        SpawnInstructionGuide instr = startField.LatestResult.CellsInstructions[i];
                        if (FGenerators.IsNull(instr.HelperCellRef)) continue;
                        if (startField.LatestChecker.GetCell(instr.HelperCellRef.Pos) != (instr.HelperCellRef)) continue;
                        if (start.HasFreeOutsideDirectionFlat(instr.HelperCellRef) == false) continue; // Inside field cell instruction

                        Vector3 wPos = start.LocalToWorld(instr.HelperCellRef.Pos);
                        float dist = Vector3.Distance(wPos, startCellWorldPos);
                        if (dist > maxDist) continue;

                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearest = instr;
                        }
                    }

                    if (nearest != null)
                    {
                        startCheckerBeginCell = nearest.HelperCellRef;
                        startCellWorldPos = start.LocalToWorld(nearest.HelperCellRef.Pos);
                    }
                }

                // Search for end position too
                if (targetField && targetField.LatestResult != null)
                {
                    SpawnInstructionGuide nearest = null;
                    float nearestDist = float.MaxValue;
                    float maxDist = findParams.SnapToInstructionsOnDistance * target.RootScale.x;

                    for (int i = 0; i < targetField.LatestResult.CellsInstructions.Count; i++)
                    {
                        SpawnInstructionGuide instr = targetField.LatestResult.CellsInstructions[i];
                        if (FGenerators.IsNull(instr.HelperCellRef)) continue;
                        if (targetField.LatestChecker.GetCell(instr.HelperCellRef.Pos) != (instr.HelperCellRef)) continue;
                        if (target.HasFreeOutsideDirectionFlat(instr.HelperCellRef) == false) continue; // Inside field cell instruction

                        Vector3 wPos = target.LocalToWorld(instr.HelperCellRef.Pos);
                        float dist = Vector3.Distance(wPos, targetCellWorldPos);
                        if (dist > maxDist) continue;

                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearest = instr;
                        }
                    }

                    if (nearest != null)
                    {
                        targetCell = nearest.HelperCellRef;
                        targetCellWorldPos = target.LocalToWorld(nearest.HelperCellRef.Pos);
                    }
                }
            }


            // Centering end cell
            if (findParams.TryEndCentered)
            {
                // Check only one cell left/right (TODO: Better solution)
                if (target.HasFreeOutsideDirectionFlat(targetCell))
                {
                    targetCell = target.GetCenteredCellOnLongestOutsideDirectionEdge(targetCell);
                    targetCellWorldPos = target.LocalToWorld(targetCell.Pos);
                }
            }


            _GeneratePathFindTowards_FromStartCell = startCheckerBeginCell;
            _GeneratePathFindTowards_OtherTargetCell = targetCell;
            _GeneratePathFindTowards_PathBeginCell = null;
            _GeneratePathFindTowards_PathEndCell = null;

            CheckerField3D path = new CheckerField3D();
            path.CopyParamsFrom(this);

            if (pathParent != null)
            {
                path.RootPosition = pathParent.RootPosition;
                path.RootRotation = pathParent.RootRotation;
                path.RootScale = pathParent.RootScale;
            }

            Vector3Int targetPosInPathLocal = path.WorldToGridPos(targetCellWorldPos);

            var pathStartCell = path.AddLocal(GetLocalPositionOfOther(startCheckerBeginCell.Pos, start));

            pathStartCell._PathFind_CalculateTotalDistance3D(targetCellWorldPos, !findParams.SphericalDistanceMeasure);
            pathStartCell._PathFind_movementCost = 0;
            pathStartCell.ParentCell = null;


            #region Commented but can be helpful later

            //FDebug.DrawBounds3D(GetFullBoundsWorldSpace(), Color.cyan);
            //UnityEngine.Debug.DrawRay(GetFullBoundsWorldSpace().center, Vector3.up + Vector3.one * 0.05f, Color.green, 1.01f);

            //FieldCell targetCell = target.GetNearestCellTowardsWorldPos3x3(target.GetFullBoundsWorldSpace().center);
            //worldSpaceCollision.RoundRootPositionAccordingly(this);
            //worldSpaceCollision.DebugLogDrawBoundings(Color.yellow);

            //start.DebugLogDrawBoundings(Color.white);
            //target.DebugLogDrawBoundings(Color.green);

            //start.DebugLogDrawCellIn(startCellWorldPos, Color.white);
            //target.DebugLogDrawCellIn(targetCellWorldPos, Color.green);
            //UnityEngine.Debug.DrawLine(startCellWorldPos, targetCellWorldPos, Color.gray, 1.01f);
            //UnityEngine.Debug.DrawLine(_nearestMyBoundsPos, targetCellWorldPos, Color.green, 1.01f);
            //UnityEngine.Debug.DrawLine(path.GetWorldPos(startCell), path.GetWorldPos(targetPosInPathLocal), Color.magenta, 1.01f);
            #endregion


            _pathFind_openListC.Clear();
            _pathFind_openListC.Add(pathStartCell);

            _pathFind_cheapestCost = float.MaxValue;
            _pathFind_cheapestNodeC = null;

            _pathFind_cheapestDiscardedCost = float.MaxValue;
            _pathFind_cheapestNodeDiscardedC = null;


            findParams.ResetBeforePathFind();

            int limitIter = findParams.SearchStepIterationLimit + 4 * Mathf.RoundToInt(Vector3Int.Distance(targetPosInPathLocal, pathStartCell.Pos));
            int l = 0; // Counter which defines number of iterations during searching, if searching is too long we can stop it and create path based on created data
            FieldCell cheapest = null;
            _pathFind_endCellOther = null;
            bool found = false;

            while (_pathFind_openListC.Count > 0) /* If there are nodes in queue to check */
            {

                #region Iteration Limit Check and Log + Discarded nearest connection compute

                if (l > limitIter) /* If there is too much iterations, let's stop and return path to nearest point to player */
                {
                    _pathFind_cheapestCost = float.MaxValue;

                    if (findParams.ConnectEvenDiscarded)
                    {
                        // Check if there is possible path cell completing on the target field regardless condition
                        if (FGenerators.NotNull(_pathFind_cheapestNodeDiscardedC))
                        {
                            //path.DebugLogDrawCellInWorldSpace(_pathFind_cheapestNodeDiscardedC, Color.red);
                            _pathFind_cheapestCost = -1f;
                            _pathFind_cheapestNodeC = _pathFind_cheapestNodeDiscardedC;
                            _pathFind_endCellOther = _pathFind_cheapestNodeDiscardedC;
                            cheapest = _pathFind_cheapestNodeDiscardedC;
                            found = true;
                            break;
                        }
                    }

                    if (_pathFind_cheapestCost != -1)
                    {
                        if (findParams.LogWarnings) Debug.Log(">>>>>>>>>>>> Searching path was too long, stopped (openList.Count=" + _pathFind_openListC.Count + ") <<<<<<<<<<<<");
                        break;
                    }
                }

                #endregion


                cheapest = _pathFind_openListC[0];
                _pathFind_cheapestNodeC = _pathFind_openListC[0];

                // Opening node - creating child nodes around in the step
                PathFind_OpenNode(cheapest, path, start, target, worldSpaceCollision, targetPosInPathLocal, findParams, targetField);

                if (_pathFind_cheapestCost == -1f)
                {
                    // COMPLETE \\
                    cheapest = _pathFind_cheapestNodeC;
                    _pathFind_endCellOther = _pathFind_cheapestNodeC;

                    found = true;
                    break;
                }


                // Remove current node to check new ones with _pathFind_openListC[0]
                if (cheapest != null) _pathFind_openListC.Remove(cheapest);

                steps = l;
                l++;
            }

            if (!found)
            {
                if (findParams.DiscardOnNoPathFound)
                    return null;
            }


            PathFind_ReverseTracePath(cheapest, path);


            #region Commented but can be helpful later

            //if (cheapest != null)
            //{
            //    // Align start cell to be first before out of start checker volume
            //    if (path.AllCells.Count > 1)
            //    {
            //        path.DebugLogDrawCellInWorldSpace(path.AllCells[0], Color.cyan);

            //        int newStartCellIndex = 0;

            //        // TODO: Allow overlapping? When generating vent system inside rooms?
            //        // Check Only when begin [1] cell is contained by start volume
            //        for (int i = 0; i < path.AllCells.Count; i++)
            //        {
            //            Vector3 nextCellWorldPos = path.GetWorldPos( path.AllCells[i]);

            //            if ( start.ContainsWorld(nextCellWorldPos) == false)
            //            {
            //                newStartCellIndex = i;
            //                break;
            //            }
            //        }

            //        if ( newStartCellIndex > 0)
            //        {
            //            UnityEngine.Debug.Log("new = " + newStartCellIndex);
            //            for (int i = 0; i < newStartCellIndex; i++)
            //            {
            //                path.DebugLogDrawCellInWorldSpace(path.AllCells[0], Color.red);
            //                path.RemoveLocal(0);
            //            }
            //        }
            //    }
            //}



            //for (int i = path.ChildPositionsCount - 1; i >= 0; i--)
            //{
            //    var cell = path.AllCells[i];
            //    if (cell._PathFind_LastUsedStep == null) continue;
            //    cell._PathFind_CheckPathDirectionOrigination(cell._PathFind_LastUsedStep);
            //}

            #endregion


            bool skipReAdjustingStartCell = false;

            if (path.AllCells.Count < 5)
            {
                // Check alignment in case
                if (start.IsAnyAligning(target, true))
                {
                    if (findParams.DiscardIfAligning) return null;

                    // Find aligning cell nearest to the target end path position
                    float nearest = float.MaxValue;
                    int nearestI = -1;
                    Vector3 desiredTargetWorldPos = target.GetWorldPos(_GeneratePathFindTowards_OtherTargetCell);

                    for (int i = 0; i < start.AllCells.Count; i++)
                    {
                        FieldCell searchCell = start.AllCells[i];
                        FieldCell aligning = start.IsAnyAligning(searchCell, target, false);

                        if (FGenerators.NotNull(aligning))
                        {
                            float distance = Vector3.Distance(start.GetWorldPos(searchCell), desiredTargetWorldPos);
                            if (distance < nearest)
                            {
                                nearest = distance;
                                nearestI = i;
                            }
                        }
                    }

                    if (nearestI != -1)
                    {
                        _GeneratePathFindTowards_FromStartCell = start.GetCell(nearestI);
                        skipReAdjustingStartCell = true;
                    }

                }
            }


            // Adjust start/end cell for the start field to be placed on before path start cell
            if (!skipReAdjustingStartCell && path.AllCells.Count > 1)
            {

                for (int i = path.AllCells.Count - 2; i >= 0; i--)
                {
                    FieldCell pathPreCell = path.AllCells[i + 1];
                    FieldCell pathCell = path.AllCells[i];

                    Vector3 preWorldPos = path.GetWorldPos(pathPreCell);
                    Vector3 currWorldPos = path.GetWorldPos(pathCell);
                    // if (start.ContainsWorld(currWorldPos) ) start.DebugLogDrawCellIn(currWorldPos, Color.gray); else start.DebugLogDrawCellIn(currWorldPos, Color.red);

                    if (start.ContainsWorld(currWorldPos) && !start.ContainsWorld(preWorldPos))
                    {
                        _GeneratePathFindTowards_FromStartCell = start.GetCellInWorldPos(currWorldPos);
                        break;
                    }
                }


                #region Commented but can be helpful later
                // Towards/Target field end cell adjustement if required
                //for (int i = 1; i < path.AllCells.Count; i++)
                //{
                //    path.DebugLogDrawCellInWorldSpace(path.AllCells[i - 1], Color.blue);

                //    FieldCell pathPreCell = path.AllCells[i - 1];
                //    FieldCell pathCell = path.AllCells[i];

                //    Vector3 preWorldPos = path.GetWorldPos(pathPreCell);
                //    Vector3 currWorldPos = path.GetWorldPos(pathCell);

                //    if (target.ContainsWorld(currWorldPos) && !target.ContainsWorld(preWorldPos))
                //    {
                //        //start.DebugLogDrawCellIn(currWorldPos, Color.yellow);
                //        //start.DebugLogDrawCellIn(preWorldPos, Color.red);
                //        _GeneratePathFindTowards_OtherTargetCell = target.GetCellInWorldPos(currWorldPos);
                //        break;
                //    }
                //}
                #endregion

            }

            if (path.AllCells.Count > 0)
            {
                _GeneratePathFindTowards_PathBeginCell = path.AllCells[0];
                _GeneratePathFindTowards_PathEndCell = path.AllCells[path.AllCells.Count - 1];
            }

            if (removeOverlappedCells)
            {
                path.RemoveCellsCollidingWith(start);
                path.RemoveCellsCollidingWith(target);

                // Adjust path start / end cell references
                _GeneratePathFindTowards_PathBeginCell = null;
                _GeneratePathFindTowards_PathEndCell = null;

                if (path.AllCells.Count > 0)
                {
                    _GeneratePathFindTowards_PathBeginCell = path.AllCells[0];
                    _GeneratePathFindTowards_PathEndCell = path.AllCells[path.AllCells.Count - 1];
                }
            }


            #region Commented but can be helpful later


            //path.DebugLogDrawCellInWorldSpace(_GeneratePathFindTowards_PathBeginCell, Color.green);
            //path.DebugLogDrawCellInWorldSpace(_GeneratePathFindTowards_PathEndCell, Color.yellow);

            //UnityEngine.Debug.Log("sizes " + path.RootScale + " start scale: " + RootScale + " tgt: " + target.RootScale);

            //path.RecalculateMultiBounds();
            //path.DebugLogDrawBoundings(Color.magenta);

            #endregion


            return path;
        }

        private bool HasFreeOutsideDirectionFlat(FieldCell cell)
        {
            if (ContainsLocal(cell.Pos + new Vector3Int(1, 0, 0)) == false) return true;
            if (ContainsLocal(cell.Pos + new Vector3Int(-1, 0, 0)) == false) return true;
            if (ContainsLocal(cell.Pos + new Vector3Int(0, 0, 1)) == false) return true;
            if (ContainsLocal(cell.Pos + new Vector3Int(0, 0, -1)) == false) return true;
            return false;
        }

        private FieldCell GetCenteredCellOnLongestOutsideDirectionEdge(FieldCell cell)
        {
            Vector3Int dir = Vector3Int.zero;
            FieldCell cCell = cell;
            int longestEdge = int.MinValue;

            for (int d = 0; d < 4; d++)
            {
                if (d == 0) dir = new Vector3Int(1, 0, 0);
                else if (d == 1) dir = new Vector3Int(0, 0, 1);
                else if (d == 2) dir = new Vector3Int(-1, 0, 0);
                else if (d == 3) dir = new Vector3Int(0, 0, -1);

                if (ContainsLocal(cell.Pos + dir)) continue;

                Vector3Int edgeDir = (Quaternion.Euler(0f, 90f, 0f) * dir).V3toV3Int();

                // First end of the edge
                int edgeE = 0;
                for (int c = 1; c < 4; c++) // max 4 cells check
                {
                    if (!ContainsLocal(cell.Pos + edgeDir * c)) break;
                    if (ContainsLocal(cell.Pos + edgeDir * c + dir)) break; // front cell detected
                    edgeE = c;
                }

                // Second end of the edge
                int edgeS = 0;
                for (int c = 1; c < 4; c++) // max 4 cells check
                {
                    if (!ContainsLocal(cell.Pos - edgeDir * c)) break;
                    if (ContainsLocal(cell.Pos - edgeDir * c + dir)) break; // front cell detected
                    edgeS = -c;
                }

                int edgeLen = Mathf.Abs(edgeE - edgeS);

                if (edgeLen > 0)
                {
                    if (edgeLen > longestEdge)
                    {
                        // Get middle position for the edge
                        int mid = Mathf.RoundToInt(Mathf.LerpUnclamped(edgeS, edgeE, 0.5f));

                        if (mid != 0)
                        {
                            var nCCell = GetCell(cell.Pos + edgeDir * mid, FailedToSet);
                            if (FGenerators.NotNull(nCCell) && nCCell.InTargetGridArea)
                            {
                                cCell = nCCell;
                                longestEdge = edgeLen;
                            }
                        }
                    }
                }
            }

            return cCell;
        }


        private bool IsAnyCellAroundOutsideDirection(Vector3Int localPos, bool checkY, bool checkDiagonals = false)
        {
            if (ContainsLocal(localPos + new Vector3Int(1, 0, 0)) == true) return true;
            if (ContainsLocal(localPos + new Vector3Int(-1, 0, 0)) == true) return true;
            if (ContainsLocal(localPos + new Vector3Int(0, 0, 1)) == true) return true;
            if (ContainsLocal(localPos + new Vector3Int(0, 0, -1)) == true) return true;

            if (checkY)
            {
                if (ContainsLocal(localPos + new Vector3Int(0, 1, 0)) == true) return true;
                if (ContainsLocal(localPos + new Vector3Int(0, -1, 0)) == true) return true;
            }

            if (checkDiagonals)
            {
                if (ContainsLocal(localPos + new Vector3Int(1, 0, 1)) == true) return true;
                if (ContainsLocal(localPos + new Vector3Int(-1, 0, 1)) == true) return true;
                if (ContainsLocal(localPos + new Vector3Int(1, 0, -1)) == true) return true;
                if (ContainsLocal(localPos + new Vector3Int(-1, 0, -1)) == true) return true;
            }

            return false;
        }

        int None { get { return int.MaxValue; } }

        public FieldCell GetNearestCellInWorldPos(Vector3 worldPos, int maxDist = 32)
        {
            Vector3Int local = WorldToGridPos(worldPos);

            // Check if in choosed position there is already cell
            FieldCell inExact = GetCell(local);

            if (FGenerators.CheckIfExist_NOTNULL(inExact)) if (inExact.InTargetGridArea)
                {
                    return inExact;
                }

            // If origin point is too far, reposition it towards nearest available grid area
            Bounds localB = GetFullBoundsLocalSpace();

            if (localB.Contains(local) == false)
            {
                Vector3 nrst = localB.ClosestPoint(local);
                local = nrst.V3toV3Int();
            }

            return CubicSearchForFirstCell(local, localB, maxDist);
        }

        //float PathFind_CheckDistanceToCommandCondition(Vector3Int locToCheck, Planning.FieldPlanner field, PathFindParams findParams)
        //{
        //    Vector3 pathInStartLocal = locToCheck;

        //    var instructions = field.LatestResult.CellsInstructions;
        //    for (int s = 0; s < instructions.Count; s++)
        //    {
        //        var instr = instructions[s];
        //        if (instr == null) continue;
        //        if (findParams.DontAllowFinishId > -1) if (instr.Id != findParams.DontAllowFinishId) continue;

        //        float distance = Vector3.Distance(pathInStartLocal, instr.pos);
        //        if (distance < findParams.DontAllowFinishDist)
        //        {
        //            newLocal = startLocal;
        //            break;
        //        }
        //    }
        //}

        public Vector3 GetNearestContainedWorldPosTo(Vector3 worldPos, int maxDist = 32)
        {
            FieldCell cell = GetNearestCellInWorldPos(worldPos, maxDist);
            if (FGenerators.CheckIfExist_NOTNULL(cell)) return GetWorldPos(cell);
            return Vector3.zero;
        }

        FieldCell _cubSearchRes = null;
        Vector3Int _cubSearchOrig = Vector3Int.zero;
        private bool _CubicSearchCheck(int x = 0, int y = 0, int z = 0)
        {
            _cubSearchRes = GetCell(_cubSearchOrig + new Vector3Int(x, y, z));
            if (FGenerators.CheckIfExist_NOTNULL(_cubSearchRes)) if (_cubSearchRes.InTargetGridArea) { return true; } else { _cubSearchRes = null; }

            return false;
        }

        /// <summary> Search in flat expanding space X Z. Y is checked only on the 'local.y' level </summary>
        public FieldCell CubicSearchForFirstCell(Vector3Int local, Bounds localFullBounds, int maxDist = 32)
        {
            _cubSearchRes = null;
            _cubSearchOrig = local;

            if (!_CubicSearchCheck(0, 0, 0))
            {
                bool xPStop = false;
                bool xNStop = false;
                bool zPStop = false;
                bool zNStop = false;

                for (int d = 1; d < maxDist; d++)
                {
                    bool refillBreak = false;

                    if (xPStop == false) xPStop = !IsXContainedIn(_cubSearchOrig.x + d, localFullBounds);
                    if (xNStop == false) xNStop = !IsXContainedIn(_cubSearchOrig.x - d, localFullBounds);
                    if (zPStop == false) zPStop = !IsZContainedIn(_cubSearchOrig.z + d, localFullBounds);
                    if (zNStop == false) zNStop = !IsZContainedIn(_cubSearchOrig.z - d, localFullBounds);

                    for (int r = 0; r <= d; r++)
                    {
                        if (r > 0)
                        {
                            if (xPStop == false) xPStop = !IsXContainedIn(_cubSearchOrig.x + r, localFullBounds);
                            if (xNStop == false) xNStop = !IsXContainedIn(_cubSearchOrig.x - r, localFullBounds);
                            if (zPStop == false) zPStop = !IsZContainedIn(_cubSearchOrig.z + r, localFullBounds);
                            if (zNStop == false) zNStop = !IsZContainedIn(_cubSearchOrig.z - r, localFullBounds);
                        }

                        if (zPStop == false)
                        {
                            if (xPStop == false) if (_CubicSearchCheck(d, 0, r)) { refillBreak = true; break; }
                            if (xNStop == false) if (_CubicSearchCheck(-d, 0, r)) { refillBreak = true; break; }
                        }

                        if (zNStop == false)
                        {
                            if (xPStop == false) if (_CubicSearchCheck(d, 0, -r)) { refillBreak = true; break; }
                            if (xNStop == false) if (_CubicSearchCheck(-d, 0, -r)) { refillBreak = true; break; }
                        }

                        if (xPStop == false)
                        {
                            if (zPStop == false) if (_CubicSearchCheck(r, 0, d)) { refillBreak = true; break; }
                            if (zNStop == false) if (_CubicSearchCheck(r, 0, -d)) { refillBreak = true; break; }
                        }

                        if (xNStop == false)
                        {
                            if (zPStop == false) if (_CubicSearchCheck(-r, 0, d)) { refillBreak = true; break; }
                            if (zNStop == false) if (_CubicSearchCheck(-r, 0, -d)) { refillBreak = true; break; }
                        }
                    }

                    if (refillBreak) break;
                }
            }

            return _cubSearchRes;
        }

        public FieldCell GetNearestCellTowardsWorldPos3x3(Vector3 worldPos)
        {
            FieldCell inExact = GetCellInWorldPos(worldPos);
            if (FGenerators.CheckIfExist_NOTNULL(inExact)) if (inExact.InTargetGridArea) return inExact;

            Vector3 targetPoint = GetNearestContainedWorldPosTo(worldPos); // Bounds based world position
            Vector3Int startPoint = WorldToGridPos(targetPoint); // Convert to grid position

            FieldCell startCell = Grid.GetEmptyCell(startPoint); // Get reference cell

            FieldCell[] cells = Grid.Get3x3Square(startCell, false); // Get surroundings
            FieldCell nrst = Grid.GetNearestFrom(startCell, cells);

            return nrst;
        }


        /// <summary>
        /// Recurent tracing path from end to start
        /// </summary>
        private void PathFind_ReverseTracePath(FieldCell cheapest, CheckerField3D owner)
        {
            if (cheapest == null) { return; }
            if (cheapest.ParentCell == null) { return; }
            //UnityEngine.Debug.DrawLine(owner.GetWorldPos(cheapest), owner.GetWorldPos(cheapest.ParentCell), Color.yellow, 1.01f);
            PathFind_ReverseTracePath(cheapest.ParentCell, owner);
            owner.AddLocal(cheapest.Pos);
        }

        private FieldCell PathFind_TraceFirstCellOfTarget(FieldCell cheapest, CheckerField3D target, CheckerField3D owner)
        {
            if (cheapest.ParentCell == null) return cheapest;

            FieldCell tCell = target.GetCellInWorldPos(owner.GetWorldPos(cheapest));
            Vector3 wPos2 = owner.GetWorldPos(cheapest.ParentCell);

            if (FGenerators.NotNull(tCell)
                && FGenerators.IsNull(target.GetCellInWorldPos(wPos2)))
            {
                return tCell;
            }

            return PathFind_TraceFirstCellOfTarget(cheapest.ParentCell, target, owner);
        }

        void PathFind_OpenNode(FieldCell originNode, CheckerField3D pathChecker, CheckerField3D startChecker, CheckerField3D targetChecker, List<CheckerField3D> collisionChecker, Vector3Int targetPathEndLocalPos, PathFindParams findParams, Planning.FieldPlanner targetField)
        {

            originNode._PathFind_status = -1; // Lock cell
            Vector3Int nodeOriginInGridLocal = originNode.Pos;


            #region Open Neightbours


            Vector3 pathScale = pathChecker.RootScale;
            var targetInvMX = targetChecker.MatrixInverse;

            // Searching for nearest point towards target
            for (int i = 0; i < findParams.directions.Count; i++)
            {
                LineFindHelper findDir = findParams.directions[i];

                Vector3Int offsettedPosGridLocal = nodeOriginInGridLocal + findDir.Dir;

                if (findParams.WorldSpace == false) // Local space limit check
                {
                    if (findParams.IsOutOfLimitsLocalSpace(offsettedPosGridLocal)) continue;
                }

                FieldCell checkedPathCell = pathChecker.Grid.GetEmptyCell(offsettedPosGridLocal);
                Vector3 checkedWorldPos = pathChecker.GetWorldPos(checkedPathCell);
                //pathChecker.DebugLogDrawCellIn(checkedWorldPos, Color.yellow * 0.8f);

                if (findParams.WorldSpace) // World space limit check
                {
                    if (findParams.IsOutOfLimitsWorldSpace(checkedWorldPos)) continue;
                }

                // Y Cells Separation Condition Check
                if (findParams.YLevelSeparation > 0)
                {
                    bool separateY = false;

                    for (int s = 1; s <= findParams.YLevelSeparation; s++)
                    {
                        if (originNode._PathFind_CheckIfCurrentPathContainsCellInPosition(offsettedPosGridLocal + new Vector3Int(0, s, 0), findDir)) { separateY = true; break; }
                        if (originNode._PathFind_CheckIfCurrentPathContainsCellInPosition(offsettedPosGridLocal - new Vector3Int(0, s, 0), findDir)) { separateY = true; break; }
                    }

                    if (separateY) continue;
                }

                Vector3 offsettedWorldPos = pathChecker.GetWorldPos(offsettedPosGridLocal);

                if (findParams.CollisionYMargins != Vector2.zero)
                {
                    bool collisionY = false;

                    if (findParams.CollisionYMargins.x > 0)
                        for (int above = 1; above <= findParams.CollisionYMargins.x; above++)
                        {
                            collisionY = PathFind_IsCollidingInWorldPos(collisionChecker, offsettedWorldPos + new Vector3(0, above * pathScale.y, 0), targetChecker, startChecker);
                            if (collisionY) break;
                        }

                    if (!collisionY)
                        if (findParams.CollisionYMargins.y > 0)
                            for (int below = 1; below <= findParams.CollisionYMargins.y; below++)
                            {
                                collisionY = PathFind_IsCollidingInWorldPos(collisionChecker, offsettedWorldPos + new Vector3(0, below * -pathScale.y, 0), targetChecker, startChecker);
                                if (collisionY) break;
                            }

                    if (collisionY) continue;
                }


                // Reaching target checker field
                if (targetChecker.ContainsWorld(checkedWorldPos, targetInvMX, false))
                {

                    if (originNode._PathFind_LastUsedStep != null)
                    {
                        if (originNode._PathFind_LastUsedStep.DisallowFinishOn)
                        {
                            PathFind_CheckForCheapestDiscarded(targetChecker, pathChecker, checkedWorldPos, originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                            continue;
                        }
                        else // Check one step back too
                        {

                            //if (FGenerators.NotNull(originNode.ParentCell))
                            //    if (FGenerators.NotNull(originNode.ParentCell._PathFind_LastUsedStep))
                            //        if (originNode.ParentCell._PathFind_LastUsedStep.DisallowFinishOn)
                            //        {
                            //            continue;
                            //        }
                        }

                    }


                    #region If using - Check if some command exists around

                    if (findParams.DontAllowFinishDist > 0)
                    {
                        if (targetField && targetField.LatestResult != null && targetField.LatestResult.CellsInstructions != null)
                        {
                            Vector3 pathInTargetLocal = targetChecker.WorldToLocal(checkedWorldPos);
                            bool dontAllowFinish = false;

                            var instructions = targetField.LatestResult.CellsInstructions;
                            for (int s = 0; s < instructions.Count; s++)
                            {
                                var instr = instructions[s];
                                if (instr == null) continue;

                                if (findParams.DontAllowFinishId > -1) if (instr.Id != findParams.DontAllowFinishId) continue;

                                float distance = Vector3.Distance(pathInTargetLocal, instr.pos);
                                if (distance < findParams.DontAllowFinishDist)
                                {
                                    dontAllowFinish = true;
                                    break;
                                }
                            }

                            if (dontAllowFinish)
                            {
                                // Hard condition
                                //PathFind_CheckForCheapestDiscarded(originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                                continue;
                            }
                        }
                    }

                    #endregion


                    #region If using - check if there are cells around 


                    if (findParams.End_RequireCellsOnLeftSide > 0)
                    {
                        Quaternion stepRotation = Quaternion.LookRotation(findDir.Dir);
                        bool hasCellsOnSide = true;
                        for (int s = 0; s < findParams.End_RequireCellsOnLeftSide; s++)
                        {
                            Vector3 checkPos = pathChecker.GetWorldPos(checkedPathCell.Pos + (stepRotation * Vector3.left).V3toV3Int());
                            if (!targetChecker.ContainsWorld(checkPos)) { hasCellsOnSide = false; break; }
                        }

                        if (!hasCellsOnSide)
                        {
                            PathFind_CheckForCheapestDiscarded(targetChecker, pathChecker, checkedWorldPos, originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                            continue;
                        }
                    }

                    if (findParams.End_RequireCellsOnRightSide > 0)
                    {
                        Quaternion stepRotation = Quaternion.LookRotation(findDir.Dir);
                        bool hasCellsOnSide = true;
                        for (int s = 0; s < findParams.End_RequireCellsOnRightSide; s++)
                        {
                            Vector3 checkPos = pathChecker.GetWorldPos(checkedPathCell.Pos + (stepRotation * Vector3.right).V3toV3Int());
                            if (!targetChecker.ContainsWorld(checkPos)) { hasCellsOnSide = false; break; }
                        }

                        if (!hasCellsOnSide)
                        {
                            PathFind_CheckForCheapestDiscarded(targetChecker, pathChecker, checkedWorldPos, originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                            continue;
                        }
                    }

                    #endregion


                    if (findDir.DisallowFinishOn == false)
                    {
                        // COMPLETE \\
                        _pathFind_cheapestCost = -1f;
                        _pathFind_cheapestNodeC = pathChecker.AddWorld(checkedWorldPos);
                        _GeneratePathFindTowards_OtherTargetCell = targetChecker.GetCellInWorldPos(checkedWorldPos, true, targetInvMX);
                        _PathFindValidateNode(startChecker, checkedWorldPos, originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                        return;
                    }
                    else // Pathfind direction not allows to end with this direction path find step
                    {
                        continue;
                    }
                }

                // Stop if it was checked already
                if (checkedPathCell._PathFind_status != 0)
                {
                    continue;
                }



                if (findDir.AllowUseSinceStep > 0)
                {


                    // Checking distance to base to use this direction

                    if (startChecker.ContainsWorld(checkedWorldPos))
                    {
                        continue;
                    }
                    else
                    {
                        Vector3 startCheckerLocal = startChecker.WorldToLocal(LocalToWorld(originNode.Pos));
                        if (startChecker.IsAnyCellAroundOutsideDirection(startCheckerLocal.V3toV3Int(), true))
                        {
                            continue;
                        }
                        else
                        {
                            // Skip step direction if not enough steps was already proceeded
                            // If current path length is smaller than required length to use this direction 
                            if (originNode._PathFind_CalculateCurrentLength() < findDir.AllowUseSinceStep)
                            {
                                continue;
                            }
                            else
                            {
                                if (originNode._PathFind_CalculateCurrentLength_Exclude(startChecker) < findDir.AllowUseSinceStep) continue;
                            }
                        }
                    }



                }


                bool collided = PathFind_IsCollidingInWorldPos(collisionChecker, offsettedWorldPos);

                if (_pathFind_skipStart)
                {
                    if (startChecker.ContainsWorld(checkedWorldPos)) collided = false;
                    else _pathFind_skipStart = false;
                }


                if (collided)
                {
                    checkedPathCell._PathFind_status = -2;
                    continue;
                }
                else
                {

                    if (findDir.ForceChangeDirAfter > 0)
                    {
                        if (originNode._PathFind_CheckIfDirectionsInTheRow(findDir, findDir.ForceChangeDirAfter))
                        {
                            continue;
                        }
                    }

                    if (findDir.DirContinuityRequirement > 0)
                    {
                        if (!originNode._PathFind_CheckPathDirectionContinuity(findDir.DirContinuityRequirement, findDir.Dir))
                        {
                            continue;
                        }
                    }

                    // Origination is checking cell back for the origination > 0 condition
                    if (originNode._PathFind_CheckPathDirectionOrigination(findDir) == false)
                    {
                        continue;
                    }

                    _PathFindValidateNode(startChecker, checkedWorldPos, originNode, checkedPathCell, targetPathEndLocalPos, findParams.directions[i], findParams);
                }
            }

            #endregion


            _pathFind_openListC = _pathFind_openListC.OrderBy(o => o._PathFind_distAndCost).ToList();
        }

        void PathFind_CheckForCheapestDiscarded(CheckerField3D target, CheckerField3D path, Vector3 checkedworldpos, FieldCell originNode, FieldCell checkedPathCell, Vector3Int targetPathEndLocalPos, LineFindHelper direction, PathFindParams parameters)
        {
            if (!target.ContainsWorld(checkedworldpos)) return;

            float cost = PathFind_ComputeStepCost(originNode, checkedPathCell, targetPathEndLocalPos, direction, parameters);

            if (cost < _pathFind_cheapestDiscardedCost)
            {
                //path.DebugLogDrawCellInWorldSpace(checkedPathCell, Color.yellow);
                checkedPathCell = checkedPathCell.Copy();
                checkedPathCell.ParentCell = originNode;
                _pathFind_cheapestNodeDiscardedC = checkedPathCell;
                _pathFind_cheapestDiscardedCost = cost;
            }
        }

        bool PathFind_IsCollidingInWorldPos(List<CheckerField3D> collisionChecker, Vector3 worldPos, CheckerField3D extraCollision1 = null, CheckerField3D extraCollision2 = null)
        {
            bool collision = false;

            for (int c = 0; c < collisionChecker.Count; c++)
            {
                var cCheck = collisionChecker[c];

                FieldCell collisionMaskCell = cCheck.GetCellInWorldPos(worldPos);
                if (FGenerators.CheckIfExist_NOTNULL(collisionMaskCell)) if (collisionMaskCell.InTargetGridArea) collision = true;
                if (collision) break;
            }

            if (!collision)
            {
                if (extraCollision1 != null)
                {
                    FieldCell collisionMaskCell = extraCollision1.GetCellInWorldPos(worldPos);
                    if (FGenerators.CheckIfExist_NOTNULL(collisionMaskCell)) if (collisionMaskCell.InTargetGridArea) return true;
                }

                if (extraCollision2 != null)
                {
                    FieldCell collisionMaskCell = extraCollision2.GetCellInWorldPos(worldPos);
                    if (FGenerators.CheckIfExist_NOTNULL(collisionMaskCell)) if (collisionMaskCell.InTargetGridArea) return true;
                }
            }

            return collision;
        }

        void _PathFindValidateNode(CheckerField3D startChecker, Vector3 targetWorldPos, FieldCell originNode, FieldCell checkedPathCell, Vector3Int targetPathEndLocalPos, LineFindHelper direction, PathFindParams parameters)
        {
            checkedPathCell._PathFind_status = 1;
            checkedPathCell.ParentCell = originNode;

            float stepCost = PathFind_ComputeStepCost(originNode, checkedPathCell, targetPathEndLocalPos, direction, parameters);

            if (parameters.StartOnSide) // Ensureing that cells which would go through inside field are not prioritized
            {
                if (startChecker.ContainsWorld(targetWorldPos)) { stepCost += 1; stepCost *= 3f; }
            }

            //if (parameters.PrioritizeTargetedYLevel)
            //{
            //    int targetY = targetPathEndLocalPos.y;
            //    int preYDiff = Mathf.Abs(targetY - originNode.Pos.y);
            //    int newYDiff = Mathf.Abs(targetY - checkedPathCell.Pos.y);

            //    if (newYDiff < preYDiff) // Getting nearer to the targeted Y level
            //    {
            //        stepCost *= 0.5f;
            //    }
            //}

            //stepCost += originNode._PathFind_movementCost;
            checkedPathCell._PathFind_movementCost = stepCost;

            checkedPathCell._PathFind_CalculateTotalDistance3D_Local(targetPathEndLocalPos, !parameters.SphericalDistanceMeasure);
            checkedPathCell._PathFind_CalculateDistAndCost();
            checkedPathCell._PathFind_LastUsedStep = direction;

            if (checkedPathCell._PathFind_distAndCost < _pathFind_cheapestCost)
            {
                _pathFind_cheapestCost = checkedPathCell._PathFind_distAndCost;
                _pathFind_cheapestNodeC = checkedPathCell;
            }

            //UnityEngine.Debug.Log("Validate node: " + checkedPathCell.Pos + " dir: " + direction.Dir);

            _pathFind_openListC.Add(checkedPathCell);
        }

        float PathFind_ComputeStepCost(FieldCell originNode, FieldCell checkedPathCell, Vector3Int targetPathEndLocalPos, LineFindHelper direction, PathFindParams parameters)
        {
            float stepCost = direction.Cost;

            if (direction.KeepDirectionCost != 0f || direction.ChangeDirectionCost != 0f)
            {
                if (originNode._PathFind_LastUsedStep != null)
                {
                    if (originNode._PathFind_LastUsedStep.Dir != direction.Dir)
                    {
                        stepCost += direction.ChangeDirectionCost;
                    }
                    else
                    {
                        stepCost += direction.KeepDirectionCost;
                        //int same = originNode._PathFind_CountDirectionsInTheRow(direction);
                        //stepCost += direction.KeepDirectionCost * same;
                        //UnityEngine.Debug.Log("stepCost + " + (direction.KeepDirectionCost * same) + " for " + direction.Dir);
                    }
                }
            }

            if (parameters.PrioritizePerpendicular)
            {
                // If closer to the end
                //if (Vector3.Distance(checkedPathCell.Pos, targetPathEndLocalPos) * 0.7f < Vector3.Distance(checkedPathCell.Pos, _GeneratePathFindTowards_FromStartCell.Pos))
                {
                    Vector3 diff = targetPathEndLocalPos.V3IntToV3() - checkedPathCell.Pos.V3IntToV3();
                    diff.Normalize();
                    FDebug.DrawBounds3D(new Bounds(targetPathEndLocalPos * 2, Vector3.one), Color.magenta);
                    if (Mathf.Abs(diff.x) == 1f || Mathf.Abs(diff.z) == 1f) stepCost *= 0.5f;
                }
                //else // If closer to the start
                //{
                //    Vector3 diff = _GeneratePathFindTowards_FromStartCell.Pos.V3IntToV3() - checkedPathCell.Pos.V3IntToV3();
                //    diff.Normalize();

                //    if (Mathf.Abs(diff.x) == 1f || Mathf.Abs(diff.z) == 1f) stepCost *= 0.5f;
                //}
            }

            if (parameters.PrioritizeTargetedYLevel)
            {
                int targetY = targetPathEndLocalPos.y;
                int preYDiff = Mathf.Abs(targetY - originNode.Pos.y);
                int newYDiff = Mathf.Abs(targetY - checkedPathCell.Pos.y);

                if (newYDiff < preYDiff) // Getting nearer to the targeted Y level
                {
                    stepCost *= 0.5f;
                }
            }

            stepCost += originNode._PathFind_movementCost;

            return stepCost;
        }


        public struct PathFindParams
        {
            //public bool StartCentered;
            public bool StartOnSide;


            // Limit params START --------------

            /// <summary> World space limits or grid space limits </summary>
            public bool WorldSpace;
            public float LimitHighestY;
            public float LimitLowestY;
            public float LimitMaxX;
            public float LimitMinX;
            public float LimitMaxZ;
            public float LimitMinZ;

            public bool NoLimits;

            // Limit params END --------------


            public List<LineFindHelper> directions;
            public int KeepDirectionFor;
            public bool PrioritizeTargetedYLevel;

            public int SearchStepIterationLimit;
            public int YLevelSeparation;
            public bool IgnoreSelfCollision;
            public bool DiscardOnNoPathFound;

            public bool LogWarnings;
            public bool ConnectEvenDiscarded;
            public bool DiscardIfAligning;

            public int DontAllowFinishId;
            public float DontAllowFinishDist;
            public float SnapToInstructionsOnDistance;
            public bool DontAllowStartToo;
            public bool TryEndCentered;

            public int End_RequireCellsOnLeftSide;
            public int End_RequireCellsOnRightSide;

            public bool SphericalDistanceMeasure;
            public bool PrioritizePerpendicular;

            /// <summary> x is above, y is below : both positive </summary>
            public Vector2 CollisionYMargins;

            public PathFindParams(List<LineFindHelper> movementDirections, float limitLowYTo = float.MaxValue, bool worldSpace = false)
            {
                //StartCentered = true;
                StartOnSide = true;
                SphericalDistanceMeasure = false;
                PrioritizePerpendicular = false;

                directions = movementDirections;
                WorldSpace = worldSpace;
                LimitHighestY = float.MaxValue;
                LimitLowestY = limitLowYTo;
                LimitMaxX = float.MaxValue;
                LimitMinX = -float.MaxValue;
                LimitMaxZ = float.MaxValue;
                LimitMinZ = -float.MaxValue;
                NoLimits = limitLowYTo == float.MaxValue;
                TryEndCentered = false;

                DontAllowFinishId = -1;
                DontAllowFinishDist = 0f;
                SnapToInstructionsOnDistance = 0f;
                DontAllowStartToo = false;

                LogWarnings = true;

                KeepDirectionFor = 1;
                PrioritizeTargetedYLevel = false;
                YLevelSeparation = 0;
                CollisionYMargins = Vector2.zero;
                SearchStepIterationLimit = 17500;
                IgnoreSelfCollision = true;
                DiscardOnNoPathFound = true;
                ConnectEvenDiscarded = false;
                DiscardIfAligning = false;

                End_RequireCellsOnLeftSide = 0;
                End_RequireCellsOnRightSide = 0;
            }


            public bool IsOutOfLimitsLocalSpace(Vector3Int gridPos)
            {
                if (NoLimits) return false;

                if (gridPos.y < LimitLowestY) return true;
                if (gridPos.y > LimitHighestY) return true;

                if (gridPos.x < LimitMinX) return true;
                if (gridPos.x > LimitMaxX) return true;

                if (gridPos.z < LimitMinZ) return true;
                if (gridPos.z > LimitMaxZ) return true;

                return false;
            }


            public bool IsOutOfLimitsWorldSpace(Vector3 worldPos)
            {
                if (NoLimits) return false;

                if (worldPos.y < LimitLowestY) return true;
                if (worldPos.y > LimitHighestY) return true;

                if (worldPos.x < LimitMinX) return true;
                if (worldPos.x > LimitMaxX) return true;

                if (worldPos.z < LimitMinZ) return true;
                if (worldPos.z > LimitMaxZ) return true;

                return false;
            }


            public void ResetBeforePathFind()
            {
                if (directions == null) return;

                for (int i = 0; i < directions.Count; i++)
                {
                    LineFindHelper dir = directions[i];
                    dir.Reset();
                    directions[i] = dir;
                }
            }

        }


    }
}