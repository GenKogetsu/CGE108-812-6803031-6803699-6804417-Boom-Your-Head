using UnityEngine;
using UnityEngine.UI; // สำหรับ UI ปกติ
using TMPro;           // สำหรับ TextMeshPro (แนะนำให้ใช้ตัวนี้)

[ExecuteAlways]
public class SliderToText : MonoBehaviour
{
    [Header("UI References")]
    public Slider mySlider;
    public TextMeshProUGUI percentageText;

    void Start()
    {
        // ตั้งค่าเริ่มต้นให้ Slider (ถ้าต้องการ)
        mySlider.minValue = 0;
        mySlider.maxValue = 100;

        // เพิ่ม Listener เพื่อให้โค้ดทำงานทุกครั้งที่ค่า Slider เปลี่ยน
        mySlider.onValueChanged.AddListener((value) => {
            UpdateText(value);
        });

        // อัปเดต Text ครั้งแรกตามค่าเริ่มต้นของ Slider
        UpdateText(mySlider.value);
    }

    void UpdateText(float value)
    {
        // แสดงผลเป็นตัวเลขจำนวนเต็ม + เครื่องหมาย %
        percentageText.text = value.ToString("0") + "%";
    }
}