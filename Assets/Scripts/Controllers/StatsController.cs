using UnityEngine;
using NaughtyAttributes;
using Genoverrei.DesignPattern;
using Genoverrei.Libary;
using System;

/// <summary>
/// <para> (TH) : ตัวจัดการค่าสถานะของตัวละคร รวมถึงระบบพลังชีวิต การรับดาเมจ สถานะอมตะชั่วคราว และการรีเซ็ตค่าสถานะ </para>
/// <para> (EN) : Manager for character statistics including health, damage, temporary invincibility, and status resetting. </para>
/// </summary>
public sealed class StatsController : MonoBehaviour, ITakeDamageable
{
    #region Variable

    [Header("Assign Data")]
    [SerializeField] private LivingThingsScriptable _statsData;

    [Header("Auto Linked Components")]
    [ReadOnly][SerializeField] private Character _livingName;
    [ReadOnly][SerializeField] private Animator _characterAnimator;
    [ReadOnly][SerializeField] private SpriteRenderer _characterSprite;
    [ReadOnly][SerializeField] private Rigidbody2D _characterRigidbody;
    [ReadOnly][SerializeField] private Collider2D _characterCollider;

    [Header("Damage Settings")]
    [SerializeField] private float _invincibilityTime = 1.2f;
    [SerializeField] private float _flashSpeed = 15f;
    [SerializeField] private GameObject _hitEffectPrefab;

    [Header("UI Observer Channel")]
    [SerializeField] private PlayerStatsChannelSO _statsChannel; // 🚀 ท่อส่งสัญญาณไปที่ UI List

    [Header("Runtime Stats")]
    [ReadOnly][SerializeField] private bool _isInvincible;

    // แก้ไข: เปลี่ยนกลับเป็นตัวแปรธรรมดาเพื่อป้องกัน StackOverflow
    [MinValue(0), MaxValue(10)]
    private int _currentHp;
    
    [MinValue(0), MaxValue(10)]
    private int _currentAtk;
    
    [MinValue(0), MaxValue(10)]
    private int _currentSpeed;

    [MinValue(0), MaxValue(10)]
    private int _currentBombAmount;

    [MinValue(0), MaxValue(10)]
    private int _currentExplosionRange;
    [ReadOnly] public int _bombsRemaining;

    public Action<int> OnHpChange;
    public Action<int> OnBombChange;
    public Action<int> OnSpeedChange;
    public Action<int> OnExpoleChange;
    #endregion //Variable

    #region ITakeDamageable Properties

    public bool IsInvincible { get => _isInvincible; set => _isInvincible = value; }
    public SpriteRenderer SpriteRenderer => _characterSprite;
    public MonoBehaviour CoroutineRunner => this;

    #endregion //ITakeDamageable Properties

    #region Explicit Interface Implementation

    void ITakeDamageable.TakeDamage(int amount) => ExecuteTakeDamage(amount);
    void ITakeDamageable.ApplyDamage(int amount) => ExecuteApplyDamage(amount);

    #endregion //Explicit Interface Implementation

    #region Properties

    public Character LivingName => _livingName;

    public int CurrentHp
    {
        get => _currentHp;
        private set
        {
            _currentHp = Mathf.Min(10, value);
            OnHpChanged();
            if (_currentHp <= 0) OnDeath();
            BroadcastStatsUpdate(); // 🚀 ส่งสัญญาณเมื่อเลือดเปลี่ยน
            OnHpChange?.Invoke(_currentHp); // 🚀 ย้าย Invoke มาไว้ตรงนี้
        }
    }

    public int CurrentSpeed
    {
        get => _currentSpeed;
        set
        {
            _currentSpeed = Mathf.Min(10, value);
            BroadcastStatsUpdate();
            OnSpeedChange?.Invoke(_currentSpeed); // 🚀 ย้าย Invoke มาไว้ตรงนี้
        }
    }

    public int CurrentBombAmount
    {
        get => _currentBombAmount;
        set
        {
            _currentBombAmount = Mathf.Min(10, value);
        }
    }

    public int CurrentExplosionRange
    {
        get => _currentExplosionRange;
        set
        {
            _currentExplosionRange = Mathf.Min(10, value);
            BroadcastStatsUpdate();
            OnExpoleChange?.Invoke(_currentExplosionRange); // 🚀 ย้าย Invoke มาไว้ตรงนี้
        }
    }

    public int CurrentAtk
    {
        get => _currentAtk;
        set { _currentAtk = Mathf.Max(0, value); }
    }

    public int BombsRemaining
    {
        get => _bombsRemaining;
        set 
        {
            _bombsRemaining = Mathf.Clamp(value, 0, _currentBombAmount); 
            BroadcastStatsUpdate();
            OnBombChange?.Invoke(_bombsRemaining);
        }
    }

