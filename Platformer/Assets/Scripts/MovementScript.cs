using UnityEngine;

public class MovementScript : MonoBehaviour
{
    [Header("Components")]
    private Rigidbody2D _rb;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _wallLayer;

    [Header("Movement Variables")]
    [SerializeField] private float _movementAcceleration;
    [SerializeField] private float _maxMoveSpeed;
    [SerializeField] private float _groundLinearDrag;
    private float _currentMaxMoveSpeed;
    private float _horizontalDirection;
    private bool _changingDirection => (_rb.velocity.x > 0f && _horizontalDirection < 0f) || (_rb.velocity.x < 0f && _horizontalDirection > 0f);

    [Header("Jump Variables")]
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _airLinearDrag = 2.5f;
    [SerializeField] private float _fallMultiplier = 8f;
    [SerializeField] private float _lowJumpFallMultiplier = 5f;
    [SerializeField] private int _extraJumps = 1;
    [SerializeField] private float _hangTime = .1f;
    [SerializeField] private float _jumpBufferLength = .1f;
    private int _extraJumpValue;
    private float _hangTimeCounter;
    private float _jumpBufferCounter;
    private bool _canJump => _jumpBufferCounter > 0 && (_hangTimeCounter > 0f || _extraJumpValue > 0);

    [Header("Ground Collision Variables")]
    [SerializeField] private float _groundRaycastLength;
    [SerializeField] private Vector3 _groundRaycastOffset;
    private bool _onGround;

    [Header("Corner Correction Variables")]
    [SerializeField] private float _topRaycastLength;
    [SerializeField] private Vector3 _edgeRaycastOffset;
    [SerializeField] private Vector3 _innerRaycastOffset;
    private bool _canCornerCorrect;

    [Header("Wall Collision Variables")]
    [SerializeField] private float _wallRaycastLength;
    [SerializeField] private Vector3 _wallRaycastOffset;
    private bool _closeToWall;
    //0 is left, 1 is right
    private int _direction;

