using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <para> (TH) : สคริปต์จัดการ UI แถบสถานะ เปลี่ยนการซ่อนหลอดเป็นการปรับ Alpha เป็น 0 เพื่อรักษา Layout Group ไว้ และดึงค่าเริ่มต้นใน Start </para>
/// <para> (EN) : Manages UI stat bars. Uses Alpha = 0 instead of SetActive to preserve Layout Group spacing, and initializes in Start. </para>
/// </summary>
public class CharacterStatsUI : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private StatsController _statsController;

    [Header("Bar Parents (Assign from Hierarchy)")]
    [SerializeField] private Transform _hpBarParent;
    [SerializeField] private Transform _bombBarParent;
    [SerializeField] private Transform _speedBarParent;
    [SerializeField] private Transform _blastBarParent;

    [SerializeField] private GameObject _out;

    [Header("Blink Settings")]
    [SerializeField] private float _blinkDuration = 0.15f;
    [SerializeField] private Color _blinkColor = Color.black;

    private Image[] _hpImages;
    private Image[] _bombImages;
    private Image[] _speedImages;
    private Image[] _blastImages;

    private Color _hpColor;
    private Color _bombColor;
    private Color _speedColor;
    private Color _blastColor;

    private int _currentHp = -1;
    private int _currentBomb = -1;
    private int _currentSpeed = -1;
    private int _currentBlast = -1;

    private void Awake()
    {
        _hpImages = SetupBar(_hpBarParent, out _hpColor);
        _bombImages = SetupBar(_bombBarParent, out _bombColor);
        _speedImages = SetupBar(_speedBarParent, out _speedColor);
        _blastImages = SetupBar(_blastBarParent, out _blastColor);
    }

    private void Start()
    {
        // อัปเดตค่าเริ่มต้นให้กับ UI โดยดึงค่ามาจาก StatsController ที่ Awake มาเรียบร้อยแล้ว
        if (_statsController != null)
        {
            UpdateHpUI(_statsController.CurrentHp);
            UpdateBombUI(_statsController.CurrentBombAmount);
            UpdateSpeedUI(_statsController.CurrentSpeed);
            UpdateBlastUI(_statsController.CurrentExplosionRange);
        }
    }

    private void OnEnable()
    {
        if (_statsController != null)
        {
            _statsController.OnHpChange += UpdateHpUI;
            _statsController.OnBombChange += UpdateBombUI;
            _statsController.OnSpeedChange += UpdateSpeedUI;
            _statsController.OnExpoleChange += UpdateBlastUI;
        }
    }

    private void OnDisable()
    {
        if (_statsController != null)
        {
            _statsController.OnHpChange -= UpdateHpUI;
            _statsController.OnBombChange -= UpdateBombUI;
            _statsController.OnSpeedChange -= UpdateSpeedUI;
            _statsController.OnExpoleChange -= UpdateBlastUI;
        }
    }

    private void UpdateHpUI(int newValue) => UpdateSingleBar(ref _currentHp, newValue, _hpImages, _hpColor);
    private void UpdateBombUI(int newValue) => UpdateSingleBar(ref _currentBomb, newValue, _bombImages, _bombColor);
    private void UpdateSpeedUI(int newValue) => UpdateSingleBar(ref _currentSpeed, newValue, _speedImages, _speedColor);
    private void UpdateBlastUI(int newValue) => UpdateSingleBar(ref _currentBlast, newValue, _blastImages, _blastColor);

    private Image[] SetupBar(Transform parent, out Color originalColor)
    {
        originalColor = Color.white;
        if (parent == null) return new Image[0];

        List<Image> images = new List<Image>();
        foreach (Transform child in parent)
        {
            Image img = child.GetComponent<Image>();
            if (img != null) images.Add(img);
        }

        if (images.Count > 0) originalColor = images[0].color;
        return images.ToArray();
    }

    private void UpdateSingleBar(ref int currentValue, int newValue, Image[] barImages, Color originalColor)
    {
        if (barImages.Length == 0) return;

        // เช็กค่า -1 สำหรับตอน Start() เพื่อจัด Alpha ให้ถูกต้องโดยไม่กะพริบ
        if (currentValue == -1)
        {
            currentValue = newValue;
            ApplyBarState(newValue, barImages, originalColor);
            return;
        }

        if (currentValue != newValue)
        {
            StartCoroutine(BlinkRoutine(currentValue, newValue, barImages, originalColor));
            currentValue = newValue;
        }
    }

    private IEnumerator BlinkRoutine(int oldValue, int newValue, Image[] barImages, Color originalColor)
    {
        int minIndex = Mathf.Min(oldValue, newValue);
        int maxIndex = Mathf.Max(oldValue, newValue);

        // ปรับ Alpha ให้เต็มก่อนเริ่มกะพริบ (สำหรับหลอดที่กำลังเพิ่มขึ้น)
        for (int i = minIndex; i < maxIndex; i++)
        {
            if (i < barImages.Length)
            {
                Color c = originalColor;
                c.a = originalColor.a;
                barImages[i].color = c;
            }
        }

        // กะพริบ 2 รอบ
        for (int blink = 0; blink < 2; blink++)
        {
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (i < barImages.Length)
                {
                    Color c = _blinkColor;
                    c.a = 1f; // ให้สีตอนกะพริบทึบ 100%
                    barImages[i].color = c;
                }
            }
            yield return new WaitForSeconds(_blinkDuration);

            for (int i = minIndex; i < maxIndex; i++)
            {
                if (i < barImages.Length)
                {
                    Color c = originalColor;
                    c.a = originalColor.a;
                    barImages[i].color = c;
                }
            }
            yield return new WaitForSeconds(_blinkDuration);
        }

        // ปิดหรือเปิดใช้งาน Object ด้วย Alpha ตามค่าปัจจุบันจริงๆ หลังกะพริบเสร็จ
        ApplyBarState(newValue, barImages, originalColor);
        if (_currentHp <= 0f) _out.SetActive(true);
    }

    private void ApplyBarState(int value, Image[] barImages, Color originalColor)
    {
        for (int i = 0; i < barImages.Length; i++)
        {
            Color c = originalColor;
            // ถ้า Index น้อยกว่าค่าที่มี ให้แสดงผล (ใช้ Alpha เดิม) ถ้ามากกว่าให้ซ่อน (Alpha = 0)
            c.a = (i < value) ? originalColor.a : 0f;
            barImages[i].color = c;
        }
    }
}