    #endregion //Properties

    #region Unity Lifecycle
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_characterAnimator == null) _characterAnimator = GetComponentInChildren<Animator>();
        if (_characterSprite == null) _characterSprite = GetComponentInChildren<SpriteRenderer>();
        if (_characterRigidbody == null) _characterRigidbody = GetComponentInChildren<Rigidbody2D>();
        if (_characterCollider == null) _characterCollider = GetComponentInChildren<Collider2D>();
        if (_statsData != null) _livingName = _statsData.livingName;
    }
#endif

    private void Awake() => SyncData();

#endregion //Unity Lifecycle

    #region Public Methods

    public void SyncData()
    {
        if (_statsData == null) return;
        _livingName = _statsData.livingName;
        _currentHp = _statsData.baseHp;
        _currentAtk = _statsData.baseAtk;
        _currentSpeed = (int)_statsData.baseSpeed;
        _currentBombAmount = _statsData.baseBombAmount;
        _currentExplosionRange = _statsData.baseExplosionRange;
        _bombsRemaining = _currentBombAmount;

        BroadcastStatsUpdate(); // 🚀 ส่งข้อมูลเริ่มต้น
    }

    public void ResetStats()
    {
        SyncData();
        IsInvincible = false;

        if (_characterSprite != null)
        {
            Color c = _characterSprite.color;
            c.a = 1f;
            _characterSprite.color = c;
        }

        if (_characterRigidbody != null)
        {
            _characterRigidbody.simulated = true;
            _characterRigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        if (_characterCollider != null) _characterCollider.enabled = true;

        this.enabled = true;
        if (_characterAnimator != null)
        {
            _characterAnimator.enabled = true;
            _characterAnimator.SetInteger("Hp", _currentHp);
            _characterAnimator.Play("Idle");
        }

        Debug.Log($"<b><color=#4FC3F7>[Stats Reset]</color></b> {name} restored.");
    }

    public void OnHitItem(string itemTag)
    {
        switch (itemTag)
        {
            case "HpItem": CurrentHp++; break;
            case "SpeedItem": CurrentSpeed++; break;
            case "BombAmountItem": CurrentBombAmount++; BombsRemaining++; break;
            case "ExplosionRangeItem": CurrentExplosionRange++; break;
        }
    }

    #endregion //Public Methods

    #region Private Logic

    // 🚀 ฟังก์ชันส่งข้อมูลเข้าท่อส่งสัญญาณ (Signal)
    private void BroadcastStatsUpdate()
    {
        if (_statsChannel == null || _livingName == Character.None) return;

        // มัดรวมข้อมูลใส่ Struct (ต้องตั้งชื่อตัวแปรให้ตรงกับใน StatsChangeEvent)
        StatsChangeEvent payload = new StatsChangeEvent
        {
            CharacterType = _livingName,
            Hp = _currentHp,
            BombAmount = _currentBombAmount,
            Speed = _currentSpeed,
            ExplosionRange = _currentExplosionRange
        };

        _statsChannel.RaiseEvent(payload);
    }

    private void ExecuteTakeDamage(int amount)
    {
        DamageAbility<StatsController>.TakeDamage(this, amount, _invincibilityTime, _flashSpeed, onHit: () => {
            SpawnHitEffect();
        });
    }

    private void ExecuteApplyDamage(int amount)
    {
        if (IsInvincible) return;
        CurrentHp -= amount;
    }

    private void SpawnHitEffect()
    {
        if (_hitEffectPrefab != null) Instantiate(_hitEffectPrefab, transform.position, Quaternion.identity);
    }

    private void OnHpChanged()
    {
        #if UNITY_EDITOR
        Debug.Log($"<b><color=#FF5252>[Stats]</color></b> {name} HP: <color=#81C784>{_currentHp}</color>");
        #endif
        if (_characterAnimator != null) _characterAnimator.SetInteger("Hp", _currentHp);
    }

    private void OnDeath()
    {
        if (_characterRigidbody != null)
        {
            _characterRigidbody.simulated = false;
            _characterRigidbody.bodyType = RigidbodyType2D.Static;
        }

        if (_characterCollider != null) _characterCollider.enabled = false;

        EventBus.Instance.Publish(new CharacterDeathEvent(this.gameObject, _statsData.livingName));

        this.enabled = false;

        BroadcastStatsUpdate(); // 🚀 ส่งสัญญาณครั้งสุดท้ายเมื่อตาย (เพื่อเซ็ตป้าย OUT)
        Debug.Log($"<b><color=#FF5252>[Death]</color></b> {name} disabled.");
    }

    #endregion //Private Logic
}