using UnityEngine;

public class MovementScript : MonoBehaviour
{
    public ParticleSystem dust;

    [Header("Components")]
    private Rigidbody2D _rb;
    private CapsuleCollider2D _capsuleCollider;
    private SpriteRenderer _spriteRenderer;

    [Header("Slope Variables")]
    [SerializeField] private PhysicsMaterial2D _noFriction;
    [SerializeField] private PhysicsMaterial2D _fullFriction;
    [SerializeField] private float _slopeCheckDistance;
    [SerializeField] private float _maxSlopeAngle;
    private Vector2 _slopeNormalPerp;
    private bool _isOnSlope;
    private bool _canWalkOnSlope;
    private float _slopeDownAngle;
    private float _slopeDownAngleOld;
    private float _slopeSideAngle;


    [Header("Layer Masks")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _wallLayer;

    [Header("Movement Variables")]
    [SerializeField] private float _movementAcceleration;
    [SerializeField] private float _maxMoveSpeed;
    [SerializeField] private float _groundLinearDrag;
    private float _horizontalDirection;
    private bool _changingDirection => (_rb.velocity.x > 0f && _horizontalDirection < 0f) || (_rb.velocity.x < 0f && _horizontalDirection > 0f);
    private bool _facingRight = true; 

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
    private bool _canJump => _jumpBufferCounter > 0 && (_hangTimeCounter > 0f  || _extraJumpValue > 0 || _onWall);
    private bool _isJumping = false;
    private bool _canMove => !_wallGrab;

    private bool _wallGrab => _onWall && ! _onGround && Input.GetButton("WallGrab");

    [Header("Ground Collision Variables")]
    [SerializeField] private float _groundRaycastLength;
    [SerializeField] private Vector3 _groundRaycastOffset;
    private bool _onGround;

    [Header("Wall Collision Variables")]
    [SerializeField] private float _wallRaycastLength;
    private bool _onWall;
    private bool _onRightWall;

    [Header("Wall Movement Variables")]
    [SerializeField] private float _wallSlideModifier = 0.5f;
    //[SerializeField] private float _wallJumpXVelocityHaltDelay = 0.2f;
    private bool _wallSlide => _onWall && !_onGround && Input.GetAxisRaw("Horizontal") != 0 && !Input.GetButton("WallGrab") && _rb.velocity.y < 0f;
    private bool _isWallJumping;
    private float _wallJumpCounter;
    private float _wallJumpTime = 0.3f;

    [Header("Corner Correction Variables")]
    [SerializeField] private float _topRaycastLength;
    [SerializeField] private Vector3 _edgeRaycastOffset;
    [SerializeField] private Vector3 _innerRaycastOffset;
    private bool _canCornerCorrect;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _capsuleCollider = GetComponent<CapsuleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
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

        if (_horizontalDirection < 0f && _facingRight)
        {
            Flip();
        }
        else if(_horizontalDirection > 0f && !_facingRight)
        {
            Flip();
        }
    }

    private void FixedUpdate()
    {
        CheckCollisions();
        SlopeCheck();

        if (_canMove) MoveCharacter();
        else _rb.velocity = Vector2.Lerp(_rb.velocity, (new Vector2(_horizontalDirection * _maxMoveSpeed, _rb.velocity.y)), 0.5f * Time.fixedDeltaTime);
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
            if (!_onWall || _rb.velocity.y < 0f) _isJumping = false;
        }