    [Header("Wall Jump Variables")]
    [SerializeField] private float _wallJumpForce = 1f;
    private float _distanceFromWall = 1f;
    private float _turnOffForceTime = 0f;
    private float _turnOffForceCap = 0.5f;
    private bool _turnOffForceForWallJump = false;
    private Vector2 _wallJumpNormalizedVector;
    private bool _canWallJump => Input.GetButtonDown("Jump") && _closeToWall;


    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _wallJumpNormalizedVector = new Vector2(1f, 1f);
        _currentMaxMoveSpeed = _maxMoveSpeed;
    }

    void Update()
    {
        _horizontalDirection = GetInput().x;

        if (Input.GetButtonDown("Jump"))
        {
            _jumpBufferCounter = _jumpBufferLength;
        }
        else
            _jumpBufferCounter -= Time.deltaTime;

        _currentMaxMoveSpeed = _maxMoveSpeed;


    }

    private void FixedUpdate()
    {
        CheckGroundCollisions();
        CheckWallCollision();
        MoveCharacter();
        
        if (_onGround)
        {
            _extraJumpValue = _extraJumps;
            ApplyGroundLinearDrag();

            _hangTimeCounter = _hangTime;
        }
        else
        {
            ApplyAirinearDrag();
            FallMultiplier();
            _hangTimeCounter -= Time.fixedDeltaTime;
        }

        if (_canJump) Jump();

        if (_canCornerCorrect) CanCornerCorrect(_rb.velocity.y);
    }

    private static Vector2 GetInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private void CanCornerCorrect(float Yvelocity)
    {
        RaycastHit2D _hit = Physics2D.Raycast(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength, Vector3.left, _topRaycastLength, _groundLayer);
        if (_hit.collider != null)
        {
            float _newPos = Vector3.Distance(new Vector3(_hit.point.x, transform.position.y, 0f) + Vector3.up * _topRaycastLength,
                transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
            transform.position = new Vector3(transform.position.x + _newPos, transform.position.y, transform.position.z);
            _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y);
            return;
        }

        _hit = Physics2D.Raycast(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength, Vector3.right, _topRaycastLength, _groundLayer);
        if (_hit.collider != null)
        {
            float _newPos = Vector3.Distance(new Vector3(_hit.point.x, transform.position.y, 0f) + Vector3.up * _topRaycastLength,
                transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
            transform.position = new Vector3(transform.position.x - _newPos, transform.position.y, transform.position.z);
            _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y);
        }
    }

    private void MoveCharacter()
    {
        if (!_turnOffForceForWallJump)
        {
            _rb.AddForce(new Vector2(_horizontalDirection, 0f) * _movementAcceleration);

            if (Mathf.Abs(_rb.velocity.x) > _currentMaxMoveSpeed)
                _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * _currentMaxMoveSpeed, _rb.velocity.y);
        }
        else if (_turnOffForceForWallJump)
        {
            if (_turnOffForceTime < _turnOffForceCap)
                _turnOffForceTime += 1f * Time.deltaTime;
            else if (_turnOffForceTime >= _turnOffForceCap)
            {
                _turnOffForceTime = 0;
                _turnOffForceForWallJump = false;
            }
        }
    }

    private void ApplyGroundLinearDrag()
    {
        if (Mathf.Abs(_horizontalDirection) < 0.4f || _changingDirection)
        {
            _rb.drag = _groundLinearDrag;
        }
        else
        {
            _rb.drag = 0f;
        }
    }

    private void ApplyAirinearDrag()
    {
            _rb.drag = _airLinearDrag;
    }

    private void Jump()
    {
        if (!_onGround)
        {
            _extraJumpValue--;
        }

        ApplyAirinearDrag();
        _rb.velocity = new Vector2(_rb.velocity.x, 0f);
        _rb.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
        _hangTimeCounter = 0f;
        _jumpBufferCounter = 0f;
    }

    private void WallJump()
    {
        if(_direction == 0)
        {
            _wallJumpNormalizedVector =new Vector2(-1, 1).normalized;

            _rb.velocity = new Vector2(0f,0f);
            _rb.AddForce(_wallJumpNormalizedVector * _wallJumpForce, ForceMode2D.Impulse);
            _turnOffForceForWallJump = true;
        }
        else
        {
            _wallJumpNormalizedVector = new Vector2(1, 1).normalized;

            _rb.velocity = new Vector2(0f, 0f);
            _rb.AddForce(_wallJumpNormalizedVector * _wallJumpForce, ForceMode2D.Impulse);
            _turnOffForceForWallJump = true;
        }
    }

    private void FallMultiplier()
    {
        if (_rb.velocity.y < 0)
        {
            _rb.gravityScale = _fallMultiplier;
        }
        else if (_rb.velocity.y > 0 && !Input.GetButton("Jump"))
        {
            _rb.gravityScale = _lowJumpFallMultiplier;
        }
        else
        {
            _rb.gravityScale = 1f;
        }
    }

    private void CheckGroundCollisions()
    {
        _onGround = Physics2D.Raycast(transform.position + _groundRaycastOffset, Vector2.down, _groundRaycastLength, _groundLayer) ||
                                Physics2D.Raycast(transform.position - _groundRaycastOffset, Vector2.down, _groundRaycastLength, _groundLayer);

        _canCornerCorrect = Physics2D.Raycast(transform.position + _edgeRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) &&
            !Physics2D.Raycast(transform.position + _innerRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) ||
            Physics2D.Raycast(transform.position - _edgeRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) &&
            !Physics2D.Raycast(transform.position - _innerRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer);
    }

    private void CheckWallCollision()
    {
        if (Physics2D.Raycast(transform.position + _wallRaycastOffset, Vector2.left, _wallRaycastLength, _wallLayer))
        {
            _closeToWall = true;
            _direction = 0;
        }
        else if (Physics2D.Raycast(transform.position - _wallRaycastOffset, Vector2.right, _wallRaycastLength, _wallLayer))
        {
            _closeToWall = true;
            _direction = 1;
        }
        else
            _closeToWall = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + _groundRaycastOffset, transform.position + _groundRaycastOffset + Vector3.down * _groundRaycastLength);
        Gizmos.DrawLine(transform.position - _groundRaycastOffset, transform.position - _groundRaycastOffset + Vector3.down * _groundRaycastLength);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position + _wallRaycastOffset, transform.position + _wallRaycastOffset + Vector3.left * _wallRaycastLength);
        Gizmos.DrawLine(transform.position - _wallRaycastOffset, transform.position - _wallRaycastOffset + Vector3.right * _wallRaycastLength);

        Gizmos.DrawLine(transform.position + _edgeRaycastOffset, transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _edgeRaycastOffset, transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset, transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _innerRaycastOffset, transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength);

        Gizmos.DrawLine(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.left * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.right * _topRaycastLength);
    }
}
