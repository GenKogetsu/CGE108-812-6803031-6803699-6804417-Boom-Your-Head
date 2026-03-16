using Genoverrei.DesignPattern;
using Genoverrei.Libary;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;

public sealed class BotCommander : MonoBehaviour, IPathfindable, IPauseWhenSceneAnimation
{
    [Header("Observer & Data")]
    [SerializeField] private MapChannelSO _mapChannel;
    [SerializeField] private GameSessionDataSO _sessionData;
    [SerializeField] private CharacterRegistrySO _characterRegistry;
    [SerializeField] private BotInputChannelSO _botInputChannel;

    [Header("AI Configs")]
    [SerializeField] private float _thinkInterval = 0.12f;

    [Range(1f, 10f)]
    [SerializeField] private float _thinkDelayOffset = 1.0f;

    [Header("Radar Layers")]
    [SerializeField] private int _itemSearchRadius = 8;
    [SerializeField] private LayerMask _itemLayer;
    [SerializeField] private LayerMask _bombLayer;

    [Header("Identity (Multi-Drive)")]
    [ReadOnly][SerializeField] private List<Character> _targetsToDrive = new List<Character>();

    private Dictionary<Character, StatsController> _controlledStatsMap = new Dictionary<Character, StatsController>();
    private Dictionary<Character, BotBrainState> _botBrains = new Dictionary<Character, BotBrainState>();
    private Dictionary<Character, float> _unreachableTargetTimer = new Dictionary<Character, float>();
    private Dictionary<Character, Vector2> _lastSentMove = new Dictionary<Character, Vector2>();
    private Dictionary<Character, Vector2Int> _lastNextStep = new Dictionary<Character, Vector2Int>();
    private Dictionary<Character, Vector2Int> _prevGridPos = new Dictionary<Character, Vector2Int>();
    private Dictionary<Character, int> _stuckCount = new Dictionary<Character, int>();
    private Dictionary<Character, float> _replanCooldown = new Dictionary<Character, float>();

    private float _nextThinkTime;
    private float _nextReflexTime;
    private const float UNREACHABLE_TIMEOUT = 2.0f;
    private const int STUCK_LIMIT = 45;
    private const float REPLAN_COOLDOWN_SECONDS = 1.0f;

    public Vector2Int CurrentGridPosition => Vector2Int.zero;
    public IMapProvider MapProvider => null;
    Vector2Int IPathfindable.GetNextPath(Vector2Int target) => PathfindAbility<BotCommander>.Execute(this, target);

