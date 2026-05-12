using UnityEngine;
using UnityEngine.Events;

namespace Microstructure
{
    /// Handles sphere collider functionality for raycasting and click detection.
    /// Can be attached to any GameObject that needs clickable sphere collider behavior.
    [RequireComponent(typeof(SphereCollider))]
    public class SphereColliderHandler : MonoBehaviour
    {
        [Header("Collider Settings")]
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private bool isTrigger = true;
        
        [Header("Click Events")]
        [SerializeField] private UnityEvent onClick;
        [SerializeField] private UnityEvent onHoverEnter;
        [SerializeField] private UnityEvent onHoverExit;
        
        [Header("Visual Feedback")]
        [SerializeField] private bool enableHighlight = true;
        [SerializeField] private Color hoverColor = Color.cyan;
        [SerializeField] private float hoverScaleMultiplier = 1.2f;
        
        // Components
        private SphereCollider _collider;
        private Transform _transform;
        private Vector3 _originalScale;
        
        // State
        private bool _isHovered = false;
        private Material _originalMaterial;
        private Material _hoverMaterial;
        private MeshRenderer _meshRenderer;
        
        // Events for external listeners
        public System.Action OnClicked;
        public System.Action OnHoverEnter;
        public System.Action OnHoverExit;
        
        // Properties
        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Max(0.001f, value);
                if (_collider != null)
                    _collider.radius = radius;
            }
        }
        
        public bool IsHovered => _isHovered;
        public bool IsTrigger
        {
            get => isTrigger;
            set
            {
                isTrigger = value;
                if (_collider != null)
                    _collider.isTrigger = isTrigger;
            }
        }
        
        private void Awake()
        {
            _transform = transform;
            _meshRenderer = GetComponent<MeshRenderer>();
            
            SetupCollider();
            StoreOriginalMaterial();
            SetupHoverMaterial();
        }
        
        private void Start()
        {
            _originalScale = _transform.localScale;
        }
        
        private void SetupCollider()
        {
            _collider = GetComponent<SphereCollider>();
            if (_collider == null)
            {
                Debug.LogError($"SphereColliderHandler: No SphereCollider found on {gameObject.name}!");
                return;
            }
            
            _collider.radius = radius;
            _collider.isTrigger = isTrigger;
            
            Debug.Log($"[SphereColliderHandler] Set up collider on {gameObject.name} - Radius: {radius}, IsTrigger: {isTrigger}");
        }
        
        private void StoreOriginalMaterial()
        {
            if (_meshRenderer != null && _meshRenderer.material != null)
            {
                _originalMaterial = _meshRenderer.material;
            }
        }
        
        private void SetupHoverMaterial()
        {
            if (_meshRenderer != null && _originalMaterial != null)
            {
                _hoverMaterial = new Material(_originalMaterial);
                _hoverMaterial.color = hoverColor;
            }
        }
        
        private void OnMouseEnter()
        {
            if (!enableHighlight) return;
            
            _isHovered = true;
            
            // Change material
            if (_meshRenderer != null && _hoverMaterial != null)
            {
                _meshRenderer.material = _hoverMaterial;
            }
            
            // Scale up
            _transform.localScale = _originalScale * hoverScaleMultiplier;
            
            // Invoke events
            onHoverEnter?.Invoke();
            OnHoverEnter?.Invoke();
        }
        
        private void OnMouseExit()
        {
            if (!enableHighlight) return;
            
            _isHovered = false;
            
            // Restore original material
            if (_meshRenderer != null && _originalMaterial != null)
            {
                _meshRenderer.material = _originalMaterial;
            }
            
            // Restore scale
            _transform.localScale = _originalScale;
            
            // Invoke events
            onHoverExit?.Invoke();
            OnHoverExit?.Invoke();
        }
        
        private void OnMouseDown()
        {
            Debug.Log($"[SphereColliderHandler] Clicked on {gameObject.name}");
            
            // Invoke events
            onClick?.Invoke();
            OnClicked?.Invoke();
        }
        
        private void OnValidate()
        {
            if (Application.isPlaying && _collider != null)
            {
                _collider.radius = radius;
                _collider.isTrigger = isTrigger;
            }
        }
        
        private void OnDestroy()
        {
            // Clean up hover material
            if (_hoverMaterial != null && _hoverMaterial != _originalMaterial)
            {
                Destroy(_hoverMaterial);
            }
        }
    }
}