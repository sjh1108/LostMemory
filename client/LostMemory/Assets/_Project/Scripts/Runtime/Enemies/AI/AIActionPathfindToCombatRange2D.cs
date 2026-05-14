using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.AI
{
    [AddComponentMenu("Lost Memory/Enemies/AI/AI Action Pathfind To Combat Range 2D")]
    public sealed class AIActionPathfindToCombatRange2D : AIAction
    {
        [Header("References")]
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private AIActionCombatApproach2D combatApproach;
        [SerializeField] private Collider2D bodyCollider;

        [Header("Combat Range")]
        [SerializeField, Min(0f)] private float preferredDistance = 1f;
        [SerializeField, Min(0f)] private float distanceTolerance = 0.15f;
        [SerializeField, Min(0f)] private float combatApproachDistance = 1.6f;

        [Header("Pathfinding")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField, Min(0.1f)] private float cellSize = 0.35f;
        [SerializeField, Min(0.05f)] private float waypointReachedDistance = 0.2f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.35f;
        [SerializeField, Min(0.05f)] private float targetRepathDistance = 0.45f;
        [SerializeField, Min(8)] private int maxVisitedNodes = 700;
        [SerializeField, Min(1f)] private float maxPathDistance = 12f;
        [SerializeField, Range(1, 8)] private int goalSearchRings = 4;
        [SerializeField] private bool allowDiagonalMovement = true;

        [Header("Clearance")]
        [SerializeField] private bool useBodyColliderClearance = true;
        [SerializeField, Min(0f)] private float obstacleClearancePadding = 0.08f;
        [SerializeField, Min(0.01f)] private float fallbackClearanceRadius = 0.25f;

        private const float MinDirectionSqrMagnitude = 0.0001f;

        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private static readonly Vector2Int[] DiagonalDirections =
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        private readonly List<Vector2> _path = new List<Vector2>();
        private readonly List<PathNode> _openNodes = new List<PathNode>();
        private readonly Dictionary<Vector2Int, PathNode> _nodes = new Dictionary<Vector2Int, PathNode>();
        private readonly HashSet<Vector2Int> _closedCells = new HashSet<Vector2Int>();
        private readonly Collider2D[] _overlapResults = new Collider2D[12];

        private int _pathIndex;
        private float _lastRepathTime = -float.MaxValue;
        private Vector2 _lastPathTarget;

        protected override void Awake()
        {
            base.Awake();
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(0.1f, cellSize);
            waypointReachedDistance = Mathf.Max(0.05f, waypointReachedDistance);
            repathInterval = Mathf.Max(0.05f, repathInterval);
            targetRepathDistance = Mathf.Max(0.05f, targetRepathDistance);
            maxVisitedNodes = Mathf.Max(8, maxVisitedNodes);
            maxPathDistance = Mathf.Max(1f, maxPathDistance);
            goalSearchRings = Mathf.Clamp(goalSearchRings, 1, 8);
            obstacleClearancePadding = Mathf.Max(0f, obstacleClearancePadding);
            fallbackClearanceRadius = Mathf.Max(0.01f, fallbackClearanceRadius);
            AutoAssignReferences();
        }

        public override void Initialization()
        {
            if (!ShouldInitialize)
            {
                return;
            }

            base.Initialization();
            AutoAssignReferences();
            combatApproach?.Initialization();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            ClearPath();
            combatApproach?.OnEnterState();
        }

        public override void PerformAction()
        {
            Move();
        }

        public override void OnExitState()
        {
            base.OnExitState();
            combatApproach?.OnExitState();
            ClearPath();
            StopMovement();
        }

        public void Configure(
            CharacterMovement configuredCharacterMovement,
            AIActionCombatApproach2D configuredCombatApproach,
            LayerMask configuredObstacleMask,
            float configuredPreferredDistance,
            float configuredDistanceTolerance)
        {
            characterMovement = configuredCharacterMovement;
            combatApproach = configuredCombatApproach;
            obstacleMask = configuredObstacleMask;
            preferredDistance = Mathf.Max(0f, configuredPreferredDistance);
            distanceTolerance = Mathf.Max(0f, configuredDistanceTolerance);
            combatApproachDistance = Mathf.Max(0.35f, preferredDistance + distanceTolerance + 0.35f);
        }

        private void Move()
        {
            if (_brain == null || _brain.Target == null || characterMovement == null)
            {
                StopMovement();
                return;
            }

            Vector2 origin = ResolveBodyCenter();
            Vector2 targetPosition = _brain.Target.position;
            float targetDistance = Vector2.Distance(origin, targetPosition);

            if (targetDistance <= combatApproachDistance && HasClearPath(origin, targetPosition))
            {
                ClearPath();
                if (combatApproach != null)
                {
                    combatApproach.PerformAction();
                }
                else
                {
                    StopMovement();
                }

                return;
            }

            Vector2 combatDestination = ResolveCombatDestination(origin, targetPosition);
            if (HasClearPath(origin, combatDestination))
            {
                ClearPath();
                MoveTowards(combatDestination, origin);
                return;
            }

            if (ShouldRepath(combatDestination))
            {
                _lastRepathTime = Time.time;
                _lastPathTarget = combatDestination;
                TryBuildPath(origin, combatDestination);
            }

            if (FollowCurrentPath(origin))
            {
                return;
            }

            if (combatApproach != null)
            {
                combatApproach.PerformAction();
                return;
            }

            MoveTowards(combatDestination, origin);
        }

        private void AutoAssignReferences()
        {
            characterMovement ??= gameObject.GetComponentInParent<Character>()?.FindAbility<CharacterMovement>();
            combatApproach ??= GetComponent<AIActionCombatApproach2D>();
            bodyCollider ??= ResolveBodyCollider();
        }

        private Vector2 ResolveCombatDestination(Vector2 origin, Vector2 targetPosition)
        {
            Vector2 fromTargetToOrigin = origin - targetPosition;
            Vector2 direction = fromTargetToOrigin.sqrMagnitude > MinDirectionSqrMagnitude
                ? fromTargetToOrigin.normalized
                : Vector2.right;

            Vector2 preferred = targetPosition + direction * Mathf.Max(0.05f, preferredDistance);
            if (IsWalkableWorldPosition(preferred))
            {
                return preferred;
            }

            float bestScore = float.PositiveInfinity;
            Vector2 bestDestination = preferred;
            int candidateCount = 16;
            float radius = Mathf.Max(0.05f, preferredDistance);

            for (int i = 0; i < candidateCount; i++)
            {
                float angle = 360f / candidateCount * i;
                Vector2 candidateDirection = Rotate(direction, angle);
                Vector2 candidate = targetPosition + candidateDirection * radius;
                if (!IsWalkableWorldPosition(candidate))
                {
                    continue;
                }

                float score = Vector2.Distance(origin, candidate);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDestination = candidate;
            }

            return bestDestination;
        }

        private bool ShouldRepath(Vector2 combatDestination)
        {
            bool targetMovedEnough = Vector2.Distance(_lastPathTarget, combatDestination) >= targetRepathDistance;
            if (Time.time - _lastRepathTime < repathInterval && !targetMovedEnough)
            {
                return false;
            }

            if (_path.Count == 0)
            {
                return true;
            }

            return targetMovedEnough;
        }

        private bool FollowCurrentPath(Vector2 origin)
        {
            if (_path.Count == 0 || _pathIndex >= _path.Count)
            {
                return false;
            }

            while (_pathIndex < _path.Count - 1
                   && Vector2.Distance(origin, _path[_pathIndex]) <= waypointReachedDistance)
            {
                _pathIndex++;
            }

            Vector2 waypoint = _path[_pathIndex];
            if (Vector2.Distance(origin, waypoint) <= waypointReachedDistance)
            {
                return false;
            }

            MoveTowards(waypoint, origin);
            return true;
        }

        private void MoveTowards(Vector2 destination, Vector2 origin)
        {
            Vector2 movement = destination - origin;
            if (movement.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                StopMovement();
                return;
            }

            characterMovement.SetMovement(movement.normalized);
        }

        private bool TryBuildPath(Vector2 start, Vector2 goal)
        {
            ClearPath();
            _openNodes.Clear();
            _nodes.Clear();
            _closedCells.Clear();

            Vector2Int startCell = WorldToCell(start);
            Vector2Int goalCell = WorldToCell(goal);

            if (!TryResolveWalkableGoalCell(goalCell, start, out goalCell))
            {
                return false;
            }

            PathNode startNode = GetOrCreateNode(startCell);
            startNode.G = 0f;
            startNode.H = Heuristic(startCell, goalCell);
            _openNodes.Add(startNode);

            int visitedNodes = 0;
            while (_openNodes.Count > 0 && visitedNodes < maxVisitedNodes)
            {
                PathNode current = PopBestOpenNode();
                if (_closedCells.Contains(current.Cell))
                {
                    continue;
                }

                visitedNodes++;
                if (current.Cell == goalCell)
                {
                    ReconstructPath(current);
                    return _path.Count > 0;
                }

                _closedCells.Add(current.Cell);
                EvaluateNeighbors(current, goalCell, start);
            }

            return false;
        }

        private void EvaluateNeighbors(PathNode current, Vector2Int goalCell, Vector2 start)
        {
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                EvaluateNeighbor(current, goalCell, start, CardinalDirections[i], 1f);
            }

            if (!allowDiagonalMovement)
            {
                return;
            }

            for (int i = 0; i < DiagonalDirections.Length; i++)
            {
                Vector2Int direction = DiagonalDirections[i];
                if (!IsWalkableCell(current.Cell + new Vector2Int(direction.x, 0))
                    || !IsWalkableCell(current.Cell + new Vector2Int(0, direction.y)))
                {
                    continue;
                }

                EvaluateNeighbor(current, goalCell, start, direction, 1.4142135f);
            }
        }

        private void EvaluateNeighbor(
            PathNode current,
            Vector2Int goalCell,
            Vector2 start,
            Vector2Int direction,
            float movementCost)
        {
            Vector2Int neighborCell = current.Cell + direction;
            if (_closedCells.Contains(neighborCell))
            {
                return;
            }

            if (Vector2.Distance(start, CellToWorld(neighborCell)) > maxPathDistance)
            {
                return;
            }

            if (!IsWalkableCell(neighborCell))
            {
                return;
            }

            PathNode neighbor = GetOrCreateNode(neighborCell);
            float tentativeG = current.G + movementCost;
            if (_openNodes.Contains(neighbor) && tentativeG >= neighbor.G)
            {
                return;
            }

            neighbor.Parent = current;
            neighbor.G = tentativeG;
            neighbor.H = Heuristic(neighborCell, goalCell);

            if (!_openNodes.Contains(neighbor))
            {
                _openNodes.Add(neighbor);
            }
        }

        private bool TryResolveWalkableGoalCell(Vector2Int requestedGoalCell, Vector2 start, out Vector2Int resolvedGoalCell)
        {
            if (IsWalkableCell(requestedGoalCell))
            {
                resolvedGoalCell = requestedGoalCell;
                return true;
            }

            float bestDistance = float.PositiveInfinity;
            resolvedGoalCell = requestedGoalCell;

            for (int radius = 1; radius <= goalSearchRings; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = requestedGoalCell + new Vector2Int(x, y);
                        if (!IsWalkableCell(candidate))
                        {
                            continue;
                        }

                        float distance = Vector2.Distance(start, CellToWorld(candidate));
                        if (distance >= bestDistance)
                        {
                            continue;
                        }

                        bestDistance = distance;
                        resolvedGoalCell = candidate;
                    }
                }

                if (bestDistance < float.PositiveInfinity)
                {
                    return true;
                }
            }

            return false;
        }

        private PathNode PopBestOpenNode()
        {
            int bestIndex = 0;
            float bestScore = _openNodes[0].F;

            for (int i = 1; i < _openNodes.Count; i++)
            {
                float score = _openNodes[i].F;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIndex = i;
            }

            PathNode best = _openNodes[bestIndex];
            _openNodes.RemoveAt(bestIndex);
            return best;
        }

        private PathNode GetOrCreateNode(Vector2Int cell)
        {
            if (_nodes.TryGetValue(cell, out PathNode node))
            {
                return node;
            }

            node = new PathNode(cell);
            _nodes.Add(cell, node);
            return node;
        }

        private void ReconstructPath(PathNode goalNode)
        {
            PathNode current = goalNode;
            while (current != null)
            {
                _path.Add(CellToWorld(current.Cell));
                current = current.Parent;
            }

            _path.Reverse();
            _pathIndex = _path.Count > 1 ? 1 : 0;
        }

        private bool HasClearPath(Vector2 origin, Vector2 destination)
        {
            if (obstacleMask.value == 0)
            {
                return true;
            }

            Vector2 delta = destination - origin;
            float distance = delta.magnitude;
            if (distance <= waypointReachedDistance)
            {
                return true;
            }

            RaycastHit2D hit = Physics2D.CircleCast(
                origin,
                ResolveClearanceRadius(),
                delta / distance,
                distance,
                obstacleMask);

            return hit.collider == null;
        }

        private bool IsWalkableCell(Vector2Int cell)
        {
            return IsWalkableWorldPosition(CellToWorld(cell));
        }

        private bool IsWalkableWorldPosition(Vector2 position)
        {
            if (obstacleMask.value == 0)
            {
                return true;
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleMask);
            filter.useTriggers = false;

            int hitCount = Physics2D.OverlapCircle(position, ResolveClearanceRadius(), filter, _overlapResults);
            return hitCount == 0;
        }

        private Vector2Int WorldToCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.RoundToInt(position.x / cellSize),
                Mathf.RoundToInt(position.y / cellSize));
        }

        private Vector2 CellToWorld(Vector2Int cell)
        {
            return new Vector2(cell.x * cellSize, cell.y * cellSize);
        }

        private static float Heuristic(Vector2Int from, Vector2Int to)
        {
            return Vector2Int.Distance(from, to);
        }

        private Vector2 ResolveBodyCenter()
        {
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        }

        private float ResolveClearanceRadius()
        {
            if (!useBodyColliderClearance || bodyCollider == null)
            {
                return fallbackClearanceRadius;
            }

            Bounds bounds = bodyCollider.bounds;
            float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y) + obstacleClearancePadding;
            return Mathf.Max(fallbackClearanceRadius, bodyRadius);
        }

        private Collider2D ResolveBodyCollider()
        {
            Collider2D[] colliders = GetComponentsInParent<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                {
                    return candidate;
                }
            }

            colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ClearPath()
        {
            _path.Clear();
            _pathIndex = 0;
        }

        private void StopMovement()
        {
            characterMovement?.SetMovement(Vector2.zero);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private sealed class PathNode
        {
            public PathNode(Vector2Int cell)
            {
                Cell = cell;
                G = float.PositiveInfinity;
            }

            public Vector2Int Cell { get; }
            public float G { get; set; }
            public float H { get; set; }
            public float F => G + H;
            public PathNode Parent { get; set; }
        }
    }
}
