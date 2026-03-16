using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public sealed class ScrollingTextureController : Singleton<ScrollingTextureController>
{
    [Header("Settings")]
    [SerializeField] private MeshRenderer _renderer;

    [SerializeField] private Vector2 _scrollSpeed = new Vector2(0.5f, 0f);

    private Vector2 _currentOffset = Vector2.zero;

    private void OnValidate()
    {
        if (_renderer == null) _renderer = this.GetComponent<MeshRenderer>();
    }

    private void Update()
    {
        _currentOffset += _scrollSpeed * Time.deltaTime;
        _renderer.material.mainTextureOffset = new Vector2(_currentOffset.x, _currentOffset.y);
    }
}