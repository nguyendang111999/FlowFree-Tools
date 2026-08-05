using UnityEngine;

public class Cell : MonoBehaviour
{
    // Direction bits used by DisplayShape.
    public const int Up = 1 << 0;
    public const int Right = 1 << 1;
    public const int Down = 1 << 2;
    public const int Left = 1 << 3;

    [SerializeField] private GameObject _top;
    [SerializeField] private GameObject _right;
    [SerializeField] private GameObject _bottom;
    [SerializeField] private GameObject _left;
    [SerializeField] private SpriteRenderer _center;

    [SerializeField] private float _dotScale = 0.8f;
    [SerializeField] private float _pathScale = 0.4f;

    private SpriteRenderer _topRenderer;
    private SpriteRenderer _rightRenderer;
    private SpriteRenderer _bottomRenderer;
    private SpriteRenderer _leftRenderer;

    private void Awake()
    {
        if (_top != null) _topRenderer = _top.GetComponent<SpriteRenderer>();
        if (_right != null) _rightRenderer = _right.GetComponent<SpriteRenderer>();
        if (_bottom != null) _bottomRenderer = _bottom.GetComponent<SpriteRenderer>();
        if (_left != null) _leftRenderer = _left.GetComponent<SpriteRenderer>();
    }

    public void Clear()
    {
        if (_center != null) _center.enabled = false;
        SetActive(_top, false);
        SetActive(_right, false);
        SetActive(_bottom, false);
        SetActive(_left, false);
    }

    public void SetColor(Color color)
    {
        if (_center != null) _center.color = color;
        if (_topRenderer != null) _topRenderer.color = color;
        if (_rightRenderer != null) _rightRenderer.color = color;
        if (_bottomRenderer != null) _bottomRenderer.color = color;
        if (_leftRenderer != null) _leftRenderer.color = color;
    }

    /// <param name="dirMask">Bitmask of connected neighbors (Up/Right/Down/Left).</param>
    /// <param name="isDot">True when this cell is an endpoint dot.</param>
    public void DisplayShape(int dirMask, bool isDot)
    {
        if (_center != null)
        {
            _center.enabled = true;
            float scale = isDot ? _dotScale : _pathScale;
            _center.transform.localScale = new Vector3(scale, scale, 1f);
        }

        SetActive(_top, (dirMask & Up) != 0);
        SetActive(_right, (dirMask & Right) != 0);
        SetActive(_bottom, (dirMask & Down) != 0);
        SetActive(_left, (dirMask & Left) != 0);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}