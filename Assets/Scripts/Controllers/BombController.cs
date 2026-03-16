using System;
using UnityEngine;
using NaughtyAttributes;
using Genoverrei.DesignPattern;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Animator))]
public sealed class BombController : MonoBehaviour
{
    [Header("Observer")]
    [SerializeField] private BombChannelSO _bombChannel;
    [SerializeField] private MapChannelSO _mapChannel;
    [SerializeField] private GameObject _poolKey;

    [Header("Components")]
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private CircleCollider2D _collider;
    [SerializeField] private Animator _animator;

    [Header("Clips Reference")]
    [SerializeField] private AnimationClip _noncriticalClip;
    [SerializeField] private AnimationClip _criticalClip;

    [ReadOnly][SerializeField] private Vector2Int _gridPosition;
    [ReadOnly][SerializeField] private int _radius;
    [ReadOnly][SerializeField] private float _lifeTime;
    [ReadOnly][SerializeField] private bool _isExploded;

    private StatsController _ownerStats;
    private bool _onCriticalPhase;

    private bool _isSmartSnapping;
    private Vector2 _targetSnapPos;

    private void Awake()
    {
        _rigidbody.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
        _rigidbody.mass = 0.0001f;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Update()
    {
        if (_isExploded) return;
        _lifeTime += Time.deltaTime;

        float totalTime = _noncriticalClip.length + _criticalClip.length;
        float timeLeft = totalTime - _lifeTime;

        if (timeLeft <= 0)
        {
            ExecuteSnapToGrid();
            ForceExplode();
            return;
        }

        if (!_onCriticalPhase && _lifeTime >= _noncriticalClip.length)
        {
            _animator.SetTrigger("ToCritical");
            _onCriticalPhase = true;
            CalculateSmartSnapTarget(timeLeft);
        }

        if (_isSmartSnapping && timeLeft > 0)
        {
            _rigidbody.linearVelocity = (_targetSnapPos - (Vector2)transform.position) / timeLeft;
        }
        else if (_rigidbody.linearVelocity.sqrMagnitude > 0.01f)
        {
            Vector2 currentPos = transform.position;
            Vector2 vel = _rigidbody.linearVelocity;
            if (Mathf.Abs(vel.x) > Mathf.Abs(vel.y))
                currentPos.y = Mathf.Lerp(currentPos.y, Mathf.Round(currentPos.y), Time.deltaTime * 10f);
            else
                currentPos.x = Mathf.Lerp(currentPos.x, Mathf.Round(currentPos.x), Time.deltaTime * 10f);

            transform.position = currentPos;
        }
    }

    private void CalculateSmartSnapTarget(float timeLeft)
    {
        Vector2 vel = _rigidbody.linearVelocity;
        if (vel.sqrMagnitude < 0.01f) return;

        Vector2 current = transform.position;
        Vector2 expected = current + (vel * timeLeft);

        int targetX = Mathf.RoundToInt(expected.x);
        int targetY = Mathf.RoundToInt(expected.y);

        if (vel.x > 0.1f && targetX < current.x) targetX = Mathf.CeilToInt(current.x);
        if (vel.x < -0.1f && targetX > current.x) targetX = Mathf.FloorToInt(current.x);
        if (vel.y > 0.1f && targetY < current.y) targetY = Mathf.CeilToInt(current.y);
        if (vel.y < -0.1f && targetY > current.y) targetY = Mathf.FloorToInt(current.y);

        if (Mathf.Abs(vel.x) > Mathf.Abs(vel.y)) targetY = Mathf.RoundToInt(current.y);
        else targetX = Mathf.RoundToInt(current.x);

        Vector2Int proposedTarget = new Vector2Int(targetX, targetY);

        // ตรวจสอบแผนที่: ถ้าเป้าหมายที่จะ Snap ไปเป็นกำแพงหรือกล่องที่เดินทะลุไม่ได้
        if (_mapChannel != null && (!_mapChannel.IsWalkable(proposedTarget) || _mapChannel.IsSolid(proposedTarget)))
        {
            // ยกเลิกการ Snap ไปข้างหน้า และบังคับให้ Snap ลงตำแหน่งปัจจุบันที่ปัดเศษแล้วแทน
            _targetSnapPos = new Vector2(Mathf.Round(current.x), Mathf.Round(current.y));
        }
        else
        {
            _targetSnapPos = new Vector2(targetX, targetY);
        }

        _isSmartSnapping = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("LivingThings"))
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _isSmartSnapping = false;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.gameObject.layer == LayerMask.NameToLayer("LivingThings"))
        {
            _collider.isTrigger = false;
        }
    }

    public void Initialize(BombBuilder builder, Vector2Int gridPos, StatsController ownerStats)
    {
        _ownerStats = ownerStats;
        _gridPosition = gridPos;
        _radius = builder.Radius;
        _isExploded = false;
        _onCriticalPhase = false;
        _isSmartSnapping = false;
        _lifeTime = 0f;

        _collider.isTrigger = true;

        transform.position = new Vector3(gridPos.x, gridPos.y, 0f);
        _rigidbody.position = transform.position;
        _rigidbody.linearVelocity = Vector2.zero;
    }

    public void ForceExplode()
    {
        if (_isExploded) return;
        _isExploded = true;

        if (_bombChannel != null) _bombChannel.RaiseBombExploded(Vector2Int.RoundToInt(transform.position), _radius);
        if (_ownerStats != null) _ownerStats.BombsRemaining++;

        string key = (_poolKey != null) ? _poolKey.name : gameObject.name.Replace("(Clone)", "").Trim();
        ObjectPoolManager.Instance.Release(key, this);
    }

    private void ExecuteSnapToGrid()
    {
        _rigidbody.linearVelocity = Vector2.zero;

        Vector2 snappedPos = _isSmartSnapping ? _targetSnapPos : new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        Vector2Int snapGrid = new Vector2Int(Mathf.RoundToInt(snappedPos.x), Mathf.RoundToInt(snappedPos.y));

        // ตรวจสอบความปลอดภัยรอบสุดท้ายก่อนบังคับ Snap จริง
        if (_mapChannel != null && (!_mapChannel.IsWalkable(snapGrid) || _mapChannel.IsSolid(snapGrid)))
        {
            snappedPos = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        }

        _rigidbody.MovePosition(snappedPos);
        transform.position = new Vector3(snappedPos.x, snappedPos.y, transform.position.z);
    }
}