    private bool _isSceneLoading = true;

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<LoadSceneEvent>(OnSceneLoading);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<LoadSceneEvent>(OnSceneLoading);
        }
    }

    private void FixedUpdate()
    {
        if (_isSceneLoading) return;

        var keys = _replanCooldown.Keys.ToList();
        foreach (var k in keys)
        {
            if (_replanCooldown[k] > 0f) _replanCooldown[k] -= Time.fixedDeltaTime;
        }

        if (_controlledStatsMap.Count == 0 || _controlledStatsMap.Any(kvp => kvp.Value == null))
        {
            ExecuteRefreshTarget();
            return;
        }

        if (Time.fixedTime >= _nextReflexTime)
        {
            _nextReflexTime = Time.fixedTime + _thinkInterval;
            ExecuteReflexes();
        }

        if (Time.fixedTime >= _nextThinkTime)
        {
            _nextThinkTime = Time.fixedTime + (_thinkInterval * _thinkDelayOffset);
            ExecutePlanning();
        }

        ExecuteMovement();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        foreach (var kvp in _controlledStatsMap)
        {
            if (kvp.Value == null || !_botBrains.ContainsKey(kvp.Key)) continue;
            var brain = _botBrains[kvp.Key];
            Gizmos.color = brain.Behavior == BotFullStateMachine.Fleeing ? UnityEngine.Color.cyan : UnityEngine.Color.yellow;
            Vector2 start = (Vector2)WorldToGrid(kvp.Value.transform.position);
            if (brain.CurrentFullPath != null)
            {
                foreach (var node in brain.CurrentFullPath)
                {
                    Gizmos.DrawLine(start, (Vector2)node);
                    start = (Vector2)node;
                }
            }
        }
    }

    private Vector2Int WorldToGrid(Vector2 worldPos) => new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));

    public void OnSceneLoading(LoadSceneEvent eventData)
    {
        _isSceneLoading = eventData.Isloding;
    }

    private void ExecuteReflexes()
    {
        foreach (var botId in _targetsToDrive)
        {
            if (!_controlledStatsMap.TryGetValue(botId, out var stats) || stats == null || stats.CurrentHp <= 0) continue;
            if (!_botBrains.ContainsKey(botId)) _botBrains[botId] = new BotBrainState();

            var brain = _botBrains[botId];
            Vector2Int myGridPos = WorldToGrid(stats.transform.position);
            int safeRadius = stats.CurrentExplosionRange + 1;

            if (_mapChannel.IsDangerous(myGridPos) || IsThreatenedByBomb(myGridPos, safeRadius))
            {
                brain.Behavior = BotFullStateMachine.Fleeing;
                if (brain.SafeTarget.x == -9999 || _mapChannel.IsDangerous(brain.SafeTarget) || IsThreatenedByBomb(brain.SafeTarget, safeRadius) || myGridPos == brain.SafeTarget)
                    brain.SafeTarget = ExecuteGetSafeSpot(myGridPos, safeRadius);

                if (brain.SafeTarget.x != -9999) brain.CurrentFullPath = GetEscapePath(myGridPos, brain.SafeTarget);
                brain.TargetItemPos = new Vector2Int(-9999, -9999);
                if (!_unreachableTargetTimer.ContainsKey(botId)) _unreachableTargetTimer[botId] = 0;
            }
            else if (brain.Behavior == BotFullStateMachine.Fleeing)
            {
                brain.Behavior = BotFullStateMachine.Patrolling;
                brain.SafeTarget = new Vector2Int(-9999, -9999);
            }
        }
    }

    private void ExecutePlanning()
    {
        if (_sessionData == null) return;

        foreach (var botId in _targetsToDrive)
        {
            if (!_controlledStatsMap.TryGetValue(botId, out var stats) || stats == null || stats.CurrentHp <= 0) continue;
            var brain = _botBrains[botId];
            if (brain.Behavior == BotFullStateMachine.Fleeing) continue;

            Vector2Int myGridPos = WorldToGrid(stats.transform.position);
            int safeRadius = stats.CurrentExplosionRange + 1;

            while (brain.CurrentFullPath.Count > 0 && brain.CurrentFullPath[0] == myGridPos)
            {
                brain.CurrentFullPath.RemoveAt(0);
            }

            if (_replanCooldown.TryGetValue(botId, out var cd) && cd > 0f)
                continue;

            if (brain.CurrentFullPath.Count > 0)
            {
                Vector2Int pathEnd = brain.CurrentFullPath[brain.CurrentFullPath.Count - 1];

                if (brain.TargetItemPos.x != -9999 && pathEnd == brain.TargetItemPos && CheckHasItemAt(brain.TargetItemPos))
                {
                    continue;
                }
            }

            if (brain.TargetItemPos.x == -9999 || !CheckHasItemAt(brain.TargetItemPos))
                brain.TargetItemPos = ExecuteFindNearestObject(myGridPos, _itemLayer, _itemSearchRadius);

            if (brain.TargetItemPos.x != -9999)
            {
                if (brain.CurrentFullPath.Count == 0 || brain.CurrentFullPath[brain.CurrentFullPath.Count - 1] != brain.TargetItemPos)
                {
                    brain.CurrentFullPath = GetFullPath(myGridPos, brain.TargetItemPos, false, safeRadius);
                }

                if (brain.CurrentFullPath.Count == 0)
                {
                    brain.TargetItemPos = new Vector2Int(-9999, -9999);
                    _replanCooldown[botId] = REPLAN_COOLDOWN_SECONDS;
                }
                continue;
            }

            Vector2Int nearestEnemyPos = new Vector2Int(-9999, -9999);
            float minDistToEnemy = float.MaxValue;

            foreach (var other in _controlledStatsMap)
            {
                if (other.Key == botId || other.Value == null || other.Value.CurrentHp <= 0) continue;
                Vector2Int otherPos = WorldToGrid(other.Value.transform.position);
                float dist = Vector2.Distance(myGridPos, otherPos);
                if (dist < minDistToEnemy)
                {
                    minDistToEnemy = dist;
                    nearestEnemyPos = otherPos;
                }
            }

            Vector2Int nearestBox = ExecuteFindNearestBox(myGridPos, 30);
            Vector2Int targetPos = nearestBox;

            if (nearestEnemyPos.x != -9999)
            {
                if (nearestBox.x == -9999 || minDistToEnemy < Vector2Int.Distance(myGridPos, nearestBox))
                {
                    targetPos = nearestEnemyPos;
                }
            }

            brain.CurrentFullPath = GetFullPath(myGridPos, targetPos.x != -9999 ? targetPos : myGridPos, true, safeRadius);

            bool isNearEnemy = minDistToEnemy <= 1.5f;
            bool isPathBlocked = brain.CurrentFullPath.Count > 0 && !_mapChannel.IsWalkable(brain.CurrentFullPath[0]);

            if (isNearEnemy || isPathBlocked)
            {
                if (isNearEnemy || Vector2Int.Distance(myGridPos, brain.CurrentFullPath[0]) <= 1.1f)
                {
                    ExecuteRequestBomb(botId, stats);
                    brain.CurrentFullPath.Clear();
                    _nextReflexTime = 0f;
                }
                else
                {
                    brain.CurrentFullPath.Clear();
                }
            }
        }
    }

    private void ExecuteMovement()
    {
        const float snapThreshold = 0.12f;
        foreach (var botId in _targetsToDrive)
        {
            if (!_controlledStatsMap.TryGetValue(botId, out var stats) || stats == null || stats.CurrentHp <= 0) continue;
            var brain = _botBrains[botId];

            if (brain.CurrentFullPath == null || brain.CurrentFullPath.Count == 0)
            {
                if (!_lastSentMove.ContainsKey(botId) || _lastSentMove[botId] != Vector2.zero)
                {
                    _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(Vector2.zero));
                    _lastSentMove[botId] = Vector2.zero;
                }
                continue;
            }

            Vector2Int myGridPos = WorldToGrid(stats.transform.position);
            if (brain.CurrentFullPath.Count > 0 && brain.CurrentFullPath[0] == myGridPos)
            {
                brain.CurrentFullPath.RemoveAt(0);
                if (brain.CurrentFullPath.Count == 0)
                {
                    if (!_lastSentMove.ContainsKey(botId) || _lastSentMove[botId] != Vector2.zero)
                    {
                        _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(Vector2.zero));
                        _lastSentMove[botId] = Vector2.zero;
                    }
                    continue;
                }
            }

            if (brain.CurrentFullPath == null || brain.CurrentFullPath.Count == 0) continue;

            Vector2Int nextStep = brain.CurrentFullPath[0];
            Vector2 dir = (Vector2)nextStep - (Vector2)stats.transform.position;

            if (dir.magnitude < snapThreshold)
            {
                stats.transform.position = (Vector2)nextStep;
                brain.CurrentFullPath.RemoveAt(0);

                if (brain.CurrentFullPath.Count == 0)
                {
                    if (!_lastSentMove.ContainsKey(botId) || _lastSentMove[botId] != Vector2.zero)
                    {
                        _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(Vector2.zero));
                        _lastSentMove[botId] = Vector2.zero;
                    }
                    continue;
                }
                else
                {
                    nextStep = brain.CurrentFullPath[0];
                    dir = (Vector2)nextStep - (Vector2)stats.transform.position;
                }
            }

            if (!_lastNextStep.ContainsKey(botId) || _lastNextStep[botId] != nextStep)
            {
                _lastNextStep[botId] = nextStep;
                _stuckCount[botId] = 0;
            }
            else
            {
                if (!_prevGridPos.ContainsKey(botId) || _prevGridPos[botId] == myGridPos)
                {
                    _stuckCount[botId] = (_stuckCount.ContainsKey(botId) ? _stuckCount[botId] : 0) + 1;
                }
                else
                {
                    _stuckCount[botId] = 0;
                }
            }
            _prevGridPos[botId] = myGridPos;

            if (_stuckCount.ContainsKey(botId) && _stuckCount[botId] >= STUCK_LIMIT)
            {
                brain.CurrentFullPath.Clear();
                _replanCooldown[botId] = REPLAN_COOLDOWN_SECONDS;
                _stuckCount[botId] = 0;
                if (!_lastSentMove.ContainsKey(botId) || _lastSentMove[botId] != Vector2.zero)
                {
                    _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(Vector2.zero));
                    _lastSentMove[botId] = Vector2.zero;
                }
                continue;
            }

            Vector2 finalDir = Vector2.zero;
            float absX = Mathf.Abs(dir.x);
            float absY = Mathf.Abs(dir.y);
            const float HYSTERESIS = 0.1f;
            if (absX > absY + HYSTERESIS)
                finalDir.x = Mathf.Sign(dir.x);
            else if (absY > absX + HYSTERESIS)
                finalDir.y = Mathf.Sign(dir.y);
            else
            {
                if (absX >= absY) finalDir.x = Mathf.Sign(dir.x);
                else finalDir.y = Mathf.Sign(dir.y);
            }

            Vector2 finalDirNormalized = finalDir.normalized;
            var moveController = stats.GetComponent<MoveController>();
            bool isMoving = moveController != null && moveController.IsMoving;

            if (!_lastSentMove.ContainsKey(botId)) _lastSentMove[botId] = Vector2.zero;
            if (isMoving && _lastSentMove[botId] == finalDirNormalized) continue;

            _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(finalDirNormalized));
            _lastSentMove[botId] = finalDirNormalized;
        }
    }

    private void ExecuteRequestMove(Character botId, StatsController stats, Vector2Int targetGrid)
    {
        Vector2 dir = (Vector2)targetGrid - (Vector2)stats.transform.position;
        Vector2 finalDir = Vector2.zero;

        if (Mathf.Abs(dir.x) > 0.1f && Mathf.Abs(dir.y) > 0.1f)
        {
            if (Mathf.Abs(dir.x) < Mathf.Abs(dir.y)) finalDir.x = Mathf.Sign(dir.x);
            else finalDir.y = Mathf.Sign(dir.y);
        }
        else
        {
            if (Mathf.Abs(dir.x) > 0.1f) finalDir.x = Mathf.Sign(dir.x);
            else if (Mathf.Abs(dir.y) > 0.1f) finalDir.y = Mathf.Sign(dir.y);
        }
        _botInputChannel.RaiseEvent(botId, ActionType.Move, new MoveInputEvent(finalDir.normalized));
    }

    private void ExecuteRefreshTarget()
    {
        if (_sessionData == null || _characterRegistry == null) return;

        _controlledStatsMap.Clear();
        _targetsToDrive.Clear();
        _unreachableTargetTimer.Clear();
        _lastSentMove.Clear();
        _lastNextStep.Clear();
        _prevGridPos.Clear();
        _stuckCount.Clear();
        _replanCooldown.Clear();

        foreach (var botId in _sessionData.SelectedBots)
        {
            var s = _characterRegistry.GetStats(botId);
            if (s != null && !_controlledStatsMap.ContainsKey(botId))
            {
                _controlledStatsMap.Add(botId, s);
                _targetsToDrive.Add(botId);
                _lastSentMove[botId] = Vector2.zero;
                _lastNextStep[botId] = new Vector2Int(-9999, -9999);
                _prevGridPos[botId] = new Vector2Int(-9999, -9999);
                _stuckCount[botId] = 0;
                _replanCooldown[botId] = 0f;
            }
        }
    }

    private bool IsThreatenedByBomb(Vector2Int pos, int radius)
    {
        if (CheckHasBombAt(pos)) return true;
        foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            for (int i = 1; i <= radius; i++)
            {
                Vector2Int next = pos + d * i;
                if (_mapChannel.IsSolid(next)) break;
                if (CheckHasBombAt(next)) return true;
                if (_mapChannel.IsDestructible(next)) break;
            }
        }
        return false;
    }

    private bool CheckHasBombAt(Vector2Int pos) => Physics2D.OverlapCircle((Vector2)pos, 0.3f, _bombLayer) != null;
    private bool CheckHasItemAt(Vector2Int pos) => Physics2D.OverlapCircle((Vector2)pos, 0.3f, _itemLayer) != null;

    private Vector2Int ExecuteFindNearestObject(Vector2Int start, LayerMask layer, float radius)
    {
        Collider2D[] objects = Physics2D.OverlapCircleAll((Vector2)start, radius, layer);
        Vector2Int bestPos = new Vector2Int(-9999, -9999);
        float minDist = float.MaxValue;
        foreach (var obj in objects)
        {
            Vector2Int gridPos = WorldToGrid(obj.transform.position);
            float dist = Vector2Int.Distance(start, gridPos);
            if (dist < minDist) { minDist = dist; bestPos = gridPos; }
        }
        return bestPos;
    }

    private Vector2Int ExecuteFindNearestBox(Vector2Int start, int maxRadius)
    {
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    Vector2Int pos = start + new Vector2Int(x, y);
                    if (_mapChannel.IsDestructible(pos)) return pos;
                }
            }
        }
        return new Vector2Int(-9999, -9999);
    }

    private void ExecuteRequestBomb(Character botId, StatsController stats)
    {
        if (stats.BombsRemaining <= 0) return;
        stats.transform.position = (Vector2)WorldToGrid(stats.transform.position);
        _botInputChannel.RaiseEvent(botId, ActionType.PlaceBomb, null);
    }

    private List<Vector2Int> GetFullPath(Vector2Int start, Vector2Int target, bool ignoreBoxes, int safeRadius)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        q.Enqueue(start); cameFrom[start] = start;
        int limit = 200;
        while (q.Count > 0 && limit-- > 0)
        {
            Vector2Int curr = q.Dequeue();
            if (curr == target) break;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int next = curr + d;
                if (!cameFrom.ContainsKey(next) && !_mapChannel.IsSolid(next))
                {
                    if (!ignoreBoxes && (_mapChannel.IsDangerous(next) || IsThreatenedByBomb(next, safeRadius))) continue;
                    if (ignoreBoxes || _mapChannel.IsWalkable(next)) { cameFrom[next] = curr; q.Enqueue(next); }
                }
            }
        }
        if (cameFrom.ContainsKey(target)) { Vector2Int curr = target; while (curr != start) { path.Add(curr); curr = cameFrom[curr]; } path.Reverse(); }
        return path;
    }

    private List<Vector2Int> GetEscapePath(Vector2Int start, Vector2Int target) => GetFullPath(start, target, false, 0);

    private Vector2Int ExecuteGetSafeSpot(Vector2Int origin, int safeRadius)
    {
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        HashSet<Vector2Int> v = new HashSet<Vector2Int>();
        q.Enqueue(origin); v.Add(origin);
        while (q.Count > 0)
        {
            Vector2Int curr = q.Dequeue();
            if (!_mapChannel.IsDangerous(curr) && !IsThreatenedByBomb(curr, safeRadius) && _mapChannel.IsWalkable(curr)) return curr;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int next = curr + d;
                if (!v.Contains(next) && !_mapChannel.IsSolid(next) && _mapChannel.IsWalkable(next)) { v.Add(next); q.Enqueue(next); }
            }
        }
        return new Vector2Int(-9999, -9999);
    }
}