        if (_canJump)
        {
            if (_onWall && !_onGround)
            {
                WallJump();
                CreateDust();
            }
            else
            {
                Jump(Vector2.up);
                CreateDust();
            }
        }
        if (_canCornerCorrect) CanCornerCorrect(_rb.velocity.y);
        if (!_isJumping)
        {
            if (_wallGrab) WallGrab();
            if (_wallSlide) WallSlide();
            if (_onWall) StickToWall();
        }
    }

    private static Vector2 GetInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private void CanCornerCorrect(float _yVelocity)
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
        if (!_isWallJumping && !_isOnSlope && _onGround && !_isJumping)
        {
            _rb.AddForce(new Vector2(_horizontalDirection, 0f) * _movementAcceleration);

            if (Mathf.Abs(_rb.velocity.x) > _maxMoveSpeed)
                _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * _maxMoveSpeed, _rb.velocity.y);
        }
        else if(!_isWallJumping && _isOnSlope && _onGround && !_isJumping && _canWalkOnSlope)
        {
            _rb.AddForce(_slopeNormalPerp * -_horizontalDirection * _movementAcceleration);

            if (Mathf.Abs(_rb.velocity.x) > _maxMoveSpeed)
                _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * _maxMoveSpeed, _rb.velocity.y);
        }
        else if (!_isWallJumping && !_onGround)
        {
            _rb.AddForce(new Vector2(_horizontalDirection, 0f) * _movementAcceleration);

            if (Mathf.Abs(_rb.velocity.x) > _maxMoveSpeed)
                _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * _maxMoveSpeed, _rb.velocity.y);
        }
        else if (_isWallJumping)
        {
            _wallJumpCounter += Time.fixedDeltaTime;

            if (_wallJumpCounter >= _wallJumpTime)
            {
                _isWallJumping = false;
            }
        }
    }

    void Flip()
    {
        CreateDust();
        _facingRight = !_facingRight;
        _spriteRenderer.flipX = true;
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

    private void Jump(Vector2 direction)
    {
        if (!_onGround && !_onWall)
            _extraJumpValue--;

        ApplyAirinearDrag();
        _rb.velocity = new Vector2(_rb.velocity.x, 0f);
        _rb.AddForce(direction * _jumpForce, ForceMode2D.Impulse);
        _hangTimeCounter = 0f;
        _jumpBufferCounter = 0f;
        _isJumping = true;
    }

    private void WallJump()
    {
        _isWallJumping = true;
        _wallJumpCounter = 0;
        Vector2 jumpDirection = _onRightWall ? Vector2.left : Vector2.right;
        Jump(Vector2.up + jumpDirection);
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

    void WallGrab()
    {
        _rb.gravityScale = 0f;
        _rb.velocity = new Vector2(_rb.velocity.x, 0f);
        
    }

    void WallSlide()
    {
        _rb.velocity = new Vector2(_rb.velocity.x, -_maxMoveSpeed * _wallSlideModifier);
        CreateDust();
    }

    void StickToWall()
    {
        if (_onRightWall && _horizontalDirection >= 0)
            _rb.velocity = new Vector2(1f, _rb.velocity.y);
        else if (!_onRightWall && _horizontalDirection <= 0)
            _rb.velocity = new Vector2(-1f, _rb.velocity.y);
    }

    private void CheckCollisions()
    {
        #region Ground Collisions
        _onGround = Physics2D.Raycast(transform.position + _groundRaycastOffset, Vector2.down, _groundRaycastLength, _groundLayer) ||
                                Physics2D.Raycast(transform.position - _groundRaycastOffset, Vector2.down, _groundRaycastLength, _groundLayer);
        #endregion
        #region Corner Collisions
        _canCornerCorrect = Physics2D.Raycast(transform.position + _edgeRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) &&
            !Physics2D.Raycast(transform.position + _innerRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) ||
            Physics2D.Raycast(transform.position - _edgeRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer) &&
            !Physics2D.Raycast(transform.position - _innerRaycastOffset, Vector2.up, _topRaycastLength, _groundLayer);
        #endregion

        #region Wall Collisions
        _onWall = Physics2D.Raycast(transform.position, Vector2.right, _wallRaycastLength, _wallLayer) ||
                    Physics2D.Raycast(transform.position, Vector2.left, _wallRaycastLength, _wallLayer);
        _onRightWall = Physics2D.Raycast(transform.position, Vector2.right, _wallRaycastLength, _wallLayer);
        #endregion
    }

    private void SlopeCheck()
    {
        Vector2 _checkPos = transform.position - new Vector3(0.0f, transform.localScale.y / 2);
        SlopeCheckHorizontal(_checkPos);
        SlopeCheckVertical(_checkPos);
    }

    private void SlopeCheckHorizontal(Vector2 _checkPos)
    {
        RaycastHit2D _slopeHitFront = Physics2D.Raycast(_checkPos, transform.right, _slopeCheckDistance, _groundLayer);
        RaycastHit2D _slopeHitBack = Physics2D.Raycast(_checkPos, -transform.right, _slopeCheckDistance, _groundLayer);

        if (_slopeHitFront)
        {
            _isOnSlope = true;
            _slopeSideAngle = Vector2.Angle(_slopeHitFront.normal, Vector2.up);
        }
        else if (_slopeHitBack)
        {
            _isOnSlope = true;
            _slopeSideAngle = Vector2.Angle(_slopeHitBack.normal, Vector2.up);
        }
        else
        {
            _slopeSideAngle = 0.0f;
            _isOnSlope = false;
        }
    }

    private void SlopeCheckVertical(Vector2 _checkPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(_checkPos, Vector2.down, _slopeCheckDistance, _groundLayer);

        if (hit)
        {
            _slopeNormalPerp = Vector2.Perpendicular(hit.normal).normalized;
            _slopeDownAngle = Vector2.Angle(hit.normal, Vector2.up);

            if(_slopeDownAngle != _slopeDownAngleOld)
            {
                _isOnSlope = true;
            }

            _slopeDownAngleOld = _slopeDownAngle;
            Debug.DrawRay(hit.point, _slopeNormalPerp, Color.red);
            Debug.DrawRay(hit.point, hit.normal, Color.red);
        }

        if(_slopeDownAngle > _maxSlopeAngle || _slopeSideAngle > _maxSlopeAngle)
        {
            _canWalkOnSlope = false;
        }
        else
        {
            _canWalkOnSlope = true;
        }

        if (_isOnSlope && _horizontalDirection == 0f && _canWalkOnSlope)
        {
            _rb.sharedMaterial = _fullFriction;
        }
        else
        {
            _rb.sharedMaterial = _noFriction;
        }
    }

    private void OnDrawGizmos()
    {
        //Ground Collisions Gizmos
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + _groundRaycastOffset, transform.position + _groundRaycastOffset + Vector3.down * _groundRaycastLength);
        Gizmos.DrawLine(transform.position - _groundRaycastOffset, transform.position - _groundRaycastOffset + Vector3.down * _groundRaycastLength);

        //Corner Collisions Gizmos
        Gizmos.DrawLine(transform.position + _edgeRaycastOffset, transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _edgeRaycastOffset, transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset, transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _innerRaycastOffset, transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength);

        Gizmos.DrawLine(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.left * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.right * _topRaycastLength);

        //Wall Collisions Gizmos
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * _wallRaycastLength);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.left * _wallRaycastLength);
    }

    void CreateDust()
    {
        dust.Play();
    }
}